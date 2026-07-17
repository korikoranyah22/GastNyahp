using GastNyahp.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Access;

public class FamilyProjection : GastNyahpProjection
{
    const string Prefix = "family";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public FamilyProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<FamilyEvents.V1.FamilyCreated>(ctx => new ValueTask(HandleCreated(ctx.Message, ctx.CancellationToken)));
        On<FamilyEvents.V1.FamilyMemberJoined>(ctx => new ValueTask(HandleMemberJoined(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<FamilyEvents.V1.FamilyInviteIssued>(ctx => new ValueTask(HandleInviteIssued(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<FamilyEvents.V1.FamilyInviteRedeemed>(ctx => new ValueTask(HandleInviteStatus(ctx.Message.InviteId, redeemed: true, revoked: false, ctx.CancellationToken)));
        On<FamilyEvents.V1.FamilyInviteRevoked>(ctx => new ValueTask(HandleInviteStatus(ctx.Message.InviteId, redeemed: false, revoked: true, ctx.CancellationToken)));
        On<FamilyEvents.V1.FamilyAgentKeyIssued>(ctx => new ValueTask(HandleAgentKeyIssued(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<FamilyEvents.V1.FamilyAgentKeyRevoked>(ctx => new ValueTask(HandleAgentKeyRevoked(ctx.Message.KeyId, ctx.CancellationToken)));
        // Cuentas y login (docs/DISENO_CUENTAS_LOGIN.md)
        On<FamilyEvents.V1.MemberCredentialsSet>(ctx => new ValueTask(HandleCredentialsSet(ctx.Message, ctx.CancellationToken)));
        On<FamilyEvents.V1.MemberPasswordChanged>(ctx => new ValueTask(HandlePasswordChanged(ctx.Message, ctx.CancellationToken)));
        On<FamilyEvents.V1.MemberSessionIssued>(ctx => new ValueTask(HandleSessionIssued(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<FamilyEvents.V1.MemberSessionRevoked>(ctx => new ValueTask(HandleSessionRevoked(ctx.Message.SessionId, ctx.CancellationToken)));
        On<FamilyEvents.V1.PasswordResetIssued>(ctx => new ValueTask(HandleResetIssued(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<FamilyEvents.V1.PasswordResetRedeemed>(ctx => new ValueTask(HandleResetRedeemed(ctx.Message.ResetId, ctx.CancellationToken)));
    }

    // ── Cuentas y login ──────────────────────────────────────────────────────────

    public async Task HandleCredentialsSet(FamilyEvents.V1.MemberCredentialsSet e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.MemberId == e.MemberId, ct);
        if (member is null) return;

        member.Email = e.Email;
        member.PasswordHash = e.PasswordHash;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandlePasswordChanged(FamilyEvents.V1.MemberPasswordChanged e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.MemberId == e.MemberId, ct);
        if (member is null) return;

        member.PasswordHash = e.PasswordHash;
        // Espeja el fold del aggregate: cambiar la contraseña cierra TODAS las sesiones del miembro. Tiene que
        // pasar acá también o el middleware seguiría resolviendo el token de un dispositivo que ya fue echado.
        var sesiones = await db.MemberSessions.Where(x => x.MemberId == e.MemberId && !x.Revoked).ToListAsync(ct);
        foreach (var s in sesiones) s.Revoked = true;

        await db.SaveChangesAsync(ct);
    }

    public async Task HandleSessionIssued(Guid familyId, FamilyEvents.V1.MemberSessionIssued e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.MemberSessions.AnyAsync(x => x.SessionId == e.SessionId, ct)) return;

        db.MemberSessions.Add(new MemberSessionEntity
        {
            SessionId = e.SessionId,
            FamilyId = familyId,
            MemberId = e.MemberId,
            TokenHash = e.TokenHash,
            DeviceName = e.DeviceName,
            Revoked = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleSessionRevoked(Guid sessionId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var session = await db.MemberSessions.FirstOrDefaultAsync(x => x.SessionId == sessionId, ct);
        if (session is null || session.Revoked) return;

        session.Revoked = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleResetIssued(Guid familyId, FamilyEvents.V1.PasswordResetIssued e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.PasswordResets.AnyAsync(r => r.ResetId == e.ResetId, ct)) return;

        db.PasswordResets.Add(new PasswordResetEntity
        {
            ResetId = e.ResetId,
            FamilyId = familyId,
            MemberId = e.MemberId,
            CodeHash = e.CodeHash,
            ExpiresAt = e.ExpiresAt,
            Redeemed = false,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleResetRedeemed(Guid resetId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var reset = await db.PasswordResets.FirstOrDefaultAsync(r => r.ResetId == resetId, ct);
        if (reset is null || reset.Redeemed) return;

        reset.Redeemed = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleAgentKeyIssued(Guid familyId, FamilyEvents.V1.FamilyAgentKeyIssued e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.FamilyAgentKeys.AnyAsync(k => k.KeyId == e.KeyId, ct)) return;

        db.FamilyAgentKeys.Add(new FamilyAgentKeyEntity
        {
            KeyId = e.KeyId, FamilyId = familyId, Name = e.Name, TokenHash = e.TokenHash, IssuedAt = DateTime.UtcNow,
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleAgentKeyRevoked(Guid keyId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var key = await db.FamilyAgentKeys.FirstOrDefaultAsync(k => k.KeyId == keyId, ct);
        if (key is null) return;

        key.Revoked = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleCreated(FamilyEvents.V1.FamilyCreated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.Families.AnyAsync(f => f.Id == e.FamilyId, ct)) return;

        db.Families.Add(new FamilyEntity { Id = e.FamilyId, Name = e.Name, CreatedAt = DateTime.UtcNow, IsInstanceOwner = e.IsInstanceOwner ?? true });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleMemberJoined(Guid familyId, FamilyEvents.V1.FamilyMemberJoined e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.FamilyMembers.AnyAsync(m => m.MemberId == e.MemberId, ct)) return;

        db.FamilyMembers.Add(new FamilyMemberEntity
        {
            MemberId = e.MemberId, FamilyId = familyId, Name = e.Name, Role = e.Role.ToString(),
            TokenHash = e.TokenHash, JoinedAt = DateTime.UtcNow,
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleInviteIssued(Guid familyId, FamilyEvents.V1.FamilyInviteIssued e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.FamilyInvites.AnyAsync(i => i.InviteId == e.InviteId, ct)) return;

        db.FamilyInvites.Add(new FamilyInviteEntity
        {
            InviteId = e.InviteId, FamilyId = familyId, CodeHash = e.CodeHash, ExpiresAt = e.ExpiresAt,
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleInviteStatus(Guid inviteId, bool redeemed, bool revoked, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var invite = await db.FamilyInvites.FirstOrDefaultAsync(i => i.InviteId == inviteId, ct);
        if (invite is null) return;

        if (redeemed) invite.Redeemed = true;
        if (revoked) invite.Revoked = true;
        await db.SaveChangesAsync(ct);
    }
}

public class AdminInviteProjection : GastNyahpProjection
{
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public AdminInviteProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<AdminInviteEvents.V1.AdminInviteIssued>(ctx => new ValueTask(HandleIssued(ctx.Message, ctx.CancellationToken)));
        On<AdminInviteEvents.V1.AdminInviteRedeemed>(ctx => new ValueTask(HandleRedeemed(StreamIds.GuidFrom(ctx.Stream, "admin-invite"), ctx.CancellationToken)));
    }

    public async Task HandleIssued(AdminInviteEvents.V1.AdminInviteIssued e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.AdminInvites.AnyAsync(i => i.Id == e.InviteId, ct)) return;

        db.AdminInvites.Add(new AdminInviteEntity { Id = e.InviteId, CodeHash = e.CodeHash, ExpiresAt = e.ExpiresAt, GrantsOwner = e.GrantsOwner ?? true });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleRedeemed(Guid inviteId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var invite = await db.AdminInvites.FirstOrDefaultAsync(i => i.Id == inviteId, ct);
        if (invite is null) return;

        invite.Redeemed = true;
        await db.SaveChangesAsync(ct);
    }
}
