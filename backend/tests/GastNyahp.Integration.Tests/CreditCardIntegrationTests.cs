using GastNyahp.Domain.Cards;
using GastNyahp.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests;

public class CreditCardIntegrationTests : IntegrationTest
{
    static readonly Guid CardId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid BankId = Guid.NewGuid();

    Task RegisterDefault() => Ok(Host.Cards.Handle(
        new RegisterCard(CardId, FamilyId, BankId, "VISA BBVA", CardNetwork.Visa, CardType.Credit, 15, 5, "#1e40af"), default));

    [Fact]
    public async Task Register_projects_an_active_card()
    {
        await RegisterDefault();

        await using var db = Host.Db();
        var card = Assert.Single(await db.CreditCards.ToListAsync());
        Assert.Equal("VISA BBVA", card.Label);
        Assert.Equal("Visa", card.Network);
        Assert.Equal(15, card.ClosingDay);
        Assert.Equal(5, card.DueDay);
        Assert.True(card.Active);
    }

    [Fact]
    public async Task Full_lifecycle_update_deactivate_reactivate()
    {
        await RegisterDefault();

        await Ok(Host.Cards.Handle(new UpdateCard(CardId, "VISA BBVA Gold", CardNetwork.Visa, CardType.Credit, 18, 8, "#0a0a5f"), default));
        await Ok(Host.Cards.Handle(new DeactivateCard(CardId), default));

        await using (var db = Host.Db())
        {
            var card = await db.CreditCards.SingleAsync();
            Assert.Equal("VISA BBVA Gold", card.Label);
            Assert.Equal(18, card.ClosingDay);
            Assert.False(card.Active);
        }

        await Ok(Host.Cards.Handle(new ActivateCard(CardId), default));

        await using (var db = Host.Db())
            Assert.True((await db.CreditCards.SingleAsync()).Active);
    }

    [Fact]
    public async Task Deactivating_twice_fails_at_the_command_level()
    {
        await RegisterDefault();
        await Ok(Host.Cards.Handle(new DeactivateCard(CardId), default));
        await Fails(Host.Cards.Handle(new DeactivateCard(CardId), default));
    }

    [Fact]
    public async Task Remove_deletes_the_row()
    {
        await RegisterDefault();
        await Ok(Host.Cards.Handle(new RemoveCard(CardId), default));

        await using var db = Host.Db();
        Assert.Empty(await db.CreditCards.ToListAsync());
    }
}
