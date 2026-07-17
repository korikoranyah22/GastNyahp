using Eventuous;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Tests.Common;

public class OwnerRefTests
{
    [Fact]
    public void Shared_never_carries_a_PersonId()
    {
        Assert.Equal("Shared", OwnerRef.SharedOwner.Kind);
        Assert.Null(OwnerRef.SharedOwner.PersonId);
    }

    [Fact]
    public void FromPrimitive_roundtrips_Owner()
    {
        var id = Guid.NewGuid();
        var owner = OwnerRef.FromPrimitive("Owner", id);
        Assert.Equal(id, owner.PersonId);
    }

    [Fact]
    public void FromPrimitive_Owner_without_PersonId_throws() =>
        Assert.Throws<DomainException>(() => OwnerRef.FromPrimitive("Owner", null));

    [Fact]
    public void FromPrimitive_unknown_kind_throws() =>
        Assert.Throws<DomainException>(() => OwnerRef.FromPrimitive("Bogus", null));
}

public class PaymentMethodTests
{
    [Fact]
    public void Card_is_credit_not_debit_or_cash()
    {
        var pm = PaymentMethod.ByCard(Guid.NewGuid());
        Assert.True(pm.IsCredit);
        Assert.False(pm.IsDebitOrCash);
    }

    [Theory]
    [InlineData("Cash")]
    [InlineData("Modo")]
    [InlineData("MercadoPago")]
    public void Non_card_non_debit_kinds_are_DebitOrCash(string kind)
    {
        var pm = PaymentMethod.FromPrimitive(kind, null);
        Assert.False(pm.IsCredit);
        Assert.True(pm.IsDebitOrCash);
    }

    [Fact]
    public void Debit_requires_a_BankId() =>
        Assert.Throws<DomainException>(() => PaymentMethod.FromPrimitive("Debit", null));

    [Fact]
    public void ReferenceId_is_null_for_cash()
    {
        Assert.Null(PaymentMethod.CashPayment.ReferenceId);
    }
}
