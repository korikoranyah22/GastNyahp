using Eventuous;
using GastNyahp.Domain.Access;

namespace GastNyahp.Domain.Tests.Access;

/// <summary>
/// Cuentas y login en el aggregate Family (docs/DISENO_CUENTAS_LOGIN.md fase 2). Lo que se blinda acá:
/// el invariante del email único por familia, quién puede resetear a quién, el un-solo-uso del reseteo, y que
/// cambiar la contraseña eche a todos los dispositivos.
/// </summary>
public class FamilyAccountsTests
{
    static readonly Guid FamilyId = Guid.NewGuid();
    static readonly Guid AdminId = Guid.NewGuid();
    static readonly Guid MemberId = Guid.NewGuid();
    static readonly Guid InviteId = Guid.NewGuid();

    const string Hash = "pbkdf2-sha256$600000$c2FsdA==$aGFzaA==";
    const string OtroHash = "pbkdf2-sha256$600000$c2FsdA==$b3Rybw==";

    static string Now => DateTime.UtcNow.ToString("O");
    static string EnUnRato => DateTime.UtcNow.AddHours(48).ToString("O");
    static string HaceUnRato => DateTime.UtcNow.AddHours(-1).ToString("O");

    /// <summary>Familia con un Admin (fundador) y un Member que entró por invitación.</summary>
    static FamilyState ConDosMiembros()
    {
        var s = new FamilyState();
        foreach (var e in FamilyCommandService.Create(
            new CreateFamily(FamilyId, "Mikami", Guid.NewGuid(), AdminId, "Miyu", "token-admin")))
            s = s.When(e);
        s = s.When(FamilyCommandService.IssueInvite(s, [],
            new IssueFamilyInvite(FamilyId, InviteId, "code-hash", AdminId, EnUnRato)).Single());
        foreach (var e in FamilyCommandService.Join(s, [],
            new JoinFamily(FamilyId, InviteId, MemberId, "Cami", "token-cami", Now)))
            s = s.When(e);
        return s;
    }

    static FamilyState ConCredenciales(FamilyState s, Guid memberId, string email) =>
        s.When(FamilyCommandService.SetCredentials(s, [], new SetMemberCredentials(FamilyId, memberId, email, Hash)).Single());

    // ── Credenciales ────────────────────────────────────────────────────────────

    [Fact]
    public void SetCredentials_attaches_email_and_hash_to_the_member()
    {
        var s = ConCredenciales(ConDosMiembros(), AdminId, "miyu@x.com");
        var m = s.Members.Single(x => x.MemberId == AdminId);

        Assert.Equal("miyu@x.com", m.Email);
        Assert.Equal(Hash, m.PasswordHash);
        Assert.Equal("token-admin", m.TokenHash);   // el token viejo sigue: etapa 1, nadie queda afuera
    }

    [Fact]
    public void Members_start_without_credentials()
    {
        // Etapa 1: los que ya existían no tienen cuenta y siguen entrando con su token.
        var s = ConDosMiembros();
        Assert.All(s.Members, m => Assert.Null(m.Email));
        Assert.All(s.Members, m => Assert.Null(m.PasswordHash));
    }

    [Theory]
    [InlineData("Miyu@X.com")]
    [InlineData("  miyu@x.com  ")]
    [InlineData("MIYU@X.COM")]
    public void The_email_is_normalized(string tipeado)
    {
        // Para que la unicidad y el login no dependan de cómo lo tipeó cada uno.
        var s = ConCredenciales(ConDosMiembros(), AdminId, tipeado);
        Assert.Equal("miyu@x.com", s.Members.Single(x => x.MemberId == AdminId).Email);
    }

    [Fact]
    public void Two_members_of_the_same_family_cannot_share_an_email()
    {
        // EL invariante de §2.1: es de aggregate puro, se garantiza en el stream sin leer el read-model.
        var s = ConCredenciales(ConDosMiembros(), AdminId, "compartido@x.com");

        var ex = Assert.Throws<DomainException>(() => FamilyCommandService.SetCredentials(s, [],
            new SetMemberCredentials(FamilyId, MemberId, "compartido@x.com", OtroHash)).ToList());
        Assert.Contains("otro miembro", ex.Message);
    }

    [Fact]
    public void The_duplicate_check_is_case_insensitive()
    {
        var s = ConCredenciales(ConDosMiembros(), AdminId, "miyu@x.com");
        Assert.Throws<DomainException>(() => FamilyCommandService.SetCredentials(s, [],
            new SetMemberCredentials(FamilyId, MemberId, "MIYU@X.COM", OtroHash)).ToList());
    }

    [Fact]
    public void A_member_can_change_its_own_email_to_the_same_value()
    {
        // El chequeo excluye al propio miembro: re-setear tu email no puede chocar contra vos mismo.
        var s = ConCredenciales(ConDosMiembros(), AdminId, "miyu@x.com");
        var ok = FamilyCommandService.SetCredentials(s, [], new SetMemberCredentials(FamilyId, AdminId, "miyu@x.com", OtroHash)).ToList();
        Assert.Single(ok);
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("@sindominio")]
    [InlineData("sinusuario@")]
    [InlineData("dos@@arrobas.com")]
    [InlineData("con espacio@x.com")]
    [InlineData("")]
    public void An_invalid_email_is_rejected(string email) =>
        Assert.Throws<DomainException>(() => FamilyCommandService.SetCredentials(ConDosMiembros(), [],
            new SetMemberCredentials(FamilyId, AdminId, email, Hash)).ToList());

    [Fact]
    public void SetCredentials_requires_an_existing_member() =>
        Assert.Throws<DomainException>(() => FamilyCommandService.SetCredentials(ConDosMiembros(), [],
            new SetMemberCredentials(FamilyId, Guid.NewGuid(), "x@x.com", Hash)).ToList());

    [Fact]
    public void ChangePassword_requires_existing_credentials()
    {
        // Sin cuenta no hay contraseña que cambiar: para eso está SetCredentials.
        var ex = Assert.Throws<DomainException>(() => FamilyCommandService.ChangePassword(ConDosMiembros(), [],
            new ChangeMemberPassword(FamilyId, AdminId, Hash)).ToList());
        Assert.Contains("todavía no tiene credenciales", ex.Message);
    }

    // ── Sesiones ────────────────────────────────────────────────────────────────

    [Fact]
    public void Sessions_accumulate_so_you_can_use_several_devices()
    {
        var s = ConDosMiembros();
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, AdminId, Guid.NewGuid(), "t-celu", "Celular")).Single());
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, AdminId, Guid.NewGuid(), "t-compu", "Compu")).Single());

        // Entrar en el celu NO desloguea la compu.
        Assert.Equal(2, s.Sessions.Count(x => !x.Revoked));
    }

    [Fact]
    public void Changing_the_password_revokes_every_session_of_that_member()
    {
        // Amenaza #6: si te robaron un dispositivo, cambiar la clave tiene que echarlo.
        var s = ConCredenciales(ConDosMiembros(), AdminId, "miyu@x.com");
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, AdminId, Guid.NewGuid(), "t1", "Celu")).Single());
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, AdminId, Guid.NewGuid(), "t2", "Compu")).Single());
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, MemberId, Guid.NewGuid(), "t3", "Cami")).Single());

        s = s.When(FamilyCommandService.ChangePassword(s, [], new ChangeMemberPassword(FamilyId, AdminId, OtroHash)).Single());

        Assert.All(s.Sessions.Where(x => x.MemberId == AdminId), x => Assert.True(x.Revoked));
        Assert.All(s.Sessions.Where(x => x.MemberId == MemberId), x => Assert.False(x.Revoked));   // la de Cami no se toca
    }

    [Fact]
    public void You_can_close_your_own_session()
    {
        var sid = Guid.NewGuid();
        var s = ConDosMiembros();
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, MemberId, sid, "t", "Celu")).Single());
        s = s.When(FamilyCommandService.RevokeSession(s, [], new RevokeMemberSession(FamilyId, sid, MemberId)).Single());

        Assert.True(s.Sessions.Single().Revoked);
    }

    [Fact]
    public void An_admin_can_close_someone_elses_session()
    {
        var sid = Guid.NewGuid();
        var s = ConDosMiembros();
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, MemberId, sid, "t", "Celu")).Single());
        s = s.When(FamilyCommandService.RevokeSession(s, [], new RevokeMemberSession(FamilyId, sid, AdminId)).Single());

        Assert.True(s.Sessions.Single().Revoked);
    }

    [Fact]
    public void A_member_cannot_close_another_members_session()
    {
        var sid = Guid.NewGuid();
        var s = ConDosMiembros();
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, AdminId, sid, "t", "Celu")).Single());

        Assert.Throws<DomainException>(() => FamilyCommandService.RevokeSession(s, [],
            new RevokeMemberSession(FamilyId, sid, MemberId)).ToList());
    }

    [Fact]
    public void Closing_an_already_closed_session_is_idempotent()
    {
        var sid = Guid.NewGuid();
        var s = ConDosMiembros();
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, MemberId, sid, "t", "Celu")).Single());
        s = s.When(FamilyCommandService.RevokeSession(s, [], new RevokeMemberSession(FamilyId, sid, MemberId)).Single());

        Assert.Empty(FamilyCommandService.RevokeSession(s, [], new RevokeMemberSession(FamilyId, sid, MemberId)));
    }

    // ── Reseteo de contraseña ───────────────────────────────────────────────────

    [Fact]
    public void An_admin_can_issue_a_reset()
    {
        var s = ConDosMiembros();
        var ev = FamilyCommandService.IssueReset(s, [],
            new IssuePasswordReset(FamilyId, MemberId, Guid.NewGuid(), "code", AdminId, EnUnRato)).ToList();
        Assert.Single(ev);
    }

    [Fact]
    public void A_plain_member_cannot_issue_a_reset()
    {
        var s = ConDosMiembros();
        var ex = Assert.Throws<DomainException>(() => FamilyCommandService.IssueReset(s, [],
            new IssuePasswordReset(FamilyId, AdminId, Guid.NewGuid(), "code", MemberId, EnUnRato)).ToList());
        Assert.Contains("solo un administrador", ex.Message);
    }

    [Fact]
    public void The_instance_flow_can_issue_a_reset_without_being_a_member()
    {
        // §3.1 — la salida de emergencia: sin esto, el único Admin que olvida su contraseña deja la familia
        // inaccesible para siempre. IssuedByMemberId = null lo respalda la X-Admin-Key en el controller.
        var s = ConDosMiembros();
        var ev = FamilyCommandService.IssueReset(s, [],
            new IssuePasswordReset(FamilyId, AdminId, Guid.NewGuid(), "code", IssuedByMemberId: null, EnUnRato)).ToList();
        Assert.Single(ev);
    }

    [Fact]
    public void Redeeming_a_reset_changes_the_password_and_burns_the_code()
    {
        var rid = Guid.NewGuid();
        var s = ConCredenciales(ConDosMiembros(), MemberId, "cami@x.com");
        s = s.When(FamilyCommandService.IssueReset(s, [], new IssuePasswordReset(FamilyId, MemberId, rid, "code", AdminId, EnUnRato)).Single());

        foreach (var e in FamilyCommandService.RedeemReset(s, [], new RedeemPasswordReset(FamilyId, rid, OtroHash, Now)))
            s = s.When(e);

        Assert.Equal(OtroHash, s.Members.Single(m => m.MemberId == MemberId).PasswordHash);
        Assert.True(s.PasswordResets.Single().Redeemed);
    }

    [Fact]
    public void A_reset_code_works_only_once()
    {
        var rid = Guid.NewGuid();
        var s = ConCredenciales(ConDosMiembros(), MemberId, "cami@x.com");
        s = s.When(FamilyCommandService.IssueReset(s, [], new IssuePasswordReset(FamilyId, MemberId, rid, "code", AdminId, EnUnRato)).Single());
        foreach (var e in FamilyCommandService.RedeemReset(s, [], new RedeemPasswordReset(FamilyId, rid, OtroHash, Now)))
            s = s.When(e);

        var ex = Assert.Throws<DomainException>(() => FamilyCommandService.RedeemReset(s, [],
            new RedeemPasswordReset(FamilyId, rid, Hash, Now)).ToList());
        Assert.Contains("ya fue utilizado", ex.Message);
    }

    [Fact]
    public void An_expired_reset_code_is_rejected()
    {
        var rid = Guid.NewGuid();
        var s = ConCredenciales(ConDosMiembros(), MemberId, "cami@x.com");
        s = s.When(FamilyCommandService.IssueReset(s, [], new IssuePasswordReset(FamilyId, MemberId, rid, "code", AdminId, HaceUnRato)).Single());

        var ex = Assert.Throws<DomainException>(() => FamilyCommandService.RedeemReset(s, [],
            new RedeemPasswordReset(FamilyId, rid, OtroHash, Now)).ToList());
        Assert.Contains("vencido", ex.Message);
    }

    [Fact]
    public void Redeeming_a_reset_also_kicks_every_device_out()
    {
        // Si te resetearon porque perdiste el acceso, el que estaba adentro tiene que salir.
        var rid = Guid.NewGuid();
        var s = ConCredenciales(ConDosMiembros(), MemberId, "cami@x.com");
        s = s.When(FamilyCommandService.IssueSession(s, [], new IssueMemberSession(FamilyId, MemberId, Guid.NewGuid(), "robado", "Celu")).Single());
        s = s.When(FamilyCommandService.IssueReset(s, [], new IssuePasswordReset(FamilyId, MemberId, rid, "code", AdminId, EnUnRato)).Single());

        foreach (var e in FamilyCommandService.RedeemReset(s, [], new RedeemPasswordReset(FamilyId, rid, OtroHash, Now)))
            s = s.When(e);

        Assert.All(s.Sessions.Where(x => x.MemberId == MemberId), x => Assert.True(x.Revoked));
    }

    [Fact]
    public void An_agent_key_can_never_touch_accounts()
    {
        // El principal de un agent key es un KeyId, nunca un MemberId → los guards fallan solos, sin código extra.
        var s = ConDosMiembros();
        var keyId = Guid.NewGuid();
        s = s.When(FamilyCommandService.IssueAgentKey(s, [], new IssueFamilyAgentKey(FamilyId, keyId, "AngelNaira", "k", AdminId)).Single());

        Assert.Throws<DomainException>(() => FamilyCommandService.SetCredentials(s, [],
            new SetMemberCredentials(FamilyId, keyId, "agente@x.com", Hash)).ToList());
        Assert.Throws<DomainException>(() => FamilyCommandService.IssueReset(s, [],
            new IssuePasswordReset(FamilyId, AdminId, Guid.NewGuid(), "code", keyId, EnUnRato)).ToList());
        Assert.Throws<DomainException>(() => FamilyCommandService.IssueSession(s, [],
            new IssueMemberSession(FamilyId, keyId, Guid.NewGuid(), "t", "x")).ToList());
    }
}
