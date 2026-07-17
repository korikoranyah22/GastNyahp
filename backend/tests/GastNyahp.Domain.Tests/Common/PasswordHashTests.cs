using System.Diagnostics;
using System.Security.Cryptography;
using Eventuous;
using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Tests.Common;

/// <summary>
/// PasswordHash es la pieza de seguridad de las cuentas (docs/DISENO_CUENTAS_LOGIN.md §4.1). Lo que importa
/// verificar no es "hashea y verifica" sino los invariantes que lo hacen seguro: que saltee, que no acepte
/// basura, que respete las iteraciones guardadas, y que no se confunda con SecretHash.
///
/// Las iteraciones reales (600k) tardan ~100ms a propósito: los tests que no miden costo usan menos para no
/// tardar una eternidad, pero los que verifican el default sí lo pagan.
/// </summary>
public class PasswordHashTests
{
    const int Fast = 1_000;   // solo para tests que no miden el costo

    [Fact]
    public void Verify_accepts_the_right_password()
    {
        var hash = PasswordHash.Compute("una contraseña larga", Fast);
        Assert.True(PasswordHash.Verify("una contraseña larga", hash));
    }

    [Fact]
    public void Verify_rejects_the_wrong_password()
    {
        var hash = PasswordHash.Compute("una contraseña larga", Fast);
        Assert.False(PasswordHash.Verify("una contraseña larga ", hash));   // un espacio de más
        Assert.False(PasswordHash.Verify("Una contraseña larga", hash));    // una mayúscula
        Assert.False(PasswordHash.Verify("", hash));
    }

    [Fact]
    public void The_same_password_never_produces_the_same_hash()
    {
        // El salt es por contraseña: sin esto, dos personas con la misma clave darían el mismo hash
        // (rainbow tables, y se filtra quién comparte contraseña).
        var a = PasswordHash.Compute("misma", Fast);
        var b = PasswordHash.Compute("misma", Fast);

        Assert.NotEqual(a, b);
        Assert.True(PasswordHash.Verify("misma", a));
        Assert.True(PasswordHash.Verify("misma", b));   // ambos verifican igual
    }

    [Fact]
    public void The_hash_never_contains_the_password()
    {
        var hash = PasswordHash.Compute("mi-contraseña-secreta", Fast);
        Assert.DoesNotContain("mi-contraseña-secreta", hash);
    }

    [Fact]
    public void The_format_carries_algorithm_and_iterations()
    {
        // Las iteraciones viajan adentro para poder subirlas sin invalidar los hashes viejos.
        var hash = PasswordHash.Compute("x1234567890", 12_345);
        var parts = hash.Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal("12345", parts[1]);
        Assert.NotEmpty(Convert.FromBase64String(parts[2]));   // salt
        Assert.NotEmpty(Convert.FromBase64String(parts[3]));   // hash
    }

    [Fact]
    public void Verify_uses_the_iterations_from_the_stored_hash_not_the_current_default()
    {
        // El invariante que hace posible subir el costo mañana: un hash viejo (pocas iteraciones) tiene que
        // seguir verificando aunque DefaultIterations haya cambiado.
        var viejo = PasswordHash.Compute("contraseña vieja", 1_000);
        Assert.True(PasswordHash.Verify("contraseña vieja", viejo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("cualquier cosa")]                       // no tiene el formato
    [InlineData("md5$1000$c2FsdA==$aGFzaA==")]           // algoritmo desconocido
    [InlineData("pbkdf2-sha256$abc$c2FsdA==$aGFzaA==")]  // iteraciones no numéricas
    [InlineData("pbkdf2-sha256$0$c2FsdA==$aGFzaA==")]    // iteraciones inválidas
    [InlineData("pbkdf2-sha256$1000$no-es-base64$aGFzaA==")]
    [InlineData("pbkdf2-sha256$1000$c2FsdA==")]          // faltan partes
    public void Verify_never_throws_on_a_broken_hash(string? stored)
    {
        // Una fila corrupta tiene que negar el acceso, no tumbar el login con un 500.
        Assert.False(PasswordHash.Verify("cualquiera", stored!));
    }

    [Fact]
    public void Compute_rejects_an_empty_password()
    {
        Assert.Throws<DomainException>(() => PasswordHash.Compute(""));
        Assert.Throws<DomainException>(() => PasswordHash.Compute(null!));
    }

    [Fact]
    public void A_SecretHash_is_not_a_valid_PasswordHash()
    {
        // Blindaje contra el error más fácil de cometer: guardar una contraseña con SecretHash. Si algún día
        // alguien lo hace, Verify devuelve false (no la deja entrar) en vez de aceptar un SHA-256 pelado.
        var conSecretHash = SecretHash.Compute("mi-contraseña");
        Assert.False(PasswordHash.Verify("mi-contraseña", conSecretHash));
    }

    [Fact]
    public void The_default_iterations_meet_the_OWASP_floor()
    {
        // Si alguien baja esto "porque tarda", que rompa un test y tenga que justificarlo.
        Assert.True(PasswordHash.DefaultIterations >= 600_000);
    }

    [Fact]
    public void VerifyDummy_always_fails_but_costs_the_same_as_a_real_verify()
    {
        // Amenaza #3: sin esto, un email inexistente respondería al toque y delataría qué emails están
        // registrados, aunque el mensaje de error sea idéntico.
        var real = PasswordHash.Compute("contraseña real");   // con las iteraciones default (caro a propósito)

        var swReal = Stopwatch.StartNew();
        PasswordHash.Verify("contraseña mala", real);
        swReal.Stop();

        var swDummy = Stopwatch.StartNew();
        var result = PasswordHash.VerifyDummy("contraseña mala");
        swDummy.Stop();

        Assert.False(result);
        // Mismo orden de magnitud. Cota generosa: en CI el reloj es ruidoso y no queremos un test flaky —
        // lo que cazamos es que alguien convierta VerifyDummy en un `return false` instantáneo.
        Assert.InRange(swDummy.Elapsed.TotalMilliseconds, swReal.Elapsed.TotalMilliseconds * 0.3, swReal.Elapsed.TotalMilliseconds * 3.0);
    }

    [Fact]
    public void Round_trip_with_the_real_cost_works()
    {
        // El default de producción, pagando los ~100ms: que 600k iteraciones no rompan nada.
        var hash = PasswordHash.Compute("contraseña de producción");
        Assert.True(PasswordHash.Verify("contraseña de producción", hash));
        Assert.False(PasswordHash.Verify("otra", hash));
    }

    [Fact]
    public void Matches_a_known_PBKDF2_vector()
    {
        // Verifica que Compute realmente hace PBKDF2-HMAC-SHA256 con el salt y las iteraciones que dice —
        // no algún otro derivado. Reproducimos el hash a mano desde las partes del string.
        var hash = PasswordHash.Compute("contraseña conocida", 2_048);
        var parts = hash.Split('$');
        var salt = Convert.FromBase64String(parts[2]);
        var stored = Convert.FromBase64String(parts[3]);

        var esperado = Rfc2898DeriveBytes.Pbkdf2("contraseña conocida", salt, 2_048, HashAlgorithmName.SHA256, 32);

        Assert.Equal(esperado, stored);
        Assert.Equal(32, stored.Length);
        Assert.Equal(16, salt.Length);
    }
}
