using Eventuous;

namespace GastNyahp.Domain.Common;

/// <summary>How an Expense/Ticket was paid. Only Card generates a "billing month" via BillingCycle.</summary>
public abstract record PaymentMethod
{
    PaymentMethod() { }

    public sealed record Card(Guid CardId) : PaymentMethod;
    public sealed record Debit(Guid BankId) : PaymentMethod;
    public sealed record Cash : PaymentMethod;
    public sealed record Modo : PaymentMethod;
    public sealed record MercadoPago : PaymentMethod;

    public static readonly PaymentMethod CashPayment = new Cash();
    public static readonly PaymentMethod ModoPayment = new Modo();
    public static readonly PaymentMethod MercadoPagoPayment = new MercadoPago();
    public static PaymentMethod ByCard(Guid cardId) => new Card(cardId);
    public static PaymentMethod ByDebit(Guid bankId) => new Debit(bankId);

    public string Kind => this switch
    {
        Card => "Card",
        Debit => "Debit",
        Cash => "Cash",
        Modo => "Modo",
        MercadoPago => "MercadoPago",
        _ => throw new InvalidOperationException("Unknown PaymentMethod case."),
    };

    /// <summary>Non-null only for Card/Debit — the referenced CreditCard or Bank id.</summary>
    public Guid? ReferenceId => this switch
    {
        Card c => c.CardId,
        Debit d => d.BankId,
        _ => null,
    };

    public bool IsCredit => this is Card;
    public bool IsDebitOrCash => this is Debit or Cash or Modo or MercadoPago;

    public static PaymentMethod FromPrimitive(string kind, Guid? referenceId) => kind switch
    {
        "Card" => ByCard(referenceId ?? throw new DomainException("PaymentMethod: CardId required for Card.")),
        "Debit" => ByDebit(referenceId ?? throw new DomainException("PaymentMethod: BankId required for Debit.")),
        "Cash" => CashPayment,
        "Modo" => ModoPayment,
        "MercadoPago" => MercadoPagoPayment,
        _ => throw new DomainException($"PaymentMethod: unknown kind '{kind}'."),
    };
}
