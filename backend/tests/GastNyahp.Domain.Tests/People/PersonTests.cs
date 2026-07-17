using Eventuous;
using GastNyahp.Domain.People;

namespace GastNyahp.Domain.Tests.People;

public class PersonTests
{
    static readonly Guid PersonId = Guid.NewGuid();
    static readonly Guid FamilyId = Guid.NewGuid();

    [Fact]
    public void Register_requires_name() =>
        Assert.Throws<DomainException>(() => PersonCommandService.Register(new RegisterPerson(PersonId, FamilyId, "  ", "😀", "#000")).ToList());

    [Fact]
    public void Archive_then_update_throws()
    {
        var state = new PersonState().When(PersonCommandService.Register(new RegisterPerson(PersonId, FamilyId, "Cami", "😀", "#000")).Single());
        state = state.When(PersonCommandService.Archive(state, [], new ArchivePerson(PersonId)).Single());

        Assert.True(state.Archived);
        Assert.Throws<DomainException>(() => PersonCommandService.Update(state, [], new UpdatePerson(PersonId, "Cami 2", "😀", "#000")).ToList());
    }

    [Fact]
    public void Archive_twice_throws()
    {
        var state = new PersonState().When(PersonCommandService.Register(new RegisterPerson(PersonId, FamilyId, "Cami", "😀", "#000")).Single());
        state = state.When(PersonCommandService.Archive(state, [], new ArchivePerson(PersonId)).Single());

        Assert.Throws<DomainException>(() => PersonCommandService.Archive(state, [], new ArchivePerson(PersonId)).ToList());
    }

    // There is deliberately no RemovePerson command/test — see DOMAIN_MODEL.md §8: people are archived, not
    // hard-deleted, precisely so historical OwnerRef.Owner(personId) references never go orphaned.
}
