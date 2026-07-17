using System.Collections.Concurrent;

namespace GastNyahp.Infrastructure.Services;

/// <summary>
/// Backoff exponencial de los intentos de login (docs/DISENO_CUENTAS_LOGIN.md, amenaza #1).
///
/// <para><b>Sin esto, PBKDF2 no sirve de nada.</b> Las 600k iteraciones encarecen el ataque OFFLINE (con la DB
/// robada), pero no impiden que alguien pruebe 10.000 contraseñas contra el endpoint. Ahí lo único que frena es
/// el rate-limit.</para>
///
/// <para>Se cuenta por <b>email</b> y por <b>IP</b>, y gana el más estricto: por email solo, un atacante rota IPs;
/// por IP sola, prueba emails distintos desde una misma.</para>
///
/// <para>En memoria a propósito: la app corre como un solo backend (docker compose). Si algún día escala a varias
/// réplicas, esto tiene que pasar a Redis o el atacante rota entre instancias — está anotado en el diseño.</para>
///
/// <para>Además tapa un DoS: cada intento cuesta ~100ms de CPU (§7 del diseño). Sin tope, 50 requests concurrentes
/// clavan el backend.</para>
/// </summary>
public sealed class LoginThrottle
{
    /// <summary>Intentos fallidos antes de empezar a frenar. Los primeros son gratis: la gente se equivoca.</summary>
    const int Gratis = 5;
    static readonly TimeSpan TopeDeEspera = TimeSpan.FromMinutes(15);
    /// <summary>Los fallos viejos se olvidan: quien se equivocó ayer no arranca penalizado hoy.</summary>
    static readonly TimeSpan Memoria = TimeSpan.FromHours(1);

    readonly ConcurrentDictionary<string, Estado> _intentos = new();

    sealed class Estado
    {
        public int Fallos;
        public DateTime BloqueadoHasta;
        public DateTime UltimoFallo;
    }

    /// <summary>Segundos que faltan para poder reintentar, o null si puede pasar.</summary>
    public int? SegundosBloqueado(string clave)
    {
        if (!_intentos.TryGetValue(Normalizar(clave), out var e)) return null;
        var falta = e.BloqueadoHasta - DateTime.UtcNow;
        return falta > TimeSpan.Zero ? (int)Math.Ceiling(falta.TotalSeconds) : null;
    }

    /// <summary>El más estricto de las claves dadas (típico: email e IP).</summary>
    public int? SegundosBloqueado(params string[] claves) =>
        claves.Select(SegundosBloqueado).Where(s => s is not null).DefaultIfEmpty(null).Max();

    public void RegistrarFallo(params string[] claves)
    {
        foreach (var clave in claves)
        {
            var e = _intentos.GetOrAdd(Normalizar(clave), _ => new Estado());
            lock (e)
            {
                if (DateTime.UtcNow - e.UltimoFallo > Memoria) e.Fallos = 0;   // se enfrió: arranca de nuevo
                e.Fallos++;
                e.UltimoFallo = DateTime.UtcNow;
                if (e.Fallos <= Gratis) continue;

                // 6º fallo → 2s, 7º → 4s, 8º → 8s… hasta el tope. Un ataque de diccionario se vuelve inviable
                // enseguida, y el que se equivocó de verdad espera unos segundos.
                var espera = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(e.Fallos - Gratis, 20)));
                if (espera > TopeDeEspera) espera = TopeDeEspera;
                e.BloqueadoHasta = DateTime.UtcNow + espera;
            }
        }
    }

    /// <summary>Login exitoso: se borra el historial de esas claves.</summary>
    public void RegistrarExito(params string[] claves)
    {
        foreach (var clave in claves) _intentos.TryRemove(Normalizar(clave), out _);
    }

    static string Normalizar(string clave) => clave.Trim().ToLowerInvariant();
}
