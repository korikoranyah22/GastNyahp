using Eventuous;

namespace GastNyahp.Domain.Access;

public static class AdminInviteEvents
{
    public static class V1
    {
        // ExpiresAt/GrantsOwner son opcionales para replay-safety: los eventos viejos (sin estos campos) deserializan
        // a null → se tratan como "sin vencimiento" y "otorga dueño" (eran códigos del operador). Los enlaces nuevos
        // que emite un dueño para invitar familias traen ExpiresAt seteado y GrantsOwner=false.
        [EventType("V1.AdminInviteIssued")]
        public record AdminInviteIssued(Guid InviteId, string CodeHash, string IssuedAt, string? ExpiresAt = null, bool? GrantsOwner = null);

        [EventType("V1.AdminInviteRedeemed")]
        public record AdminInviteRedeemed(Guid FamilyId, string RedeemedAt);
    }
}

public record AdminInviteState : State<AdminInviteState>
{
    public Guid InviteId { get; init; }
    public string CodeHash { get; init; } = "";
    public bool Redeemed { get; init; }
    public Guid? RedeemedByFamilyId { get; init; }
    public string? ExpiresAt { get; init; }
    public bool GrantsOwner { get; init; } = true;

    public AdminInviteState()
    {
        On<AdminInviteEvents.V1.AdminInviteIssued>((s, e) => s with
        {
            InviteId = e.InviteId, CodeHash = e.CodeHash, ExpiresAt = e.ExpiresAt, GrantsOwner = e.GrantsOwner ?? true,
        });
        On<AdminInviteEvents.V1.AdminInviteRedeemed>((s, e) => s with { Redeemed = true, RedeemedByFamilyId = e.FamilyId });
    }
}

/// <param name="ExpiresAt">ISO-8601 UTC, o null = sin vencimiento (códigos del operador).</param>
/// <param name="GrantsOwner">true = la familia creada será "del dueño" (puede invitar más); false = invitada.</param>
public record IssueAdminInvite(Guid InviteId, string CodeHash, string? ExpiresAt = null, bool GrantsOwner = true);
public record RedeemAdminInvite(Guid InviteId, Guid FamilyId);

/// <summary>The gate in front of family creation (DOMAIN_MODEL.md §17.1): without a code issued by the app
/// administrator there is no way to create a family — no open sign-up, no emails, no SMS.</summary>
public sealed class AdminInviteCommandService : CommandService<AdminInviteState>
{
    public AdminInviteCommandService(IEventStore store) : base(store)
    {
        On<IssueAdminInvite>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.InviteId)).Act(Issue);
        On<RedeemAdminInvite>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.InviteId)).Act(Redeem);
    }

    public static IEnumerable<object> Issue(IssueAdminInvite cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.CodeHash)) throw new DomainException("IssueAdminInvite: CodeHash required.");
        yield return new AdminInviteEvents.V1.AdminInviteIssued(cmd.InviteId, cmd.CodeHash, Now, cmd.ExpiresAt, cmd.GrantsOwner);
    }

    public static IEnumerable<object> Redeem(AdminInviteState state, object[] _, RedeemAdminInvite cmd)
    {
        if (state.Redeemed) throw new DomainException("RedeemAdminInvite: el código ya fue utilizado.");
        yield return new AdminInviteEvents.V1.AdminInviteRedeemed(cmd.FamilyId, Now);
    }

    static StreamName Stream(Guid id) => new($"admin-invite-{id}");
    static string Now => DateTime.UtcNow.ToString("O");
}
