using Eventuous;

namespace GastNyahp.Domain.People;

public static class PersonEvents
{
    public static class V1
    {
        [EventType("V1.PersonRegistered")]
        public record PersonRegistered(Guid PersonId, Guid FamilyId, string Name, string Emoji, string Color);

        [EventType("V1.PersonUpdated")]
        public record PersonUpdated(string Name, string Emoji, string Color);

        [EventType("V1.PersonArchived")]
        public record PersonArchived;
    }
}

public record PersonState : State<PersonState>
{
    public Guid PersonId { get; init; }
    public Guid FamilyId { get; init; }
    public string Name { get; init; } = "";
    public string Emoji { get; init; } = "";
    public string Color { get; init; } = "";
    public bool Archived { get; init; }

    public PersonState()
    {
        On<PersonEvents.V1.PersonRegistered>((s, e) => s with { PersonId = e.PersonId, FamilyId = e.FamilyId, Name = e.Name, Emoji = e.Emoji, Color = e.Color });
        On<PersonEvents.V1.PersonUpdated>((s, e) => s with { Name = e.Name, Emoji = e.Emoji, Color = e.Color });
        On<PersonEvents.V1.PersonArchived>((s, _) => s with { Archived = true });
    }
}

public record RegisterPerson(Guid PersonId, Guid FamilyId, string Name, string Emoji, string Color);
public record UpdatePerson(Guid PersonId, string Name, string Emoji, string Color);
public record ArchivePerson(Guid PersonId);

// No RemovePerson — DOMAIN_MODEL.md §8 decision #4: people are archived, never hard-deleted, so any
// OwnerRef.Owner(personId) already emitted elsewhere keeps resolving name/color/emoji for audit history.
public sealed class PersonCommandService : CommandService<PersonState>
{
    public PersonCommandService(IEventStore store) : base(store)
    {
        On<RegisterPerson>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.PersonId)).Act(Register);
        On<UpdatePerson>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.PersonId)).Act(Update);
        On<ArchivePerson>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.PersonId)).Act(Archive);
    }

    public static IEnumerable<object> Register(RegisterPerson cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw new DomainException("RegisterPerson: Name required.");
        yield return new PersonEvents.V1.PersonRegistered(cmd.PersonId, cmd.FamilyId, cmd.Name.Trim(), cmd.Emoji, cmd.Color);
    }

    public static IEnumerable<object> Update(PersonState state, object[] _, UpdatePerson cmd)
    {
        if (state.Archived) throw new DomainException("UpdatePerson: person is archived.");
        if (string.IsNullOrWhiteSpace(cmd.Name)) throw new DomainException("UpdatePerson: Name required.");
        yield return new PersonEvents.V1.PersonUpdated(cmd.Name.Trim(), cmd.Emoji, cmd.Color);
    }

    public static IEnumerable<object> Archive(PersonState state, object[] _, ArchivePerson cmd)
    {
        if (state.Archived) throw new DomainException("ArchivePerson: person is already archived.");
        yield return new PersonEvents.V1.PersonArchived();
    }

    static StreamName Stream(Guid id) => new($"person-{id}");
}
