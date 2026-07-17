using Eventuous;
using GastNyahp.Domain.Banks;

namespace GastNyahp.Domain.Tests.Banks;

public class BankTests
{
    static readonly Guid BankId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void Register_produces_BankRegistered_event()
    {
        var events = BankCommandService.Register(new RegisterBank(BankId, FamilyId, "BBVA", "Personal", "#004B9B", "bbva")).ToList();

        var registered = Assert.Single(events);
        var e = Assert.IsType<BankEvents.V1.BankRegistered>(registered);
        Assert.Equal(BankId, e.BankId);
        Assert.Equal("BBVA", e.Name);
    }

    [Fact]
    public void Register_without_name_throws()
    {
        Assert.Throws<DomainException>(() =>
            BankCommandService.Register(new RegisterBank(BankId, FamilyId, "  ", null, "#000", "icon")).ToList());
    }

    [Fact]
    public void Register_trims_name()
    {
        var e = (BankEvents.V1.BankRegistered)BankCommandService.Register(new RegisterBank(BankId, FamilyId, "  BBVA  ", null, "#000", "icon")).Single();
        Assert.Equal("BBVA", e.Name);
    }

    [Fact]
    public void State_folds_registration_event()
    {
        var state = new BankState();
        state = state.When(new BankEvents.V1.BankRegistered(BankId, FamilyId, "BBVA", null, "#004B9B", "bbva"));

        Assert.Equal("BBVA", state.Name);
        Assert.False(state.Removed);
    }

    [Fact]
    public void Update_after_removed_throws()
    {
        var state = new BankState()
            .When(new BankEvents.V1.BankRegistered(BankId, FamilyId, "BBVA", null, "#000", "icon"))
            .When(new BankEvents.V1.BankRemoved("2026-02-01T00:00:00Z"));

        Assert.Throws<DomainException>(() =>
            BankCommandService.Update(state, [], new UpdateBank(BankId, "BBVA 2", null, "#000", "icon")).ToList());
    }

    [Fact]
    public void Remove_twice_throws()
    {
        var state = new BankState()
            .When(new BankEvents.V1.BankRegistered(BankId, FamilyId, "BBVA", null, "#000", "icon"))
            .When(new BankEvents.V1.BankRemoved("2026-02-01T00:00:00Z"));

        Assert.Throws<DomainException>(() =>
            BankCommandService.Remove(state, [], new RemoveBank(BankId)).ToList());
    }
}
