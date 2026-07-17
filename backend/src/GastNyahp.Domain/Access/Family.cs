using Eventuous;

namespace GastNyahp.Domain.Access;

public enum MemberRole { Admin, Member }

/// <param name="Email">
/// Credencial de login (docs/DISENO_CUENTAS_LOGIN.md). NULL = miembro de la etapa 1, que todavía entra solo con
/// su token de posesión. Único DENTRO de la familia: como los miembros viven en este State, es un invariante de
/// aggregate puro — se garantiza acá, sin leer el read-model y sin carreras.
/// </param>
/// <param name="PasswordHash">Hash PBKDF2 (<see cref="GastNyahp.Domain.Common.PasswordHash"/>), NUNCA la contraseña.</param>
public readonly record struct FamilyMember(
    Guid MemberId, string Name, MemberRole Role, string TokenHash,
    string? Email = null, string? PasswordHash = null);

public readonly record struct FamilyInvite(Guid InviteId, string CodeHash, string ExpiresAt, bool Redeemed, bool Revoked);

/// <summary>A long-lived bearer credential for MCP/agents — data access only, never admin powers.</summary>
public readonly record struct FamilyAgentKey(Guid KeyId, string Name, string TokenHash, bool Revoked);

/// <summary>
/// Un dispositivo logueado. El token crudo del miembro no se guarda (solo su hash), así que el login no puede
/// devolver el token existente: emite uno nuevo. Con UN solo token por miembro, entrar en el celular deslogearía
/// la compu — por eso son un set, calcado de <see cref="FamilyAgentKey"/>.
/// </summary>
public readonly record struct MemberSession(Guid SessionId, Guid MemberId, string TokenHash, string DeviceName, bool Revoked);

/// <summary>Reseteo de contraseña de un solo uso, emitido por un Admin (no hay mail). Mismo TTL que las invitaciones.</summary>
public readonly record struct PasswordReset(Guid ResetId, Guid MemberId, string CodeHash, string ExpiresAt, bool Redeemed);

public static class FamilyEvents
{
    public static class V1
    {
        // IsInstanceOwner opcional para replay-safety: los FamilyCreated viejos (sin el campo) deserializan a null →
        // se tratan como familia DEL DUEÑO (nacieron de un código del operador). Las familias invitadas vía un enlace
        // que emite un dueño nacen con false y no pueden invitar a su vez.
        [EventType("V1.FamilyCreated")]
        public record FamilyCreated(Guid FamilyId, string Name, Guid AdminInviteId, string CreatedAt, bool? IsInstanceOwner = null);

        [EventType("V1.FamilyMemberJoined")]
        public record FamilyMemberJoined(Guid MemberId, string Name, MemberRole Role, string TokenHash, string JoinedAt, Guid? ViaInviteId);

        [EventType("V1.FamilyInviteIssued")]
        public record FamilyInviteIssued(Guid InviteId, string CodeHash, Guid IssuedByMemberId, string IssuedAt, string ExpiresAt);

        [EventType("V1.FamilyInviteRedeemed")]
        public record FamilyInviteRedeemed(Guid InviteId, Guid ByMemberId, string RedeemedAt);

        [EventType("V1.FamilyInviteRevoked")]
        public record FamilyInviteRevoked(Guid InviteId, Guid ByMemberId, string RevokedAt);

        [EventType("V1.FamilyAgentKeyIssued")]
        public record FamilyAgentKeyIssued(Guid KeyId, string Name, string TokenHash, Guid IssuedByMemberId, string IssuedAt);

        [EventType("V1.FamilyAgentKeyRevoked")]
        public record FamilyAgentKeyRevoked(Guid KeyId, Guid ByMemberId, string RevokedAt);

        // ── Cuentas y login (docs/DISENO_CUENTAS_LOGIN.md) ────────────────────────────
        // El PasswordHash llega YA hasheado desde el application service: la contraseña en texto plano no entra
        // jamás a un evento (son inmutables y para siempre), igual que hoy nunca entra un token crudo.

        [EventType("V1.MemberCredentialsSet")]
        public record MemberCredentialsSet(Guid MemberId, string Email, string PasswordHash, string SetAt);

        [EventType("V1.MemberPasswordChanged")]
        public record MemberPasswordChanged(Guid MemberId, string PasswordHash, string ChangedAt);

        [EventType("V1.MemberSessionIssued")]
        public record MemberSessionIssued(Guid SessionId, Guid MemberId, string TokenHash, string DeviceName, string IssuedAt);

        [EventType("V1.MemberSessionRevoked")]
        public record MemberSessionRevoked(Guid SessionId, string RevokedAt);

        [EventType("V1.PasswordResetIssued")]
        public record PasswordResetIssued(Guid ResetId, Guid MemberId, string CodeHash, string IssuedAt, string ExpiresAt);

        [EventType("V1.PasswordResetRedeemed")]
        public record PasswordResetRedeemed(Guid ResetId, Guid MemberId, string RedeemedAt);
    }
}

public record FamilyState : State<FamilyState>
{
    public Guid FamilyId { get; init; }
    public string Name { get; init; } = "";
    public bool IsInstanceOwner { get; init; } = true;
    public IReadOnlyList<FamilyMember> Members { get; init; } = [];
    public IReadOnlyList<FamilyInvite> Invites { get; init; } = [];
    public IReadOnlyList<FamilyAgentKey> AgentKeys { get; init; } = [];
    public IReadOnlyList<MemberSession> Sessions { get; init; } = [];
    public IReadOnlyList<PasswordReset> PasswordResets { get; init; } = [];

    public FamilyState()
    {
        On<FamilyEvents.V1.FamilyCreated>((s, e) => s with { FamilyId = e.FamilyId, Name = e.Name, IsInstanceOwner = e.IsInstanceOwner ?? true });
        On<FamilyEvents.V1.FamilyMemberJoined>((s, e) => s with
        {
            Members = [.. s.Members, new FamilyMember(e.MemberId, e.Name, e.Role, e.TokenHash)],
        });
        On<FamilyEvents.V1.FamilyInviteIssued>((s, e) => s with
        {
            Invites = [.. s.Invites, new FamilyInvite(e.InviteId, e.CodeHash, e.ExpiresAt, false, false)],
        });
        On<FamilyEvents.V1.FamilyInviteRedeemed>((s, e) => s with
        {
            Invites = s.Invites.Select(i => i.InviteId == e.InviteId ? i with { Redeemed = true } : i).ToList(),
        });
        On<FamilyEvents.V1.FamilyInviteRevoked>((s, e) => s with
        {
            Invites = s.Invites.Select(i => i.InviteId == e.InviteId ? i with { Revoked = true } : i).ToList(),
        });
        On<FamilyEvents.V1.FamilyAgentKeyIssued>((s, e) => s with
        {
            AgentKeys = [.. s.AgentKeys, new FamilyAgentKey(e.KeyId, e.Name, e.TokenHash, false)],
        });
        On<FamilyEvents.V1.FamilyAgentKeyRevoked>((s, e) => s with
        {
            AgentKeys = s.AgentKeys.Select(k => k.KeyId == e.KeyId ? k with { Revoked = true } : k).ToList(),
        });

        // ── Cuentas y login ───────────────────────────────────────────────────────────

        On<FamilyEvents.V1.MemberCredentialsSet>((s, e) => s with
        {
            Members = s.Members
                .Select(m => m.MemberId == e.MemberId ? m with { Email = e.Email, PasswordHash = e.PasswordHash } : m)
                .ToList(),
        });
        On<FamilyEvents.V1.MemberPasswordChanged>((s, e) => s with
        {
            Members = s.Members
                .Select(m => m.MemberId == e.MemberId ? m with { PasswordHash = e.PasswordHash } : m)
                .ToList(),
            // Cambiar la contraseña cierra TODAS las sesiones del miembro (amenaza #6): si alguien te robó un
            // dispositivo, cambiar la clave tiene que echarlo — si no, el atacante sigue adentro con su token.
            Sessions = s.Sessions
                .Select(x => x.MemberId == e.MemberId ? x with { Revoked = true } : x)
                .ToList(),
        });
        On<FamilyEvents.V1.MemberSessionIssued>((s, e) => s with
        {
            Sessions = [.. s.Sessions, new MemberSession(e.SessionId, e.MemberId, e.TokenHash, e.DeviceName, false)],
        });
        On<FamilyEvents.V1.MemberSessionRevoked>((s, e) => s with
        {
            Sessions = s.Sessions.Select(x => x.SessionId == e.SessionId ? x with { Revoked = true } : x).ToList(),
        });
        On<FamilyEvents.V1.PasswordResetIssued>((s, e) => s with
        {
            PasswordResets = [.. s.PasswordResets, new PasswordReset(e.ResetId, e.MemberId, e.CodeHash, e.ExpiresAt, false)],
        });
        On<FamilyEvents.V1.PasswordResetRedeemed>((s, e) => s with
        {
            PasswordResets = s.PasswordResets.Select(r => r.ResetId == e.ResetId ? r with { Redeemed = true } : r).ToList(),
        });
    }
}

public record CreateFamily(Guid FamilyId, string Name, Guid AdminInviteId, Guid FounderMemberId, string FounderName, string FounderTokenHash, bool IsInstanceOwner = true);
public record IssueFamilyInvite(Guid FamilyId, Guid InviteId, string CodeHash, Guid IssuedByMemberId, string ExpiresAt);
public record JoinFamily(Guid FamilyId, Guid InviteId, Guid MemberId, string Name, string TokenHash, string Now);
public record RevokeFamilyInvite(Guid FamilyId, Guid InviteId, Guid ByMemberId);
public record IssueFamilyAgentKey(Guid FamilyId, Guid KeyId, string Name, string TokenHash, Guid IssuedByMemberId);
public record RevokeFamilyAgentKey(Guid FamilyId, Guid KeyId, Guid ByMemberId);

// ── Cuentas y login. El PasswordHash SIEMPRE llega hasheado desde el application service. ──
public record SetMemberCredentials(Guid FamilyId, Guid MemberId, string Email, string PasswordHash);
public record ChangeMemberPassword(Guid FamilyId, Guid MemberId, string PasswordHash);
public record IssueMemberSession(Guid FamilyId, Guid MemberId, Guid SessionId, string TokenHash, string? DeviceName = null);
public record RevokeMemberSession(Guid FamilyId, Guid SessionId, Guid ByMemberId);
/// <param name="IssuedByMemberId">Admin de la familia. <c>null</c> = flujo de instancia (X-Admin-Key): la salida
/// de emergencia del §3.1 del diseño, para cuando el ÚNICO admin es justamente el que olvidó su contraseña.</param>
public record IssuePasswordReset(Guid FamilyId, Guid MemberId, Guid ResetId, string CodeHash, Guid? IssuedByMemberId, string ExpiresAt);
public record RedeemPasswordReset(Guid FamilyId, Guid ResetId, string NewPasswordHash, string Now);

/// <summary>
/// Members are access credentials (token hashes) — NOT the Person aggregate, which stays the expense
/// attribution label INSIDE a family (DOMAIN_MODEL.md §17.2). Invites are part of this consistency boundary
/// so "single use" is guaranteed by the stream, not by a read-model race.
/// </summary>
public sealed class FamilyCommandService : CommandService<FamilyState>
{
    public FamilyCommandService(IEventStore store) : base(store)
    {
        On<CreateFamily>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(Create);
        On<IssueFamilyInvite>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(IssueInvite);
        On<JoinFamily>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(Join);
        On<RevokeFamilyInvite>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(RevokeInvite);
        On<IssueFamilyAgentKey>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(IssueAgentKey);
        On<RevokeFamilyAgentKey>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(RevokeAgentKey);
        On<SetMemberCredentials>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(SetCredentials);
        On<ChangeMemberPassword>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(ChangePassword);
        On<IssueMemberSession>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(IssueSession);
        On<RevokeMemberSession>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(RevokeSession);
        On<IssuePasswordReset>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(IssueReset);
        On<RedeemPasswordReset>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.FamilyId)).Act(RedeemReset);
    }

    public static IEnumerable<object> Create(CreateFamily cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw new DomainException("CreateFamily: Name required.");
        if (string.IsNullOrWhiteSpace(cmd.FounderName)) throw new DomainException("CreateFamily: FounderName required.");
        if (string.IsNullOrWhiteSpace(cmd.FounderTokenHash)) throw new DomainException("CreateFamily: FounderTokenHash required.");

        yield return new FamilyEvents.V1.FamilyCreated(cmd.FamilyId, cmd.Name.Trim(), cmd.AdminInviteId, Now, cmd.IsInstanceOwner);
        yield return new FamilyEvents.V1.FamilyMemberJoined(cmd.FounderMemberId, cmd.FounderName.Trim(), MemberRole.Admin, cmd.FounderTokenHash, Now, null);
    }

    public static IEnumerable<object> IssueInvite(FamilyState state, object[] _, IssueFamilyInvite cmd)
    {
        var issuer = state.Members.FirstOrDefault(m => m.MemberId == cmd.IssuedByMemberId);
        if (issuer.MemberId == Guid.Empty) throw new DomainException("IssueFamilyInvite: el emisor no es miembro de la familia.");
        if (issuer.Role != MemberRole.Admin) throw new DomainException("IssueFamilyInvite: solo un administrador puede invitar.");
        if (string.IsNullOrWhiteSpace(cmd.CodeHash)) throw new DomainException("IssueFamilyInvite: CodeHash required.");

        yield return new FamilyEvents.V1.FamilyInviteIssued(cmd.InviteId, cmd.CodeHash, cmd.IssuedByMemberId, Now, cmd.ExpiresAt);
    }

    public static IEnumerable<object> Join(FamilyState state, object[] _, JoinFamily cmd)
    {
        var invite = state.Invites.FirstOrDefault(i => i.InviteId == cmd.InviteId);
        if (invite.InviteId == Guid.Empty) throw new DomainException("JoinFamily: la invitación no existe.");
        if (invite.Redeemed) throw new DomainException("JoinFamily: la invitación ya fue utilizada.");
        if (invite.Revoked) throw new DomainException("JoinFamily: la invitación fue revocada.");
        // ISO-8601 "O" strings compare correctly as ordinals — same convention as every date in the domain.
        if (string.CompareOrdinal(cmd.Now, invite.ExpiresAt) > 0) throw new DomainException("JoinFamily: la invitación está vencida.");
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw new DomainException("JoinFamily: Name required.");
        if (string.IsNullOrWhiteSpace(cmd.TokenHash)) throw new DomainException("JoinFamily: TokenHash required.");

        yield return new FamilyEvents.V1.FamilyInviteRedeemed(cmd.InviteId, cmd.MemberId, Now);
        yield return new FamilyEvents.V1.FamilyMemberJoined(cmd.MemberId, cmd.Name.Trim(), MemberRole.Member, cmd.TokenHash, Now, cmd.InviteId);
    }

    public static IEnumerable<object> RevokeInvite(FamilyState state, object[] _, RevokeFamilyInvite cmd)
    {
        var revoker = state.Members.FirstOrDefault(m => m.MemberId == cmd.ByMemberId);
        if (revoker.MemberId == Guid.Empty || revoker.Role != MemberRole.Admin)
            throw new DomainException("RevokeFamilyInvite: solo un administrador puede revocar invitaciones.");
        var invite = state.Invites.FirstOrDefault(i => i.InviteId == cmd.InviteId);
        if (invite.InviteId == Guid.Empty) throw new DomainException("RevokeFamilyInvite: la invitación no existe.");
        if (invite.Redeemed || invite.Revoked) throw new DomainException("RevokeFamilyInvite: la invitación ya no está pendiente.");

        yield return new FamilyEvents.V1.FamilyInviteRevoked(cmd.InviteId, cmd.ByMemberId, Now);
    }

    // Agent keys are issued/revoked ONLY by admin MEMBERS — an agent key can never mint another key or an
    // invite, because its principal id is a key id, never a member id, so these guards fail for it naturally.
    public static IEnumerable<object> IssueAgentKey(FamilyState state, object[] _, IssueFamilyAgentKey cmd)
    {
        var issuer = state.Members.FirstOrDefault(m => m.MemberId == cmd.IssuedByMemberId);
        if (issuer.MemberId == Guid.Empty || issuer.Role != MemberRole.Admin)
            throw new DomainException("IssueFamilyAgentKey: solo un administrador puede generar claves de agente.");
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw new DomainException("IssueFamilyAgentKey: Name required.");
        if (string.IsNullOrWhiteSpace(cmd.TokenHash)) throw new DomainException("IssueFamilyAgentKey: TokenHash required.");

        yield return new FamilyEvents.V1.FamilyAgentKeyIssued(cmd.KeyId, cmd.Name.Trim(), cmd.TokenHash, cmd.IssuedByMemberId, Now);
    }

    public static IEnumerable<object> RevokeAgentKey(FamilyState state, object[] _, RevokeFamilyAgentKey cmd)
    {
        var revoker = state.Members.FirstOrDefault(m => m.MemberId == cmd.ByMemberId);
        if (revoker.MemberId == Guid.Empty || revoker.Role != MemberRole.Admin)
            throw new DomainException("RevokeFamilyAgentKey: solo un administrador puede revocar claves de agente.");
        var key = state.AgentKeys.FirstOrDefault(k => k.KeyId == cmd.KeyId);
        if (key.KeyId == Guid.Empty) throw new DomainException("RevokeFamilyAgentKey: la clave no existe.");
        if (key.Revoked) throw new DomainException("RevokeFamilyAgentKey: la clave ya fue revocada.");

        yield return new FamilyEvents.V1.FamilyAgentKeyRevoked(cmd.KeyId, cmd.ByMemberId, Now);
    }

    // ── Cuentas y login (docs/DISENO_CUENTAS_LOGIN.md) ────────────────────────────────

    /// <summary>
    /// Setea email+contraseña de un miembro. El invariante que importa: <b>el email es único DENTRO de la
    /// familia</b>. Se garantiza acá, en el stream, porque los miembros viven en este State — sin leer el
    /// read-model y sin la ventana de carrera que tendría un chequeo global.
    /// </summary>
    public static IEnumerable<object> SetCredentials(FamilyState state, object[] _, SetMemberCredentials cmd)
    {
        var member = state.Members.FirstOrDefault(m => m.MemberId == cmd.MemberId);
        if (member.MemberId == Guid.Empty) throw new DomainException("SetMemberCredentials: el miembro no existe.");
        if (string.IsNullOrWhiteSpace(cmd.PasswordHash)) throw new DomainException("SetMemberCredentials: PasswordHash required.");

        var email = NormalizeEmail(cmd.Email);
        if (state.Members.Any(m => m.MemberId != cmd.MemberId && m.Email == email))
            throw new DomainException("SetMemberCredentials: ya hay otro miembro de la familia con ese email.");

        yield return new FamilyEvents.V1.MemberCredentialsSet(cmd.MemberId, email, cmd.PasswordHash, Now);
    }

    public static IEnumerable<object> ChangePassword(FamilyState state, object[] _, ChangeMemberPassword cmd)
    {
        var member = state.Members.FirstOrDefault(m => m.MemberId == cmd.MemberId);
        if (member.MemberId == Guid.Empty) throw new DomainException("ChangeMemberPassword: el miembro no existe.");
        if (member.PasswordHash is null) throw new DomainException("ChangeMemberPassword: el miembro todavía no tiene credenciales — usá SetMemberCredentials.");
        if (string.IsNullOrWhiteSpace(cmd.PasswordHash)) throw new DomainException("ChangeMemberPassword: PasswordHash required.");

        yield return new FamilyEvents.V1.MemberPasswordChanged(cmd.MemberId, cmd.PasswordHash, Now);
    }

    public static IEnumerable<object> IssueSession(FamilyState state, object[] _, IssueMemberSession cmd)
    {
        var member = state.Members.FirstOrDefault(m => m.MemberId == cmd.MemberId);
        if (member.MemberId == Guid.Empty) throw new DomainException("IssueMemberSession: el miembro no existe.");
        if (string.IsNullOrWhiteSpace(cmd.TokenHash)) throw new DomainException("IssueMemberSession: TokenHash required.");

        var device = string.IsNullOrWhiteSpace(cmd.DeviceName) ? "Dispositivo" : cmd.DeviceName.Trim();
        yield return new FamilyEvents.V1.MemberSessionIssued(cmd.SessionId, cmd.MemberId, cmd.TokenHash, device, Now);
    }

    /// <summary>Cerrar sesión: solo el DUEÑO de la sesión o un Admin (echar a alguien de un dispositivo robado).</summary>
    public static IEnumerable<object> RevokeSession(FamilyState state, object[] _, RevokeMemberSession cmd)
    {
        var session = state.Sessions.FirstOrDefault(x => x.SessionId == cmd.SessionId);
        if (session.SessionId == Guid.Empty) throw new DomainException("RevokeMemberSession: la sesión no existe.");
        if (session.Revoked) yield break;   // idempotente: cerrar dos veces no es un error

        var actor = state.Members.FirstOrDefault(m => m.MemberId == cmd.ByMemberId);
        if (actor.MemberId == Guid.Empty) throw new DomainException("RevokeMemberSession: el emisor no es miembro de la familia.");
        if (session.MemberId != cmd.ByMemberId && actor.Role != MemberRole.Admin)
            throw new DomainException("RevokeMemberSession: solo el dueño de la sesión o un administrador pueden cerrarla.");

        yield return new FamilyEvents.V1.MemberSessionRevoked(cmd.SessionId, Now);
    }

    /// <summary>
    /// Emite un reseteo de un solo uso. Lo puede pedir un Admin de la familia, o el flujo de INSTANCIA
    /// (<c>IssuedByMemberId == null</c>, respaldado por la X-Admin-Key en el controller): sin esa segunda vía,
    /// un admin que olvida su contraseña deja la familia inaccesible para siempre (§3.1 del diseño).
    /// </summary>
    public static IEnumerable<object> IssueReset(FamilyState state, object[] _, IssuePasswordReset cmd)
    {
        var target = state.Members.FirstOrDefault(m => m.MemberId == cmd.MemberId);
        if (target.MemberId == Guid.Empty) throw new DomainException("IssuePasswordReset: el miembro no existe.");
        if (string.IsNullOrWhiteSpace(cmd.CodeHash)) throw new DomainException("IssuePasswordReset: CodeHash required.");

        if (cmd.IssuedByMemberId is not null)
        {
            var issuer = state.Members.FirstOrDefault(m => m.MemberId == cmd.IssuedByMemberId.Value);
            if (issuer.MemberId == Guid.Empty || issuer.Role != MemberRole.Admin)
                throw new DomainException("IssuePasswordReset: solo un administrador puede generar reseteos.");
        }

        yield return new FamilyEvents.V1.PasswordResetIssued(cmd.ResetId, cmd.MemberId, cmd.CodeHash, Now, cmd.ExpiresAt);
    }

    /// <summary>
    /// Canjea el reseteo: un solo uso, con TTL. Emite además el cambio de contraseña, cuyo fold cierra todas las
    /// sesiones del miembro — si te resetearon porque te robaron el acceso, el que estaba adentro se va.
    /// </summary>
    public static IEnumerable<object> RedeemReset(FamilyState state, object[] _, RedeemPasswordReset cmd)
    {
        var reset = state.PasswordResets.FirstOrDefault(r => r.ResetId == cmd.ResetId);
        if (reset.ResetId == Guid.Empty) throw new DomainException("RedeemPasswordReset: el reseteo no existe.");
        if (reset.Redeemed) throw new DomainException("RedeemPasswordReset: el código ya fue utilizado.");
        // Comparación ordinal de ISO-8601 "O", igual que JoinFamily con las invitaciones.
        if (string.CompareOrdinal(cmd.Now, reset.ExpiresAt) > 0) throw new DomainException("RedeemPasswordReset: el código está vencido.");
        if (string.IsNullOrWhiteSpace(cmd.NewPasswordHash)) throw new DomainException("RedeemPasswordReset: NewPasswordHash required.");

        yield return new FamilyEvents.V1.PasswordResetRedeemed(cmd.ResetId, reset.MemberId, Now);
        yield return new FamilyEvents.V1.MemberPasswordChanged(reset.MemberId, cmd.NewPasswordHash, Now);
    }

    /// <summary>
    /// Los emails se guardan normalizados (trim + minúsculas) para que la unicidad de §2.1 y el login no dependan
    /// de cómo lo tipeó cada uno: "Miyu@X.com " y "miyu@x.com" son la misma cuenta.
    /// </summary>
    static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("Email required.");
        var e = email.Trim().ToLowerInvariant();
        // Validación deliberadamente mínima: sin mail de confirmación, el email es un IDENTIFICADOR de login, no
        // un canal. Una regex "RFC-completa" rechaza direcciones válidas y no agrega nada acá.
        var at = e.IndexOf('@');
        if (at <= 0 || at == e.Length - 1 || e.IndexOf('@', at + 1) >= 0 || e.Contains(' '))
            throw new DomainException($"Email inválido: '{email}'.");
        return e;
    }

    static StreamName Stream(Guid id) => new($"family-{id}");
    static string Now => DateTime.UtcNow.ToString("O");
}
