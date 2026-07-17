namespace GastNyahp.Infrastructure.Projections.Access;

public class FamilyEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    /// <summary>true = familia "del dueño" (nació de la llave de instancia); sus admins pueden emitir enlaces para
    /// crear familias nuevas. false = familia invitada vía uno de esos enlaces, no puede invitar a su vez.</summary>
    public bool IsInstanceOwner { get; set; } = true;
}

/// <summary>Access credentials — the auth middleware resolves Bearer tokens against TokenHash here.
/// NOT the Person read model, which is the expense-attribution label inside a family.</summary>
public class FamilyMemberEntity
{
    public Guid MemberId { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// Email de login (docs/DISENO_CUENTAS_LOGIN.md). NULL = miembro de la etapa 1, que todavía entra solo con su
    /// token de posesión. Único por familia: hay un índice único compuesto (FamilyId, Email) que duplica en la DB
    /// el invariante que ya garantiza el aggregate — defensa en profundidad, no redundancia.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>Hash PBKDF2 (nunca la contraseña, nunca un SecretHash). NULL = sin cuenta todavía.</summary>
    public string? PasswordHash { get; set; }
}

/// <summary>
/// Un dispositivo logueado. El middleware resuelve el Bearer contra TokenHash acá, igual que con los miembros y
/// las agent keys. Existe porque el login no puede devolver el token del miembro (solo se guarda su hash) y con
/// un único token por miembro, entrar en el celular deslogearía la compu.
/// </summary>
public class MemberSessionEntity
{
    public Guid SessionId { get; set; }
    public Guid FamilyId { get; set; }
    public Guid MemberId { get; set; }
    public string TokenHash { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public bool Revoked { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Reseteo de contraseña de un solo uso emitido por un Admin (no hay mail). El código va hasheado.</summary>
public class PasswordResetEntity
{
    public Guid ResetId { get; set; }
    public Guid FamilyId { get; set; }
    public Guid MemberId { get; set; }
    public string CodeHash { get; set; } = "";
    public string ExpiresAt { get; set; } = "";
    public bool Redeemed { get; set; }
}

public class FamilyInviteEntity
{
    public Guid InviteId { get; set; }
    public Guid FamilyId { get; set; }
    public string CodeHash { get; set; } = "";
    public string ExpiresAt { get; set; } = "";
    public bool Redeemed { get; set; }
    public bool Revoked { get; set; }
}

public class AdminInviteEntity
{
    public Guid Id { get; set; }
    public string CodeHash { get; set; } = "";
    public bool Redeemed { get; set; }

    /// <summary>ISO-8601 UTC, o NULL = sin vencimiento (códigos del operador).</summary>
    public string? ExpiresAt { get; set; }

    /// <summary>true = la familia creada con este código será "del dueño"; false = invitada.</summary>
    public bool GrantsOwner { get; set; } = true;
}

/// <summary>Bearer credential for MCP/agents (DOMAIN_MODEL.md §17): data access only, individually revocable.</summary>
public class FamilyAgentKeyEntity
{
    public Guid KeyId { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public bool Revoked { get; set; }
    public DateTime IssuedAt { get; set; }
}
