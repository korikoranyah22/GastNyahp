using Eventuous;
using GastNyahp.Domain.Cards;

namespace GastNyahp.Domain.Tests.Cards;

public class CreditCardTests
{
    static readonly Guid CardId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid BankId = Guid.NewGuid();

    static RegisterCard ValidRegister(int closingDay = 15, int dueDay = 5) =>
        new(CardId, FamilyId, BankId, "VISA BBVA", CardNetwork.Visa, CardType.Credit, closingDay, dueDay, "#1e40af");

    [Fact]
    public void Register_produces_event_with_active_true()
    {
        var e = (CreditCardEvents.V1.CardRegistered)CreditCardCommandService.Register(ValidRegister()).Single();
        Assert.Equal("VISA BBVA", e.Label);

        var state = new CreditCardState().When(e);
        Assert.True(state.Active);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(32, 5)]
    [InlineData(15, 0)]
    [InlineData(15, 32)]
    public void Register_rejects_out_of_range_days(int closingDay, int dueDay) =>
        Assert.Throws<DomainException>(() => CreditCardCommandService.Register(ValidRegister(closingDay, dueDay)).ToList());

    [Fact]
    public void Register_rejects_empty_label() =>
        Assert.Throws<DomainException>(() =>
            CreditCardCommandService.Register(ValidRegister() with { Label = "  " }).ToList());

    [Fact]
    public void Activate_when_already_active_throws()
    {
        var state = new CreditCardState().When(CreditCardCommandService.Register(ValidRegister()).Single());
        Assert.Throws<DomainException>(() =>
            CreditCardCommandService.Activate(state, [], new ActivateCard(CardId)).ToList());
    }

    [Fact]
    public void Deactivate_then_reactivate_flips_state()
    {
        var state = new CreditCardState().When(CreditCardCommandService.Register(ValidRegister()).Single());

        state = state.When(CreditCardCommandService.Deactivate(state, [], new DeactivateCard(CardId)).Single());
        Assert.False(state.Active);

        state = state.When(CreditCardCommandService.Activate(state, [], new ActivateCard(CardId)).Single());
        Assert.True(state.Active);
    }

    [Fact]
    public void Deactivate_when_already_inactive_throws()
    {
        var state = new CreditCardState().When(CreditCardCommandService.Register(ValidRegister()).Single());
        state = state.When(CreditCardCommandService.Deactivate(state, [], new DeactivateCard(CardId)).Single());

        Assert.Throws<DomainException>(() =>
            CreditCardCommandService.Deactivate(state, [], new DeactivateCard(CardId)).ToList());
    }

    [Fact]
    public void Update_after_removed_throws()
    {
        var state = new CreditCardState().When(CreditCardCommandService.Register(ValidRegister()).Single());
        state = state.When(CreditCardCommandService.Remove(state, [], new RemoveCard(CardId)).Single());

        Assert.Throws<DomainException>(() =>
            CreditCardCommandService.Update(state, [], new UpdateCard(CardId, "New label", CardNetwork.Visa, CardType.Credit, 15, 5, "#000")).ToList());
    }
}
