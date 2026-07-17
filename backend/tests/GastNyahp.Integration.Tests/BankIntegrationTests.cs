using GastNyahp.Domain.Banks;
using GastNyahp.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests;

public class BankIntegrationTests : IntegrationTest
{
    static readonly Guid BankId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    Task RegisterDefault() => Ok(Host.Banks.Handle(new RegisterBank(BankId, FamilyId, "BBVA", "Personal", "#004B9B", "bbva"), default));

    [Fact]
    public async Task Register_projects_a_row_into_the_read_model()
    {
        await RegisterDefault();

        await using var db = Host.Db();
        var bank = Assert.Single(await db.Banks.ToListAsync());
        Assert.Equal(BankId, bank.Id);
        Assert.Equal("BBVA", bank.Name);
        Assert.Equal("Personal", bank.Alias);
    }

    [Fact]
    public async Task Registering_the_same_id_twice_fails_and_keeps_a_single_row()
    {
        await RegisterDefault();
        await Fails(Host.Banks.Handle(new RegisterBank(BankId, FamilyId, "Otro banco", null, "#000", "x"), default));

        await using var db = Host.Db();
        var bank = Assert.Single(await db.Banks.ToListAsync());
        Assert.Equal("BBVA", bank.Name); // the duplicate never overwrote anything
    }

    [Fact]
    public async Task Update_modifies_the_projected_row()
    {
        await RegisterDefault();
        await Ok(Host.Banks.Handle(new UpdateBank(BankId, "BBVA Francés", "Sueldo", "#004B9B", "bbva"), default));

        await using var db = Host.Db();
        var bank = await db.Banks.SingleAsync();
        Assert.Equal("BBVA Francés", bank.Name);
        Assert.Equal("Sueldo", bank.Alias);
    }

    [Fact]
    public async Task Update_on_a_nonexistent_bank_fails()
    {
        await Fails(Host.Banks.Handle(new UpdateBank(Guid.NewGuid(), "Fantasma", null, "#000", "x"), default));
    }

    [Fact]
    public async Task Remove_deletes_the_projected_row_but_the_event_stream_survives()
    {
        await RegisterDefault();
        await Ok(Host.Banks.Handle(new RemoveBank(BankId), default));

        await using var db = Host.Db();
        Assert.Empty(await db.Banks.ToListAsync());
        // Auditability: removal is an event, the stream keeps the full history.
        Assert.True(await Host.Store.StreamExists(new($"bank-{BankId}"), default));
    }

    [Fact]
    public async Task Replaying_the_registration_event_is_idempotent()
    {
        await RegisterDefault();

        // Simulate the double-processing the projection must tolerate (read-your-writes + subscription replay).
        var projection = new GastNyahp.Infrastructure.Projections.Banks.BankProjection(Host.DbFactory);
        await projection.HandleRegistered(new BankEvents.V1.BankRegistered(BankId, FamilyId, "BBVA", "Personal", "#004B9B", "bbva"));

        await using var db = Host.Db();
        Assert.Single(await db.Banks.ToListAsync());
    }
}
