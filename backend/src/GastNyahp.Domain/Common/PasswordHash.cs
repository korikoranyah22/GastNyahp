using System.Security.Cryptography;
using Eventuous;

namespace GastNyahp.Domain.Common;

/// <summary>
/// Hashing de CONTRASEÑAS elegidas por humanos (ver <c>docs/DISENO_CUENTAS_LOGIN.md</c> §4.1).
///
/// <para><b>Esto NO es <see cref="SecretHash"/>, y la diferencia no es cosmética.</b> <see cref="SecretHash"/> es
/// SHA-256 pelado y sin salt: correcto para los tokens/códigos del §17, que son 32 bytes aleatorios (256 bits de
/// entropía — la fuerza bruta es inviable y el salt no aportaría nada). Una contraseña humana tiene órdenes de
/// magnitud menos entropía, así que con SHA-256 un atacante con la DB prueba miles de millones por segundo en GPU
/// y la saca de un diccionario en minutos; y sin salt, dos personas con la misma contraseña dan el mismo hash
/// (rainbow tables + se filtra quién comparte contraseña).</para>
///
/// <para><b>Nunca uses <see cref="SecretHash"/> para una contraseña, ni esto para un token.</b></para>
///
/// <para>PBKDF2-HMAC-SHA256 porque viene en el framework: Argon2id sería preferible pero arrastra una dependencia
/// nativa. Las iteraciones viajan DENTRO del hash, así que subir el costo mañana no invalida los hashes de hoy —
/// <see cref="Verify"/> usa las del hash guardado, no la constante actual.</para>
/// </summary>
public static class PasswordHash
{
    const string Algorithm = "pbkdf2-sha256";
    const int SaltBytes = 16;
    const int HashBytes = 32;
    const char Separator = '$';

    /// <summary>Recomendación OWASP 2023 para PBKDF2-HMAC-SHA256. Subirla no rompe los hashes ya guardados.</summary>
    public const int DefaultIterations = 600_000;

    /// <summary>
    /// Hashea una contraseña. Formato: <c>pbkdf2-sha256$&lt;iteraciones&gt;$&lt;salt-b64&gt;$&lt;hash-b64&gt;</c>.
    /// Cada llamada usa un salt nuevo, así que la misma contraseña NUNCA da el mismo string.
    /// </summary>
    public static string Compute(string password, int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(password)) throw new DomainException("PasswordHash: la contraseña no puede estar vacía.");
        if (iterations <= 0) throw new DomainException("PasswordHash: las iteraciones tienen que ser mayores a 0.");

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
        return string.Join(Separator, Algorithm, iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Verifica una contraseña contra un hash guardado. Nunca tira: un hash corrupto/desconocido es simplemente
    /// "no coincide" (que el login se caiga con 500 ante una fila rara sería peor que negar el acceso).
    /// </summary>
    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash)) return false;

        var parts = storedHash.Split(Separator);
        if (parts.Length != 4 || parts[0] != Algorithm) return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException) { return false; }
        if (salt.Length == 0 || expected.Length == 0) return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        // Tiempo constante: comparar con == cortaría en el primer byte distinto y filtraría el hash byte a byte.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// Quema el mismo CPU que un <see cref="Verify"/> real, y siempre devuelve false. Se usa cuando el email NO
    /// existe (amenaza #3 del diseño): si ahí respondiéramos al toque, el tiempo de respuesta delataría qué emails
    /// están registrados aunque el mensaje de error sea idéntico. El login tiene que tardar lo mismo siempre.
    /// </summary>
    public static bool VerifyDummy(string password)
    {
        _ = Rfc2898DeriveBytes.Pbkdf2(
            password ?? "", DummySalt, DefaultIterations, HashAlgorithmName.SHA256, HashBytes);
        return false;
    }

    // Salt fijo a propósito: no protege nada (no hay contraseña real detrás), solo hace el trabajo.
    static readonly byte[] DummySalt = new byte[SaltBytes];
}
