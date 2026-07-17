using Eventuous;
using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;

namespace GastNyahp.Domain.Tests.Expenses;

public class TicketTests
{
    static readonly Guid TicketId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    static TicketItemInput Item(decimal amount, string category = "Comida") =>
        new(Guid.NewGuid(), "Ítem", amount, category, "Unassigned", null);

    static RegisterTicket ValidTicket(params TicketItemInput[] items) =>
        new(TicketId, FamilyId, "2026-02-15", "Supermercado", PaymentMethod.CashPayment, Discount: 0, Items: items);

    [Fact]
    public void Total_is_sum_of_items_minus_discount()
    {
        var state = new TicketState().When(TicketCommandService.Register(
            ValidTicket(Item(30000m), Item(8000m)) with { Discount = 5000m }).Single());

        Assert.Equal(33000m, state.Total); // 30000 + 8000 - 5000
    }

    [Fact]
    public void Total_never_goes_negative_even_if_discount_exceeds_subtotal()
    {
        var state = new TicketState().When(TicketCommandService.Register(
            ValidTicket(Item(1000m)) with { Discount = 5000m }).Single());

        Assert.Equal(0m, state.Total);
    }

    [Fact]
    public void Register_requires_at_least_one_item() =>
        Assert.Throws<DomainException>(() => TicketCommandService.Register(ValidTicket()).ToList());

    [Fact]
    public void Register_validates_each_item_independently()
    {
        var badSecondItem = Item(0m); // zero amount is invalid
        var ex = Assert.Throws<DomainException>(() =>
            TicketCommandService.Register(ValidTicket(Item(1000m), badSecondItem)).ToList());
        Assert.Contains("item 2", ex.Message);
    }

    [Fact]
    public void Update_replaces_the_entire_item_set()
    {
        var state = new TicketState().When(TicketCommandService.Register(ValidTicket(Item(1000m), Item(2000m))).Single());
        Assert.Equal(2, state.Items.Count);

        state = state.When(TicketCommandService.Update(state, [], new UpdateTicket(
            TicketId, "2026-02-16", "Super", PaymentMethod.CashPayment, 0, [Item(500m)])).Single());

        Assert.Single(state.Items);
        Assert.Equal(500m, state.Total);
    }
}
