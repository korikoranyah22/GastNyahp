using Eventuous;

namespace GastNyahp.Domain.Common;

/// <summary>
/// Who a financial fact (installment, service, expense, ticket item) belongs to.
/// "Shared" is a well-known sentinel, not a Person row (see DOMAIN_MODEL.md §1.3) — it never carries a PersonId.
/// </summary>
public abstract record OwnerRef
{
    OwnerRef() { }

    public sealed record Unassigned : OwnerRef;
    public sealed record Shared : OwnerRef;
    public sealed record Owner(Guid Id) : OwnerRef;

    public static readonly OwnerRef None = new Unassigned();
    public static readonly OwnerRef SharedOwner = new Shared();
    public static OwnerRef Of(Guid personId) => new Owner(Id: personId);

    public string Kind => this switch
    {
        Unassigned => "Unassigned",
        Shared => "Shared",
        Owner => "Owner",
        _ => throw new InvalidOperationException("Unknown OwnerRef case."),
    };

    public Guid? PersonId => this is Owner o ? o.Id : null;

    /// <summary>Reconstructs an OwnerRef from the two primitive fields persisted in events.</summary>
    public static OwnerRef FromPrimitive(string kind, Guid? personId) => kind switch
    {
        "Shared" => SharedOwner,
        "Owner" => Of(personId ?? throw new DomainException("OwnerRef: PersonId required when Kind is 'Owner'.")),
        "Unassigned" => None,
        _ => throw new DomainException($"OwnerRef: unknown kind '{kind}'."),
    };
}
