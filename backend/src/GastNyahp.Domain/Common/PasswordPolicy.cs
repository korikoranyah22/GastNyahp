namespace GastNyahp.Domain.Common;

/// <summary>
/// Qué contraseña se acepta (docs/DISENO_CUENTAS_LOGIN.md, amenaza #4). Criterio NIST 800-63B: <b>largo y lista
/// de prohibidas, NO reglas de composición</b>.
///
/// <para>Las reglas tipo "una mayúscula, un número y un símbolo" empujan a la gente a <c>Password1!</c> —
/// cumplen la regla y están en cualquier diccionario. El largo es lo que agrega entropía de verdad, y la lista
/// de prohibidas mata las que un atacante prueba primero.</para>
///
/// <para>Tampoco hay tope bajo de largo ni prohibición de espacios: una passphrase larga es lo mejor que puede
/// elegir un usuario y sería absurdo rechazarla.</para>
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;
    public const int MaxLength = 200;   // solo para que un "password" de 1MB no nos cueste un PBKDF2 eterno

    /// <summary>
    /// Las más usadas del mundo real (filtraciones tipo rockyou/HIBP). Es una muestra corta a propósito: la
    /// defensa fuerte es el largo mínimo + el rate-limit. Si algún día se quiere la lista completa de 10k, va
    /// como recurso embebido y se cambia solo <see cref="EsComun"/>.
    /// </summary>
    static readonly HashSet<string> Comunes = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "passw0rd", "contraseña", "contrasena", "contraseña1",
        "123456", "1234567", "12345678", "123456789", "1234567890", "12345678910", "0123456789",
        "qwerty", "qwerty123", "qwertyuiop", "asdfghjkl", "1q2w3e4r", "1qaz2wsx", "zaq12wsx",
        "iloveyou", "princess", "sunshine", "welcome", "welcome1", "admin", "admin123", "administrator",
        "letmein", "monkey", "dragon", "football", "baseball", "superman", "batman", "trustno1",
        "abc123", "abcd1234", "a1b2c3d4", "michael", "jennifer", "jordan23",
        "boca juniors", "riverplate", "river plate", "argentina", "argentina1", "buenosaires",
        "gastnyahp", "gastnyahp123", "familia", "familia123", "casa1234", "hola1234", "holamundo",
    };

    /// <summary>null = la contraseña sirve. Si no, el motivo, listo para mostrarle al usuario.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "La contraseña es obligatoria.";
        if (password.Length < MinLength)
            return $"La contraseña tiene que tener al menos {MinLength} caracteres.";
        if (password.Length > MaxLength)
            return $"La contraseña no puede superar los {MaxLength} caracteres.";
        if (EsComun(password))
            return "Esa contraseña es demasiado común — elegí otra.";
        return null;
    }

    /// <summary>
    /// Compara ignorando mayúsculas y también sin los dígitos del final: <c>Password2024</c> es tan mala como
    /// <c>password</c>, y agregarle el año es justo lo que hace todo el mundo cuando le piden "un número".
    /// </summary>
    static bool EsComun(string password)
    {
        if (Comunes.Contains(password)) return true;
        var sinDigitosFinales = password.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '!', '.', '*');
        return sinDigitosFinales.Length >= 4 && Comunes.Contains(sinDigitosFinales);
    }
}
