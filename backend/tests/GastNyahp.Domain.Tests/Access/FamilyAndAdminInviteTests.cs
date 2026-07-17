using Eventuous;
using GastNyahp.Domain.Access;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Tests.Access;

public class AdminInviteTests
{
    static readonly Guid InviteId = Guid.NewGuid();

    [Fact]
    public void Redeem_twice_throws_single_use_guard()
    {
        var state = new AdminInviteState()
            .When(AdminInviteCommandService.Issue(new IssueAdminInvite(InviteId, SecretHash.Compute("codigo"))).Single());
        state = state.When(AdminInviteCommandService.Redeem(state, [], new RedeemAdminInvite(InviteId, Guid.NewGuid())).Single());

        Assert.True(state.Redeemed);
        Assert.Throws<DomainException>(() =>
            AdminInviteCommandService.Redeem(state, [], new RedeemAdminInvite(InviteId, Guid.NewGuid())).ToList());
    }

    [Fact]
    public void Issued_event_carries_the_hash_never_the_raw_code()
    {
        var e = (AdminInviteEvents.V1.AdminInviteIssued)AdminInviteCommandService.Issue(
            new IssueAdminInvite(InviteId, SecretHash.Compute("mi-codigo-secreto"))).Single();

        Assert.DoesNotContain("mi-codigo-secreto", e.CodeHash);
        Assert.Equal(64, e.CodeHash.Length); // sha-256 hex
    }
}

public class FamilyTests
{
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid FounderId = Guid.NewGuid();
    static readonly Guid InviteId = Guid.NewGuid();
    const string Future = "2099-01-01T00:00:00.0000000Z";
    const string Past = "2020-01-01T00:00:00.0000000Z";
    static string Now => DateTime.UtcNow.ToString("O");

    static FamilyState CreatedFamily()
    {
        var state = new FamilyState();
        foreach (var e in FamilyCommandService.Create(new CreateFamily(FamilyId, "Los Pérez", Guid.NewGuid(), FounderId, "Miyu", SecretHash.Compute("token"))))
            state = state.When(e);
        return state;
    }

    static FamilyState WithPendingInvite(FamilyState state, string expiresAt)
    {
        var e = FamilyCommandService.IssueInvite(state, [], new IssueFamilyInvite(FamilyId, InviteId, SecretHash.Compute("invite"), FounderId, expiresAt)).Single();
        return state.When(e);
    }

    [Fact]
    public void Create_yields_family_plus_founding_admin_member()
    {
        var state = CreatedFamily();
        var founder = Assert.Single(state.Members);
        Assert.Equal(MemberRole.Admin, founder.Role);
        Assert.Equal("Miyu", founder.Name);
    }

    [Fact]
    public void Only_admin_members_can_issue_invites()
    {
        var state = CreatedFamily();
        state = WithPendingInvite(state, Future);

        // A regular member joins via that invite…
        var memberId = Guid.NewGuid();
        foreach (var e in FamilyCommandService.Join(state, [], new JoinFamily(FamilyId, InviteId, memberId, "Cami", SecretHash.Compute("t2"), Now)))
            state = state.When(e);

        // …and cannot issue invites themselves.
        Assert.Throws<DomainException>(() => FamilyCommandService.IssueInvite(state, [],
            new IssueFamilyInvite(FamilyId, Guid.NewGuid(), SecretHash.Compute("x"), memberId, Future)).ToList());

        // Nor can a complete stranger.
        Assert.Throws<DomainException>(() => FamilyCommandService.IssueInvite(state, [],
            new IssueFamilyInvite(FamilyId, Guid.NewGuid(), SecretHash.Compute("x"), Guid.NewGuid(), Future)).ToList());
    }

    [Fact]
    public void Join_redeems_the_invite_and_adds_a_member()
    {
        var state = WithPendingInvite(CreatedFamily(), Future);

        var events = FamilyCommandService.Join(state, [], new JoinFamily(FamilyId, InviteId, Guid.NewGuid(), "Cami", SecretHash.Compute("t2"), Now)).ToList();
        Assert.Equal(2, events.Count);
        foreach (var e in events) state = state.When(e);

        Assert.Equal(2, state.Members.Count);
        Assert.True(state.Invites.Single().Redeemed);
        Assert.Equal(MemberRole.Member, state.Members[^1].Role);
    }

    [Fact]
    public void Join_with_a_redeemed_invite_throws_single_use_guard()
    {
        var state = WithPendingInvite(CreatedFamily(), Future);
        foreach (var e in FamilyCommandService.Join(state, [], new JoinFamily(FamilyId, InviteId, Guid.NewGuid(), "Cami", SecretHash.Compute("t2"), Now)))
            state = state.When(e);

        Assert.Throws<DomainException>(() => FamilyCommandService.Join(state, [],
            new JoinFamily(FamilyId, InviteId, Guid.NewGuid(), "Intruso", SecretHash.Compute("t3"), Now)).ToList());
    }

    [Fact]
    public void Join_with_an_expired_invite_throws()
    {
        var state = WithPendingInvite(CreatedFamily(), Past);
        Assert.Throws<DomainException>(() => FamilyCommandService.Join(state, [],
            new JoinFamily(FamilyId, InviteId, Guid.NewGuid(), "Tarde", SecretHash.Compute("t2"), Now)).ToList());
    }

    [Fact]
    public void Join_with_a_revoked_invite_throws()
    {
        var state = WithPendingInvite(CreatedFamily(), Future);
        state = state.When(FamilyCommandService.RevokeInvite(state, [], new RevokeFamilyInvite(FamilyId, InviteId, FounderId)).Single());

        Assert.Throws<DomainException>(() => FamilyCommandService.Join(state, [],
            new JoinFamily(FamilyId, InviteId, Guid.NewGuid(), "Tarde", SecretHash.Compute("t2"), Now)).ToList());
    }

    [Fact]
    public void AgentKey_can_only_be_issued_by_an_admin_member()
    {
        var state = CreatedFamily();
        var keyId = Guid.NewGuid();

        state = state.When(FamilyCommandService.IssueAgentKey(state, [],
            new IssueFamilyAgentKey(FamilyId, keyId, "cron matutino", SecretHash.Compute("agent-token"), FounderId)).Single());
        Assert.Single(state.AgentKeys);
        Assert.False(state.AgentKeys[0].Revoked);

        // A regular member (joined via invite) cannot issue keys.
        state = WithPendingInvite(state, Future);
        var memberId = Guid.NewGuid();
        foreach (var e in FamilyCommandService.Join(state, [], new JoinFamily(FamilyId, InviteId, memberId, "Cami", SecretHash.Compute("t2"), Now)))
            state = state.When(e);
        Assert.Throws<DomainException>(() => FamilyCommandService.IssueAgentKey(state, [],
            new IssueFamilyAgentKey(FamilyId, Guid.NewGuid(), "x", SecretHash.Compute("y"), memberId)).ToList());

        // And neither can the agent key itself (its id is not a member id).
        Assert.Throws<DomainException>(() => FamilyCommandService.IssueAgentKey(state, [],
            new IssueFamilyAgentKey(FamilyId, Guid.NewGuid(), "x", SecretHash.Compute("y"), keyId)).ToList());
    }

    [Fact]
    public void AgentKey_revocation_is_admin_only_and_single_shot()
    {
        var state = CreatedFamily();
        var keyId = Guid.NewGuid();
        state = state.When(FamilyCommandService.IssueAgentKey(state, [],
            new IssueFamilyAgentKey(FamilyId, keyId, "Claude Desktop", SecretHash.Compute("agent-token"), FounderId)).Single());

        Assert.Throws<DomainException>(() => FamilyCommandService.RevokeAgentKey(state, [],
            new RevokeFamilyAgentKey(FamilyId, keyId, Guid.NewGuid())).ToList()); // stranger

        state = state.When(FamilyCommandService.RevokeAgentKey(state, [], new RevokeFamilyAgentKey(FamilyId, keyId, FounderId)).Single());
        Assert.True(state.AgentKeys.Single().Revoked);

        Assert.Throws<DomainException>(() => FamilyCommandService.RevokeAgentKey(state, [],
            new RevokeFamilyAgentKey(FamilyId, keyId, FounderId)).ToList()); // already revoked
    }

    [Fact]
    public void Revoke_requires_admin_and_a_pending_invite()
    {
        var state = WithPendingInvite(CreatedFamily(), Future);

        Assert.Throws<DomainException>(() => FamilyCommandService.RevokeInvite(state, [],
            new RevokeFamilyInvite(FamilyId, InviteId, Guid.NewGuid())).ToList()); // stranger

        state = state.When(FamilyCommandService.RevokeInvite(state, [], new RevokeFamilyInvite(FamilyId, InviteId, FounderId)).Single());
        Assert.Throws<DomainException>(() => FamilyCommandService.RevokeInvite(state, [],
            new RevokeFamilyInvite(FamilyId, InviteId, FounderId)).ToList()); // already revoked
    }
}
