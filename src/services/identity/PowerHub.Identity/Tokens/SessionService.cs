using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PowerHub.Identity.Data;

namespace PowerHub.Identity.Tokens;

public enum RotationOutcome
{
    Rotated,
    Invalid,
    Reused,
}

public sealed record Rotation(RotationOutcome Outcome, Guid UserId = default, Guid FamilyId = default, string? Token = null);

/// <summary>
/// Refresh sessions and one-time proofs. Secrets are 256-bit random values; only their
/// SHA-256 digest is stored (NFR-SEC-006, NFR-SEC-008). A fast hash is appropriate here
/// because the input is high-entropy, unlike a password.
/// </summary>
public sealed class SessionService(IdentityDb db, TimeProvider time, IOptions<SessionOptions> options)
{
    public async Task<(string Token, Guid FamilyId)> StartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var familyId = Guid.CreateVersion7();
        var token = AddSession(userId, familyId);
        await db.SaveChangesAsync(cancellationToken);
        return (token, familyId);
    }

    public async Task<Rotation> RotateAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var now = time.GetUtcNow();
        var session = await db.RefreshSessions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now)
        {
            return new Rotation(RotationOutcome.Invalid);
        }

        // Conditional update makes consumption atomic across replicas: exactly one caller wins.
        var claimed = await db.RefreshSessions
            .Where(s => s.Id == session.Id && s.ConsumedAt == null && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, now), cancellationToken);

        if (claimed == 0)
        {
            // A consumed token was presented again: assume theft and end the whole chain.
            await RevokeFamilyAsync(session.FamilyId, cancellationToken);
            return new Rotation(RotationOutcome.Reused, session.UserId, session.FamilyId);
        }

        var next = AddSession(session.UserId, session.FamilyId);
        await db.SaveChangesAsync(cancellationToken);
        return new Rotation(RotationOutcome.Rotated, session.UserId, session.FamilyId, next);
    }

    public async Task<RefreshSession?> FindAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        return await db.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);
    }

    public Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return db.RefreshSessions
            .Where(s => s.FamilyId == familyId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }

    public Task<int> RevokeAllAsync(Guid userId, Guid? exceptFamilyId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return db.RefreshSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && (exceptFamilyId == null || s.FamilyId != exceptFamilyId))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }

    /// <summary>Issues a proof, replacing any outstanding proof of the same purpose.</summary>
    public async Task<string> CreateOneTimeTokenAsync(
        Guid userId, OneTimeTokenPurpose purpose, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        await db.OneTimeTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null)
            .ExecuteDeleteAsync(cancellationToken);

        var token = NewSecret();
        var now = time.GetUtcNow();
        db.OneTimeTokens.Add(new OneTimeToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Purpose = purpose,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime),
        });
        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<OneTimeToken?> FindOneTimeTokenAsync(
        string token, OneTimeTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var now = time.GetUtcNow();
        return await db.OneTimeTokens.AsNoTracking().SingleOrDefaultAsync(
            t => t.TokenHash == hash && t.Purpose == purpose && t.ConsumedAt == null && t.ExpiresAt > now,
            cancellationToken);
    }

    /// <summary>Returns false when another request already consumed the proof.</summary>
    public async Task<bool> ConsumeOneTimeTokenAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var claimed = await db.OneTimeTokens
            .Where(t => t.Id == tokenId && t.ConsumedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.ConsumedAt, now), cancellationToken);
        return claimed == 1;
    }

    private string AddSession(Guid userId, Guid familyId)
    {
        var token = NewSecret();
        var now = time.GetUtcNow();
        db.RefreshSessions.Add(new RefreshSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddDays(options.Value.RefreshTokenDays),
        });
        return token;
    }

    private static string NewSecret() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
