using GastNyahp.Domain.People;
using GastNyahp.Domain.Reserves;
using GastNyahp.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Integration.Tests;

public class ReserveIntegrationTests : IntegrationTest
{
    static readonly Guid ReserveId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public async Task SetMonthAmount_is_an_upsert_amount_and_note()
    {
        await Ok(Host.Reserves.Handle(new RegisterReserve(ReserveId, FamilyId, "Cami", ReserveType.Reserve, "👤", false, 0), default));
        await Ok(Host.Reserves.Handle(new SetReserveMonthAmount(ReserveId, "2026-02", 30000m, "Facu + Médica"), default));
        await Ok(Host.Reserves.Handle(new SetReserveMonthAmount(ReserveId, "2026-02", 40000m, "actualizado"), default));

        await using var db = Host.Db();
        var overrideRow = Assert.Single(await db.ReserveMonthOverrides.ToListAsync());
        Assert.Equal(40000m, overrideRow.Amount);
        Assert.Equal("actualizado", overrideRow.Note);
    }

    [Fact]
    public async Task ApplyBase_clears_all_overrides_and_marks_recurring()
    {
        await Ok(Host.Reserves.Handle(new RegisterReserve(ReserveId, FamilyId, "Efectivo", ReserveType.Cash, "💵", false, 0), default));
        await Ok(Host.Reserves.Handle(new SetReserveMonthAmount(ReserveId, "2026-01", 1000m, null), default));
        await Ok(Host.Reserves.Handle(new SetReserveMonthAmount(ReserveId, "2026-02", 2000m, null), default));

        await Ok(Host.Reserves.Handle(new ApplyReserveBaseToAllMonths(ReserveId, 100000m), default));

        await using var db = Host.Db();
        var reserve = await db.Reserves.SingleAsync();
        Assert.True(reserve.Recurring);
        Assert.Equal(100000m, reserve.BaseAmount);
        Assert.Empty(await db.ReserveMonthOverrides.ToListAsync()); // destructive by design (DOMAIN_MODEL.md §7)
    }

    [Fact]
    public async Task Remove_cascades_overrides()
    {
        await Ok(Host.Reserves.Handle(new RegisterReserve(ReserveId, FamilyId, "Cami", ReserveType.Reserve, "👤", false, 0), default));
        await Ok(Host.Reserves.Handle(new SetReserveMonthAmount(ReserveId, "2026-02", 30000m, null), default));
        await Ok(Host.Reserves.Handle(new RemoveReserve(ReserveId), default));

        await using var db = Host.Db();
        Assert.Empty(await db.Reserves.ToListAsync());
        Assert.Empty(await db.ReserveMonthOverrides.ToListAsync());
    }
}

public class PersonIntegrationTests : IntegrationTest
{
    static readonly Guid PersonId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public async Task Register_and_update_project_the_row()
    {
        await Ok(Host.People.Handle(new RegisterPerson(PersonId, FamilyId, "Cami", "😀", "#e11d48"), default));
        await Ok(Host.People.Handle(new UpdatePerson(PersonId, "Camila", "🌸", "#e11d48"), default));

        await using var db = Host.Db();
        var person = Assert.Single(await db.People.ToListAsync());
        Assert.Equal("Camila", person.Name);
        Assert.Equal("🌸", person.Emoji);
    }

    [Fact]
    public async Task Archive_keeps_the_row_so_historical_owner_refs_still_resolve()
    {
        await Ok(Host.People.Handle(new RegisterPerson(PersonId, FamilyId, "Cami", "😀", "#e11d48"), default));
        await Ok(Host.People.Handle(new ArchivePerson(PersonId), default));

        await using var db = Host.Db();
        var person = Assert.Single(await db.People.ToListAsync()); // NOT deleted — DOMAIN_MODEL.md §8
        Assert.True(person.Archived);
        Assert.Equal("Cami", person.Name);
    }

    [Fact]
    public async Task Updating_an_archived_person_fails()
    {
        await Ok(Host.People.Handle(new RegisterPerson(PersonId, FamilyId, "Cami", "😀", "#e11d48"), default));
        await Ok(Host.People.Handle(new ArchivePerson(PersonId), default));
        await Fails(Host.People.Handle(new UpdatePerson(PersonId, "Otra", "😀", "#000"), default));
    }
}
