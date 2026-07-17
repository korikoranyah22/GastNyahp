using System.Security.Cryptography;
using GastNyahp.Domain.Access;
using GastNyahp.Domain.Common;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

/// <summary>Opaque URL-safe secrets. Raw values leave the server exactly once, at issuance.</summary>
public static class TokenGenerator
{
    public static string NewMemberToken() => Generate(32);
    public static string NewInviteCode() => Generate(16);
    public static string NewAdminCode() => Generate(8);

    static string Generate(int bytes) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

public record FamilyCredential(Guid FamilyId, Guid MemberId, string MemberToken);

/// <summary>Una familia candidata cuando el mismo email+contraseña existe en varias (§3.2 del diseño).</summary>
public record FamilyChoice(Guid FamilyId, string Name);

/// <summary>
/// Resultado del login. Exactamente uno de los tres: entró, tiene que elegir familia, o falló.
/// El <see cref="Error"/> es SIEMPRE genérico — nunca dice si el email existe (amenaza #2).
/// </summary>
public record LoginResult(FamilyCredential? Credential, IReadOnlyList<FamilyChoice>? Choices, string? Error, int? RetryAfterSeconds = null);

public record MemberSessionView(Guid SessionId, string DeviceName, DateTime CreatedAt, bool Current);

/// <summary>Un código de un solo uso para crear una familia nueva, con su vencimiento. El crudo sale una vez.</summary>
public record AdminInviteIssue(string Code, string ExpiresAt);
public record FamilyInviteIssue(string InviteCode, string QrPayload, string ExpiresAt);
/// <param name="MeId">Quién de <paramref name="Members"/> es el que pregunta. Para una agent key no es ninguno (no es miembro).</param>
/// <param name="IsInstanceOwner">true = familia del dueño de la instancia; sus admins pueden invitar a crear familias nuevas.</param>
public record FamilyOverview(Guid FamilyId, string Name, IReadOnlyList<MemberOverview> Members, Guid MeId, bool IsInstanceOwner);

/// <param name="HasCredentials">
/// False = miembro de la etapa 1, todavía sin cuenta: la UI le insiste con el cartel de "creá tu cuenta" y el
/// Admin sabe a quién NO tiene sentido emitirle un reset (docs/DISENO_CUENTAS_LOGIN.md §3.3).
/// </param>
public record MemberOverview(Guid MemberId, string Name, string Role, string? Email, bool HasCredentials);
public record AgentKeyOverview(Guid KeyId, string Name, bool Revoked, DateTime IssuedAt);
public record ResolvedCredential(Guid FamilyId, Guid PrincipalId, string Role);

public class FamilyService(
    FamilyCommandService familyCommands,
    AdminInviteCommandService adminInviteCommands,
    LoginThrottle throttle,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    IConfiguration configuration,
    ILogger<FamilyService> logger)
{
    static readonly TimeSpan InviteTtl = TimeSpan.FromHours(48);

    // ── Admin gate ─────────────────────────────────────────────────────────────

    /// <summary>Called by the admin endpoint (X-Admin-Key protected). Returns the raw code once. Grants OWNER.</summary>
    public async Task<(OpResult Result, string? Code)> IssueAdminInviteAsync(CancellationToken ct = default)
    {
        var inviteId = Guid.NewGuid();
        var code = TokenGenerator.NewAdminCode();
        var result = await CommandExecutor.Exec(
            adminInviteCommands.Handle(new IssueAdminInvite(inviteId, SecretHash.Compute(code), GrantsOwner: true), ct),
            sync, logger, "IssueAdminInvite", inviteId, ct);
        return (result, result.Ok ? code : null);
    }

    /// <summary>
    /// Un admin de una familia DEL DUEÑO emite un código de un solo uso (TTL 48h) para que otra persona cree una
    /// familia NUEVA — que nace "invitada" (GrantsOwner=false), así no puede a su vez invitar más. El operador de
    /// la instancia delega la creación de familias sin repartir su Admin:ApiKey. El código crudo sale una sola vez.
    /// </summary>
    public async Task<(OpResult Result, AdminInviteIssue? Invite)> IssueFamilyCreationInviteAsync(
        Guid familyId, Guid memberId, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var family = await db.Families.FirstOrDefaultAsync(f => f.Id == familyId, ct);
            if (family is null || !family.IsInstanceOwner)
                return (OpResult.Fail("Solo el dueño de la instancia puede invitar a crear familias nuevas."), null);
            var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.MemberId == memberId, ct);
            if (member is null || member.Role != nameof(MemberRole.Admin))
                return (OpResult.Fail("Solo un administrador puede emitir estos enlaces."), null);
        }

        var inviteId = Guid.NewGuid();
        var code = TokenGenerator.NewAdminCode();
        var expiresAt = DateTime.UtcNow.Add(InviteTtl).ToString("O");
        var result = await CommandExecutor.Exec(
            adminInviteCommands.Handle(new IssueAdminInvite(inviteId, SecretHash.Compute(code), expiresAt, GrantsOwner: false), ct),
            sync, logger, "IssueAdminInvite", inviteId, ct);
        return result.Ok ? (result, new AdminInviteIssue(code, expiresAt)) : (result, null);
    }

    // ── Family lifecycle ───────────────────────────────────────────────────────

    /// <summary>The only way to create a family: a valid, unredeemed admin code (DOMAIN_MODEL.md §17.1).</summary>
    public async Task<(OpResult Result, FamilyCredential? Credential)> CreateFamilyAsync(
        string adminInviteCode, string familyName, string founderName, CancellationToken ct = default)
    {
        Guid adminInviteId;
        bool grantsOwner;   // ¿la familia que se crea será "del dueño" (puede invitar más) o "invitada"?

        // Atajo self-hosted (opt-in con Admin:AllowKeyAsCode=true): el operador escribe su propia llave de
        // instancia (Admin:ApiKey) como "código de administrador", sin pasar por POST /api/admin/invites. Emitimos
        // al vuelo una invitación descartable (código random) para que el flujo de abajo la cree y la redima igual
        // — así NO se rompe el invariante "toda familia nace de una invitación" (DOMAIN_MODEL §17.1) ni el audit
        // trail. Es reusable a propósito. OJO: con el flag activo, Admin:ApiKey pasa a ser también una contraseña
        // de "crear familia" — mantenela secreta. Por defecto (flag ausente) el comportamiento no cambia.
        var instanceKey = configuration["Admin:ApiKey"];
        var allowKeyAsCode = string.Equals(configuration["Admin:AllowKeyAsCode"], "true", StringComparison.OrdinalIgnoreCase);
        if (allowKeyAsCode && !string.IsNullOrEmpty(instanceKey) && adminInviteCode == instanceKey)
        {
            grantsOwner = true;   // el operador crea familias DEL DUEÑO
            adminInviteId = Guid.NewGuid();
            var issued = await CommandExecutor.Exec(
                adminInviteCommands.Handle(new IssueAdminInvite(adminInviteId, SecretHash.Compute(TokenGenerator.NewAdminCode()), GrantsOwner: true), ct),
                sync, logger, "IssueAdminInvite", adminInviteId, ct);
            if (!issued.Ok) return (issued, null);
        }
        else
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var codeHash = SecretHash.Compute(adminInviteCode);
            var invite = await db.AdminInvites.FirstOrDefaultAsync(i => i.CodeHash == codeHash, ct);
            if (invite is null) return (OpResult.Fail("El código de administrador no es válido."), null);
            if (invite.Redeemed) return (OpResult.Fail("El código de administrador ya fue utilizado."), null);
            if (invite.ExpiresAt is not null && DateTimeOffset.TryParse(invite.ExpiresAt, out var exp) && exp < DateTimeOffset.UtcNow)
                return (OpResult.Fail("El enlace venció — pedí uno nuevo."), null);
            adminInviteId = invite.Id;
            grantsOwner = invite.GrantsOwner;
        }

        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var token = TokenGenerator.NewMemberToken();

        var created = await CommandExecutor.Exec(
            familyCommands.Handle(new CreateFamily(familyId, familyName, adminInviteId, memberId, founderName, SecretHash.Compute(token), grantsOwner), ct),
            sync, logger, "CreateFamily", familyId, ct);
        if (!created.Ok) return (created, null);

        // Single-use enforcement lives in the AdminInvite stream itself; if two creations race on the same
        // code, the second redeem fails here and we surface it (the family stays, but flagged in logs).
        var redeemed = await CommandExecutor.Exec(
            adminInviteCommands.Handle(new RedeemAdminInvite(adminInviteId, familyId), ct),
            sync, logger, "RedeemAdminInvite", adminInviteId, ct);
        if (!redeemed.Ok)
        {
            logger.LogError("Family {FamilyId} was created but admin invite {InviteId} could not be redeemed (concurrent use?)", familyId, adminInviteId);
            return (redeemed, null);
        }

        return (OpResult.Success(familyId), new FamilyCredential(familyId, memberId, token));
    }

    /// <summary>Issues a single-use invite with TTL; the QR payload is what the frontend renders as QR.</summary>
    public async Task<(OpResult Result, FamilyInviteIssue? Invite)> IssueInviteAsync(
        Guid familyId, Guid issuedByMemberId, CancellationToken ct = default)
    {
        var inviteId = Guid.NewGuid();
        var code = TokenGenerator.NewInviteCode();
        var expiresAt = DateTime.UtcNow.Add(InviteTtl).ToString("O");

        var result = await CommandExecutor.Exec(
            familyCommands.Handle(new IssueFamilyInvite(familyId, inviteId, SecretHash.Compute(code), issuedByMemberId, expiresAt), ct),
            sync, logger, "IssueFamilyInvite", inviteId, ct);
        if (!result.Ok) return (result, null);

        return (result, new FamilyInviteIssue(code, $"gastnyahp://join?code={code}", expiresAt));
    }

    /// <summary>Joining with a QR code — the invite's single-use/expiry guards live in the Family stream.</summary>
    public async Task<(OpResult Result, FamilyCredential? Credential)> JoinFamilyAsync(
        string inviteCode, string memberName, CancellationToken ct = default)
    {
        Guid familyId, inviteId;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var codeHash = SecretHash.Compute(inviteCode);
            var invite = await db.FamilyInvites.FirstOrDefaultAsync(i => i.CodeHash == codeHash, ct);
            if (invite is null) return (OpResult.Fail("La invitación no es válida."), null);
            familyId = invite.FamilyId;
            inviteId = invite.InviteId;
        }

        var memberId = Guid.NewGuid();
        var token = TokenGenerator.NewMemberToken();

        var result = await CommandExecutor.Exec(
            familyCommands.Handle(new JoinFamily(familyId, inviteId, memberId, memberName, SecretHash.Compute(token), DateTime.UtcNow.ToString("O")), ct),
            sync, logger, "JoinFamily", memberId, ct);
        if (!result.Ok) return (result, null);

        return (result, new FamilyCredential(familyId, memberId, token));
    }

    // ── Agent keys — what MCP clients use as bearer credential (§17) ──────────

    public async Task<(OpResult Result, string? Token)> IssueAgentKeyAsync(
        Guid familyId, Guid issuedByMemberId, string name, CancellationToken ct = default)
    {
        var keyId = Guid.NewGuid();
        var token = TokenGenerator.NewMemberToken();
        var result = await CommandExecutor.Exec(
            familyCommands.Handle(new IssueFamilyAgentKey(familyId, keyId, name, SecretHash.Compute(token), issuedByMemberId), ct),
            sync, logger, "IssueFamilyAgentKey", keyId, ct);
        return (result, result.Ok ? token : null);
    }

    public Task<OpResult> RevokeAgentKeyAsync(Guid familyId, Guid keyId, Guid byMemberId, CancellationToken ct = default) =>
        CommandExecutor.Exec(
            familyCommands.Handle(new RevokeFamilyAgentKey(familyId, keyId, byMemberId), ct),
            sync, logger, "RevokeFamilyAgentKey", keyId, ct);

    public async Task<List<AgentKeyOverview>> ListAgentKeysAsync(Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.FamilyAgentKeys
            .Where(k => k.FamilyId == familyId)
            .OrderBy(k => k.IssuedAt)
            .Select(k => new AgentKeyOverview(k.KeyId, k.Name, k.Revoked, k.IssuedAt))
            .ToListAsync(ct);
    }

    // ── Auth + queries ─────────────────────────────────────────────────────────

    /// <summary>The auth middleware's lookup: raw bearer token → member OR active agent key, or null.
    /// One credential model for humans and agents — possession is identity, revocation is per-credential.</summary>
    public async Task<ResolvedCredential?> ResolveCredentialAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = SecretHash.Compute(rawToken);
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.TokenHash == hash, ct);
        if (member is not null)
            return new ResolvedCredential(member.FamilyId, member.MemberId, member.Role);

        // Token de sesión (docs/DISENO_CUENTAS_LOGIN.md): lo que devuelve el login. Resuelve al MISMO miembro con
        // su MISMO rol — una sesión no es un principal aparte, es otra llave de la misma puerta.
        var session = await db.MemberSessions.FirstOrDefaultAsync(x => x.TokenHash == hash && !x.Revoked, ct);
        if (session is not null)
        {
            var owner = await db.FamilyMembers.FirstOrDefaultAsync(m => m.MemberId == session.MemberId, ct);
            if (owner is not null)
                return new ResolvedCredential(owner.FamilyId, owner.MemberId, owner.Role);
        }

        var agentKey = await db.FamilyAgentKeys.FirstOrDefaultAsync(k => k.TokenHash == hash && !k.Revoked, ct);
        if (agentKey is not null)
            return new ResolvedCredential(agentKey.FamilyId, agentKey.KeyId, "Agent");

        return null;
    }

    // ── Cuentas y login (docs/DISENO_CUENTAS_LOGIN.md) ─────────────────────────

    /// <summary>Un solo mensaje para "no existe" y para "contraseña mala" (amenaza #2: enumeración de usuarios).</summary>
    const string ErrorGenerico = "Email o contraseña incorrectos.";

    /// <summary>
    /// Login por email+contraseña. Con unicidad POR FAMILIA el email puede estar en varias, así que se prueban
    /// todas y se resuelve según cuántas dan match (§3.2 del diseño).
    /// </summary>
    public async Task<LoginResult> LoginAsync(
        string email, string password, Guid? familyId, string? deviceName, string clientIp, CancellationToken ct = default)
    {
        var normalizado = (email ?? "").Trim().ToLowerInvariant();

        // El rate-limit va PRIMERO: si no, cada intento nos cuesta un PBKDF2 de ~100ms (amenaza #7).
        var bloqueado = throttle.SegundosBloqueado(normalizado, clientIp);
        if (bloqueado is not null)
            return new LoginResult(null, null, "Demasiados intentos. Probá de nuevo en un rato.", bloqueado);

        if (string.IsNullOrWhiteSpace(normalizado) || string.IsNullOrEmpty(password))
        {
            PasswordHash.VerifyDummy(password ?? "");
            throttle.RegistrarFallo(normalizado, clientIp);
            return new LoginResult(null, null, ErrorGenerico);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var candidatos = await db.FamilyMembers
            .Where(m => m.Email == normalizado && m.PasswordHash != null)
            .Where(m => familyId == null || m.FamilyId == familyId)
            .ToListAsync(ct);

        if (candidatos.Count == 0)
        {
            // Corremos un PBKDF2 igual: si contestáramos al toque, el tiempo de respuesta delataría qué emails
            // están registrados aunque el mensaje sea idéntico (amenaza #3).
            PasswordHash.VerifyDummy(password);
            throttle.RegistrarFallo(normalizado, clientIp);
            return new LoginResult(null, null, ErrorGenerico);
        }

        var coinciden = candidatos.Where(m => PasswordHash.Verify(password, m.PasswordHash!)).ToList();

        if (coinciden.Count == 0)
        {
            throttle.RegistrarFallo(normalizado, clientIp);
            return new LoginResult(null, null, ErrorGenerico);
        }

        if (coinciden.Count > 1)
        {
            // Mismo email y misma contraseña en dos familias: no adivinamos a cuál entrar. NO cuenta como fallo:
            // el usuario acertó la contraseña.
            var familias = await db.Families
                .Where(f => coinciden.Select(m => m.FamilyId).Contains(f.Id))
                .Select(f => new FamilyChoice(f.Id, f.Name))
                .ToListAsync(ct);
            return new LoginResult(null, familias, null);
        }

        var member = coinciden[0];
        throttle.RegistrarExito(normalizado, clientIp);

        var (result, token) = await IssueSessionAsync(member.FamilyId, member.MemberId, deviceName, ct);
        return result.Ok
            ? new LoginResult(new FamilyCredential(member.FamilyId, member.MemberId, token!), null, null)
            : new LoginResult(null, null, result.Error);
    }

    /// <summary>Emite un token de sesión nuevo. El crudo sale una sola vez, igual que todo en §17.</summary>
    public async Task<(OpResult Result, string? Token)> IssueSessionAsync(
        Guid familyId, Guid memberId, string? deviceName, CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid();
        var token = TokenGenerator.NewMemberToken();
        var result = await CommandExecutor.Exec(
            familyCommands.Handle(new IssueMemberSession(familyId, memberId, sessionId, SecretHash.Compute(token), deviceName), ct),
            sync, logger, "IssueMemberSession", sessionId, ct);
        return (result, result.Ok ? token : null);
    }

    /// <summary>
    /// Setea email+contraseña por primera vez. En la etapa 1 el miembro se autentica con su token de posesión:
    /// así crea su cuenta sin que nadie quede afuera (§3.3).
    /// </summary>
    public async Task<OpResult> SetCredentialsAsync(
        Guid familyId, Guid memberId, string email, string password, CancellationToken ct = default)
    {
        var motivo = PasswordPolicy.Validate(password);
        if (motivo is not null) return OpResult.Fail(motivo);

        return await CommandExecutor.Exec(
            familyCommands.Handle(new SetMemberCredentials(familyId, memberId, email, PasswordHash.Compute(password)), ct),
            sync, logger, "SetMemberCredentials", memberId, ct);
    }

    /// <summary>Cambia la contraseña. Exige la actual: si te dejan el dispositivo abierto, que no te la puedan cambiar.</summary>
    public async Task<OpResult> ChangePasswordAsync(
        Guid familyId, Guid memberId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var motivo = PasswordPolicy.Validate(newPassword);
        if (motivo is not null) return OpResult.Fail(motivo);

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var member = await db.FamilyMembers.FirstOrDefaultAsync(m => m.MemberId == memberId && m.FamilyId == familyId, ct);
            if (member?.PasswordHash is null) return OpResult.Fail("Todavía no tenés una cuenta creada.");
            if (!PasswordHash.Verify(currentPassword ?? "", member.PasswordHash))
                return OpResult.Fail("La contraseña actual no es correcta.");
        }

        // El fold de MemberPasswordChanged cierra TODAS las sesiones del miembro, incluida la de este request:
        // es a propósito (amenaza #6). El cliente vuelve a loguearse con la contraseña nueva.
        return await CommandExecutor.Exec(
            familyCommands.Handle(new ChangeMemberPassword(familyId, memberId, PasswordHash.Compute(newPassword)), ct),
            sync, logger, "ChangeMemberPassword", memberId, ct);
    }

    public async Task<List<MemberSessionView>> ListSessionsAsync(
        Guid memberId, string? currentRawToken, CancellationToken ct = default)
    {
        var currentHash = currentRawToken is null ? null : SecretHash.Compute(currentRawToken);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.MemberSessions
            .Where(x => x.MemberId == memberId && !x.Revoked)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MemberSessionView(x.SessionId, x.DeviceName, x.CreatedAt, x.TokenHash == currentHash))
            .ToListAsync(ct);
    }

    public Task<OpResult> RevokeSessionAsync(Guid familyId, Guid sessionId, Guid byMemberId, CancellationToken ct = default) =>
        CommandExecutor.Exec(
            familyCommands.Handle(new RevokeMemberSession(familyId, sessionId, byMemberId), ct),
            sync, logger, "RevokeMemberSession", sessionId, ct);

    /// <param name="issuedByMemberId">Admin de la familia; null = flujo de instancia con X-Admin-Key (§3.1).</param>
    public async Task<(OpResult Result, string? Code)> IssuePasswordResetAsync(
        Guid familyId, Guid memberId, Guid? issuedByMemberId, CancellationToken ct = default)
    {
        var resetId = Guid.NewGuid();
        var code = TokenGenerator.NewInviteCode();
        var expira = DateTime.UtcNow.Add(InviteTtl).ToString("O");   // mismo TTL que las invitaciones
        var result = await CommandExecutor.Exec(
            familyCommands.Handle(new IssuePasswordReset(familyId, memberId, resetId, SecretHash.Compute(code), issuedByMemberId, expira), ct),
            sync, logger, "IssuePasswordReset", resetId, ct);
        return (result, result.Ok ? code : null);
    }

    /// <summary>Canjea un código de reseteo. Anónimo: el código ES la autorización, como las invitaciones.</summary>
    public async Task<OpResult> RedeemPasswordResetAsync(string code, string newPassword, CancellationToken ct = default)
    {
        var motivo = PasswordPolicy.Validate(newPassword);
        if (motivo is not null) return OpResult.Fail(motivo);
        if (string.IsNullOrWhiteSpace(code)) return OpResult.Fail("El código es obligatorio.");

        var hash = SecretHash.Compute(code.Trim());
        PasswordResetEntity? reset;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
            reset = await db.PasswordResets.FirstOrDefaultAsync(r => r.CodeHash == hash && !r.Redeemed, ct);

        // Mensaje genérico: un código inválido y uno ya usado se ven igual desde afuera.
        if (reset is null) return OpResult.Fail("El código no es válido o ya fue utilizado.");

        return await CommandExecutor.Exec(
            familyCommands.Handle(new RedeemPasswordReset(reset.FamilyId, reset.ResetId, PasswordHash.Compute(newPassword), DateTime.UtcNow.ToString("O")), ct),
            sync, logger, "RedeemPasswordReset", reset.ResetId, ct);
    }

    /// <summary>Resuelve familia+miembro por email, para el reseteo de instancia (§3.1), que no tiene sesión.</summary>
    public async Task<(Guid FamilyId, Guid MemberId)?> FindMemberByEmailAsync(
        Guid familyId, string email, CancellationToken ct = default)
    {
        var normalizado = (email ?? "").Trim().ToLowerInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var m = await db.FamilyMembers.FirstOrDefaultAsync(x => x.FamilyId == familyId && x.Email == normalizado, ct);
        return m is null ? null : (m.FamilyId, m.MemberId);
    }

    public async Task<FamilyOverview?> GetOverviewAsync(Guid familyId, Guid meId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var family = await db.Families.FirstOrDefaultAsync(f => f.Id == familyId, ct);
        if (family is null) return null;

        var members = await db.FamilyMembers
            .Where(m => m.FamilyId == familyId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new MemberOverview(m.MemberId, m.Name, m.Role, m.Email, m.PasswordHash != null))
            .ToListAsync(ct);
        return new FamilyOverview(family.Id, family.Name, members, meId, family.IsInstanceOwner);
    }
}
