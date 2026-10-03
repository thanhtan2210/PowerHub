using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PowerHub.Device.Data;
using PowerHub.Device.Features;

namespace PowerHub.Device.Tests;

public sealed class DeviceTests(DeviceApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registering_makes_the_caller_owner_and_returns_the_credential_once()
    {
        var userId = Guid.NewGuid();
        using var client = app.ClientFor(userId);

        var response = await client.PostAsJsonAsync("/api/v1/devices", new { name = "  Living room plug ", kind = "smart-plug" }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<DeviceCredentialResponse>(Ct);
        Assert.Equal("Living room plug", created!.Device.Name);
        Assert.Equal("manage", created.Device.Permission);
        Assert.True(created.Device.IsOwner);
        Assert.Equal($"/api/v1/devices/{created.Device.Id}", response.Headers.Location!.ToString());
        Assert.Equal("\"1\"", response.Headers.ETag!.Tag);

        // Reading the device later never discloses the credential again.
        var read = await client.GetStringAsync($"/api/v1/devices/{created.Device.Id}", Ct);
        Assert.DoesNotContain(created.Credential, read, StringComparison.Ordinal);
        Assert.DoesNotContain("credential", read, StringComparison.OrdinalIgnoreCase);

        // Only a digest is stored.
        var stored = await app.WithDbAsync(db => db.Credentials.SingleAsync(c => c.DeviceId == created.Device.Id, Ct));
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(created.Credential)), stored.SecretHash);
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    public async Task A_user_cannot_see_or_change_another_users_device()
    {
        using var owner = app.ClientFor(Guid.NewGuid());
        using var stranger = app.ClientFor(Guid.NewGuid());
        var device = await RegisterAsync(owner, "Owner's heater");
        await RegisterAsync(stranger, "Stranger's lamp");

        var path = $"/api/v1/devices/{device.Id}";
        var read = await stranger.GetAsync(path, Ct);
        var update = await SendAsync(stranger, HttpMethod.Patch, path, new { name = "Hijacked" }, ifMatch: "\"1\"");
        var remove = await stranger.DeleteAsync(path, Ct);
        var rotate = await stranger.PostAsync($"{path}/credentials", null, Ct);
        var unknown = await stranger.GetAsync($"/api/v1/devices/{Guid.NewGuid()}", Ct);

        // 404, not 403: the response is identical to that for a device that does not exist.
        Assert.All([read, update, remove, rotate], response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.Equal(
            (await read.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString(),
            (await unknown.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString());

        var list = await stranger.GetFromJsonAsync<DevicePage>("/api/v1/devices", Ct);
        Assert.Equal(["Stranger's lamp"], list!.Items.Select(item => item.Name));

        // Nothing changed for the owner.
        var unchanged = await owner.GetFromJsonAsync<DeviceResponse>(path, Ct);
        Assert.Equal("Owner's heater", unchanged!.Name);
        Assert.Equal(1, unchanged.Version);
    }

    [Fact]
    public async Task Requests_without_a_valid_identity_token_are_rejected()
    {
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/devices", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/devices", new { name = "x", kind = "y" }, Ct)).StatusCode);

        using var attackerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var user = Guid.NewGuid();
        var rejected = new Dictionary<string, string>
        {
            ["signed by another key"] = DeviceApp.Token(user, attackerKey),
            ["wrong issuer"] = app.TokenWith(user, issuer: "https://evil.test"),
            ["wrong audience"] = app.TokenWith(user, audience: "another-api"),
            ["expired"] = app.TokenWith(user, expires: DateTime.UtcNow.AddMinutes(-5)),
            ["unsigned"] = "eyJhbGciOiJub25lIn0." + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"sub\":\"{user}\"}}")).TrimEnd('=') + ".",
        };

        foreach (var (reason, token) in rejected)
        {
            using var client = app.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/api/v1/devices", Ct);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"Token {reason} was accepted.");
        }
    }

    [Fact]
    public async Task Update_requires_the_current_etag()
    {
        using var client = app.ClientFor(Guid.NewGuid());
        var device = await RegisterAsync(client, "Before");
        var path = $"/api/v1/devices/{device.Id}";

        var missing = await SendAsync(client, HttpMethod.Patch, path, new { name = "After" }, ifMatch: null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

        var updated = await SendAsync(client, HttpMethod.Patch, path, new { name = "After" }, ifMatch: "\"1\"");
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("\"2\"", updated.Headers.ETag!.Tag);

        // A second writer still holding version 1 must not overwrite the first change.
        var stale = await SendAsync(client, HttpMethod.Patch, path, new { name = "Lost update" }, ifMatch: "\"1\"");
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("After", (await client.GetFromJsonAsync<DeviceResponse>(path, Ct))!.Name);
    }

    [Fact]
    public async Task Concurrent_updates_with_the_same_etag_apply_exactly_once()
    {
        using var client = app.ClientFor(Guid.NewGuid());
        var device = await RegisterAsync(client, "Contended");
        var path = $"/api/v1/devices/{device.Id}";

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            SendAsync(client, HttpMethod.Patch, path, new { name = $"Writer {index}" }, ifMatch: "\"1\"")));

        Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(results.Where(response => response.StatusCode != HttpStatusCode.OK),
            response => Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode));
        Assert.Equal(2, (await client.GetFromJsonAsync<DeviceResponse>(path, Ct))!.Version);
        Assert.Equal(1, await app.WithDbAsync(db => db.Outbox.CountAsync(
            o => o.EventType == EventTypes.MetadataChanged && o.Subject == $"device/{device.Id}", Ct)));
    }

    [Fact]
    public async Task Removing_hides_the_device_and_revokes_its_credential()
    {
        using var client = app.ClientFor(Guid.NewGuid());
        var device = await RegisterAsync(client, "Old sensor");
        var path = $"/api/v1/devices/{device.Id}";

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(path, Ct)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<DevicePage>("/api/v1/devices", Ct))!.Items);

        // History is retained; the credential can no longer be used.
        Assert.NotNull(await app.WithDbAsync(db => db.Devices.Where(d => d.Id == device.Id).Select(d => d.RemovedAt).SingleAsync(Ct)));
        Assert.NotNull(await app.WithDbAsync(db => db.Credentials.Where(c => c.DeviceId == device.Id).Select(c => c.RevokedAt).SingleAsync(Ct)));
    }

    [Fact]
    public async Task Rotating_issues_a_new_credential_and_revokes_the_old_one()
    {
        using var client = app.ClientFor(Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/devices", new { name = "Meter", kind = "meter" }, Ct);
        var original = (await response.Content.ReadFromJsonAsync<DeviceCredentialResponse>(Ct))!;

        var rotated = await client.PostAsync($"/api/v1/devices/{original.Device.Id}/credentials", null, Ct);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var replacement = (await rotated.Content.ReadFromJsonAsync<DeviceCredentialResponse>(Ct))!.Credential;
        Assert.NotEqual(original.Credential, replacement);

        var stored = await app.WithDbAsync(db => db.Credentials.Where(c => c.DeviceId == original.Device.Id).ToListAsync(Ct));
        Assert.Equal(2, stored.Count);
        var active = Assert.Single(stored, credential => credential.RevokedAt is null);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(replacement)), active.SecretHash);
    }

    [Fact]
    public async Task Each_change_records_one_audit_entry_and_one_outbox_event()
    {
        var userId = Guid.NewGuid();
        using var client = app.ClientFor(userId);
        var device = await RegisterAsync(client, "Audited");
        var path = $"/api/v1/devices/{device.Id}";
        await SendAsync(client, HttpMethod.Patch, path, new { name = "Renamed" }, ifMatch: "\"1\"");
        await client.DeleteAsync(path, Ct);

        var audits = await app.WithDbAsync(db => db.AuditEvents.Where(a => a.TargetId == device.Id.ToString()).OrderBy(a => a.Id).ToListAsync(Ct));
        Assert.Equal([AuditActions.Registered, AuditActions.Updated, AuditActions.Removed], audits.Select(a => a.Action));
        Assert.All(audits, audit =>
        {
            Assert.Equal(userId, audit.ActorId);
            Assert.Equal("success", audit.Result);
            Assert.False(string.IsNullOrEmpty(audit.CorrelationId));
        });

        var events = await app.WithDbAsync(db => db.Outbox.Where(o => o.Subject == $"device/{device.Id}").OrderBy(o => o.Id).ToListAsync(Ct));
        Assert.Equal([EventTypes.Registered, EventTypes.MetadataChanged, EventTypes.Removed], events.Select(e => e.EventType));
        Assert.All(events, message => Assert.Null(message.PublishedAt));

        var envelope = JsonDocument.Parse(events[0].Payload).RootElement;
        Assert.Equal(events[0].Id, envelope.GetProperty("eventId").GetGuid());
        Assert.Equal("device-service", envelope.GetProperty("producer").GetString());
        Assert.Equal($"device/{device.Id}", envelope.GetProperty("subject").GetString());
        Assert.Equal(userId, envelope.GetProperty("data").GetProperty("ownerUserId").GetGuid());
    }

    [Fact]
    public async Task A_rejected_request_leaves_no_audit_or_outbox_record()
    {
        using var client = app.ClientFor(Guid.NewGuid());
        var before = await app.WithDbAsync(async db => (await db.AuditEvents.CountAsync(Ct), await db.Outbox.CountAsync(Ct)));

        var tooLong = await client.PostAsJsonAsync("/api/v1/devices", new { name = new string('x', 101), kind = "plug" }, Ct);
        var missing = await client.PostAsJsonAsync("/api/v1/devices", new { kind = "plug" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<DevicePage>("/api/v1/devices", Ct))!.Items);
        Assert.Equal(before, await app.WithDbAsync(async db => (await db.AuditEvents.CountAsync(Ct), await db.Outbox.CountAsync(Ct))));
    }

    [Fact]
    public async Task Listing_pages_through_only_the_callers_devices()
    {
        using var client = app.ClientFor(Guid.NewGuid());
        for (var index = 0; index < 3; index++)
        {
            await RegisterAsync(client, $"Device {index}");
        }

        var first = await client.GetFromJsonAsync<DevicePage>("/api/v1/devices?limit=2", Ct);
        Assert.Equal(2, first!.Items.Count);
        Assert.NotNull(first.NextCursor);

        var second = await client.GetFromJsonAsync<DevicePage>($"/api/v1/devices?limit=2&cursor={first.NextCursor}", Ct);
        Assert.Single(second!.Items);
        Assert.Null(second.NextCursor);
        Assert.Equal(["Device 0", "Device 1", "Device 2"], first.Items.Concat(second.Items).Select(item => item.Name));
    }

    [Fact]
    public async Task A_member_with_view_permission_can_read_but_not_manage()
    {
        using var owner = app.ClientFor(Guid.NewGuid());
        var viewerId = Guid.NewGuid();
        using var viewer = app.ClientFor(viewerId);
        var device = await RegisterAsync(owner, "Shared thermostat");
        // Sharing has no API yet; the membership model is exercised directly.
        await app.WithDbAsync(async db =>
        {
            db.Members.Add(new DeviceMember { DeviceId = device.Id, UserId = viewerId, Permission = DevicePermission.View, CreatedAt = DateTimeOffset.UtcNow });
            return await db.SaveChangesAsync(Ct);
        });
        var path = $"/api/v1/devices/{device.Id}";

        var read = await viewer.GetFromJsonAsync<DeviceResponse>(path, Ct);
        Assert.Equal("view", read!.Permission);
        Assert.False(read.IsOwner);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(viewer, HttpMethod.Patch, path, new { name = "Nope" }, ifMatch: "\"1\"")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.DeleteAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"{path}/credentials", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Health_endpoints_need_no_token()
    {
        using var client = app.CreateClient();
        Assert.Equal("Healthy", await client.GetStringAsync("/health/live", Ct));
        Assert.Equal("Healthy", await client.GetStringAsync("/health/ready", Ct));
    }

    private static async Task<DeviceResponse> RegisterAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/devices", new { name, kind = "smart-plug" }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceCredentialResponse>(Ct))!.Device;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object body, string? ifMatch)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return client.SendAsync(request, Ct);
    }
}
