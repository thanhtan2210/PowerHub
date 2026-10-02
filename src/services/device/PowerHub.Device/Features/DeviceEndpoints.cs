using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PowerHub.Device.Data;

namespace PowerHub.Device.Features;

public sealed record RegisterDeviceRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Required, StringLength(50)] string Kind);

public sealed record UpdateDeviceRequest([property: Required, StringLength(100)] string Name);

public sealed record DeviceResponse(
    Guid Id, string Name, string Kind, string Permission, bool IsOwner, DateTimeOffset CreatedAt, int Version);

/// <summary>The credential is returned only here and is not retrievable afterwards.</summary>
public sealed record DeviceCredentialResponse(DeviceResponse Device, string Credential);

public sealed record DevicePage(IReadOnlyList<DeviceResponse> Items, Guid? NextCursor);

public static class DeviceEndpoints
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        var devices = app.MapGroup("/api/v1/devices").WithTags("Devices").RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        devices.MapGet("/", List).WithName("ListDevices");
        devices.MapPost("/", Register).WithName("RegisterDevice");

        var one = devices.MapGroup("/{deviceId:guid}").ProducesProblem(StatusCodes.Status404NotFound);
        one.MapGet("/", Get).WithName("GetDevice");
        one.MapPatch("/", Update).WithName("UpdateDevice")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
        one.MapDelete("/", Remove).WithName("RemoveDevice").ProducesProblem(StatusCodes.Status403Forbidden);
        one.MapPost("/credentials", RotateCredential).WithName("RotateDeviceCredential")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static Guid UserId(this ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue("sub")!);

    private static async Task<Ok<DevicePage>> List(
        ClaimsPrincipal user, DeviceDb db, Guid? cursor, int? limit, CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);

        // Membership is the only path to a device: there is no unscoped query in this service.
        var query =
            from member in db.Members
            join device in db.Devices on member.DeviceId equals device.Id
            where member.UserId == userId && device.RemovedAt == null
            select new { device, member };

        if (cursor is { } after)
        {
            query = query.Where(row => row.device.Id.CompareTo(after) > 0);
        }

        var rows = await query.OrderBy(row => row.device.Id).Take(pageSize + 1).AsNoTracking().ToListAsync(cancellationToken);
        var hasMore = rows.Count > pageSize;
        var items = rows.Take(pageSize).Select(row => ToResponse(row.device, row.member)).ToList();
        return TypedResults.Ok(new DevicePage(items, hasMore ? items[^1].Id : null));
    }

    private static async Task<Created<DeviceCredentialResponse>> Register(
        RegisterDeviceRequest request,
        HttpContext http,
        DeviceDb db,
        ChangeRecorder changes,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = http.User.UserId();
        var now = time.GetUtcNow();
        var device = new DeviceRecord
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            Kind = request.Kind.Trim(),
            CreatedAt = now,
            Version = 1,
        };
        var member = new DeviceMember
        {
            DeviceId = device.Id,
            UserId = userId,
            Permission = DevicePermission.Manage,
            IsOwner = true,
            CreatedAt = now,
        };

        db.Devices.Add(device);
        db.Members.Add(member);
        var secret = AddCredential(db, device.Id, now);
        changes.Audit(AuditActions.Registered, device.Id, userId);
        changes.Publish(EventTypes.Registered, device.Id, new { deviceId = device.Id, ownerUserId = userId, kind = device.Kind });
        await db.SaveChangesAsync(cancellationToken);

        SetETag(http, device);
        return TypedResults.Created($"/api/v1/devices/{device.Id}", new DeviceCredentialResponse(ToResponse(device, member), secret));
    }

    private static async Task<Results<Ok<DeviceResponse>, ProblemHttpResult>> Get(
        Guid deviceId, HttpContext http, DeviceDb db, CancellationToken cancellationToken)
    {
        var (device, member, problem) = await FindAsync(db, deviceId, http.User.UserId(), DevicePermission.View, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        SetETag(http, device!);
        return TypedResults.Ok(ToResponse(device!, member!));
    }

    private static async Task<Results<Ok<DeviceResponse>, ProblemHttpResult>> Update(
        Guid deviceId,
        UpdateDeviceRequest request,
        HttpContext http,
        DeviceDb db,
        ChangeRecorder changes,
        CancellationToken cancellationToken)
    {
        var userId = http.User.UserId();
        var (device, member, problem) = await FindAsync(db, deviceId, userId, DevicePermission.Manage, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (!TryReadIfMatch(http, out var expectedVersion))
        {
            return Problems.PreconditionRequired();
        }

        if (device!.Version != expectedVersion)
        {
            return Problems.PreconditionFailed();
        }

        device.Name = request.Name.Trim();
        device.Version++;
        changes.Audit(AuditActions.Updated, device.Id, userId);
        changes.Publish(EventTypes.MetadataChanged, device.Id, new { deviceId = device.Id, name = device.Name });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request passed the same check first; the database version guard decides.
            return Problems.PreconditionFailed();
        }

        SetETag(http, device);
        return TypedResults.Ok(ToResponse(device, member!));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Remove(
        Guid deviceId,
        HttpContext http,
        DeviceDb db,
        ChangeRecorder changes,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = http.User.UserId();
        var (device, _, problem) = await FindAsync(db, deviceId, userId, DevicePermission.Manage, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var now = time.GetUtcNow();
        device!.RemovedAt = now;
        device.Version++;
        await RevokeCredentialsAsync(db, device.Id, now, cancellationToken);
        changes.Audit(AuditActions.Removed, device.Id, userId);
        changes.Publish(EventTypes.Removed, device.Id, new { deviceId = device.Id });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problems.NotFound();
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<DeviceCredentialResponse>, ProblemHttpResult>> RotateCredential(
        Guid deviceId,
        HttpContext http,
        DeviceDb db,
        ChangeRecorder changes,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var userId = http.User.UserId();
        var (device, member, problem) = await FindAsync(db, deviceId, userId, DevicePermission.Manage, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var now = time.GetUtcNow();
        await RevokeCredentialsAsync(db, deviceId, now, cancellationToken);
        var secret = AddCredential(db, deviceId, now);
        changes.Audit(AuditActions.CredentialRotated, deviceId, userId);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new DeviceCredentialResponse(ToResponse(device!, member!), secret));
    }

    /// <summary>
    /// Resolves a device through the caller's membership. No membership yields 404; a
    /// membership below <paramref name="required"/> yields 403, since that caller already
    /// knows the device exists.
    /// </summary>
    private static async Task<(DeviceRecord? Device, DeviceMember? Member, ProblemHttpResult? Problem)> FindAsync(
        DeviceDb db, Guid deviceId, Guid userId, DevicePermission required, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking()
            .SingleOrDefaultAsync(m => m.DeviceId == deviceId && m.UserId == userId, cancellationToken);
        var device = member is null
            ? null
            : await db.Devices.SingleOrDefaultAsync(d => d.Id == deviceId && d.RemovedAt == null, cancellationToken);

        if (member is null || device is null)
        {
            return (null, null, Problems.NotFound());
        }

        return member.Permission < required ? (null, null, Problems.Forbidden()) : (device, member, null);
    }

    private static string AddCredential(DeviceDb db, Guid deviceId, DateTimeOffset now)
    {
        // 256 random bits; only the digest is stored, so a database leak yields no usable credential.
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.Credentials.Add(new DeviceCredential
        {
            Id = Guid.CreateVersion7(),
            DeviceId = deviceId,
            SecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret)),
            CreatedAt = now,
        });
        return secret;
    }

    private static async Task RevokeCredentialsAsync(DeviceDb db, Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var credential in await db.Credentials.Where(c => c.DeviceId == deviceId && c.RevokedAt == null).ToListAsync(cancellationToken))
        {
            credential.RevokedAt = now;
        }
    }

    private static void SetETag(HttpContext http, DeviceRecord device) =>
        http.Response.Headers.ETag = $"\"{device.Version.ToString(CultureInfo.InvariantCulture)}\"";

    private static bool TryReadIfMatch(HttpContext http, out int version) =>
        int.TryParse(http.Request.Headers.IfMatch.ToString().Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out version);

    private static DeviceResponse ToResponse(DeviceRecord device, DeviceMember member) => new(
        device.Id, device.Name, device.Kind, member.Permission.ToString().ToLowerInvariant(), member.IsOwner, device.CreatedAt, device.Version);
}
