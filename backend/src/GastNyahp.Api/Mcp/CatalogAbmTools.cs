using System.ComponentModel;
using GastNyahp.Api.Auth;
using GastNyahp.Domain.Cards;
using GastNyahp.Infrastructure.Services;
using ModelContextProtocol.Server;

namespace GastNyahp.Api.Mcp;

/// <summary>
/// ABM del CATÁLOGO por conversación: bancos, tarjetas y personas — las entidades que el resto de las tools
/// referencian POR NOMBRE ("pagué con la Visa Galicia", "el shampoo es de Meli"). Sin esto el diálogo se corta:
/// el agente resuelve el nombre, no existe, y no tiene forma de crearlo sin abrir la app.
///
/// <para>Separado de <see cref="GastNyahpTools"/> (consulta) y <see cref="DraftTools"/> (carga del changuito): son
/// operaciones de SETUP, esporádicas, no algo que se haga en la fila del super.</para>
///
/// <para><b>Nada acá borra.</b> Para sacar algo de circulación se desactiva (tarjetas) o se archiva (personas):
/// reversible. El DELETE real vive solo en la UI — un borrado por un audio mal transcripto no se deshace, y
/// arrastra los datos asociados.</para>
/// </summary>
[McpServerToolType]
public sealed class CatalogAbmTools
{
    // ── Bancos ───────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "banco_crear")]
    [Description("Da de alta un banco de la familia. Necesario antes de crear una tarjeta (toda tarjeta pertenece a un banco) o de pagar con débito.")]
    public static async Task<string> BancoCrear(
        BankService banks,
        IHttpContextAccessor http,
        [Description("Nombre del banco, ej. 'Galicia'.")] string nombre,
        [Description("Alias corto, opcional.")] string? alias = null,
        [Description("Color hex, ej. '#004B9B'. Si se omite se usa un gris neutro.")] string? color = null,
        [Description("Icono (lucide), ej. 'building-2'. Si se omite se usa 'building-2'.")] string? icono = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        if (string.IsNullOrWhiteSpace(nombre)) return "Error: el nombre del banco es obligatorio.";

        var existente = (await banks.GetAllAsync(familyId)).FirstOrDefault(b => b.Name.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existente is not null) return $"El banco '{existente.Name}' ya existe — no hace falta crearlo de nuevo.";

        var result = await banks.RegisterAsync(familyId, nombre.Trim(), alias, color ?? "#64748B", icono ?? "building-2");
        return result.Ok ? $"Banco '{nombre.Trim()}' creado." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "banco_editar")]
    [Description("Edita un banco existente (nombre, alias, color o icono), identificándolo por su nombre actual.")]
    public static async Task<string> BancoEditar(
        BankService banks,
        IHttpContextAccessor http,
        [Description("Nombre ACTUAL del banco (usar bancos_listar).")] string nombre,
        [Description("Nombre nuevo; si se omite, no cambia.")] string? nombreNuevo = null,
        [Description("Alias nuevo; si se omite, no cambia.")] string? alias = null,
        [Description("Color hex nuevo; si se omite, no cambia.")] string? color = null,
        [Description("Icono nuevo; si se omite, no cambia.")] string? icono = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var bank = (await banks.GetAllAsync(familyId)).FirstOrDefault(b => b.Name.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (bank is null) return $"Error: no existe un banco llamado '{nombre}'. Usá bancos_listar.";

        var result = await banks.UpdateAsync(familyId, bank.Id,
            nombreNuevo?.Trim() ?? bank.Name, alias ?? bank.Alias, color ?? bank.Color, icono ?? bank.Icon);
        return result.Ok ? $"Banco '{nombreNuevo?.Trim() ?? bank.Name}' actualizado." : $"Error: {result.Error}";
    }

    // ── Tarjetas ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "tarjeta_crear")]
    [Description("Da de alta una tarjeta de la familia, asociada a un banco existente. Necesario para poder pagar con esa tarjeta o cargar compras en cuotas.")]
    public static async Task<string> TarjetaCrear(
        CardService cards,
        BankService banks,
        IHttpContextAccessor http,
        [Description("Nombre de la tarjeta, ej. 'Visa Galicia'. Es el que se usa después para pagar.")] string nombre,
        [Description("Nombre exacto del banco al que pertenece (usar bancos_listar; si no existe, banco_crear).")] string banco,
        [Description("Red: 'Visa' o 'Mastercard'.")] string red,
        [Description("Tipo: 'Credito' o 'Debito'.")] string tipo,
        [Description("Día del mes en que cierra el resumen (1-31).")] int diaCierre,
        [Description("Día del mes en que vence el resumen (1-31).")] int diaVencimiento,
        [Description("Color hex, opcional.")] string? color = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        if (string.IsNullOrWhiteSpace(nombre)) return "Error: el nombre de la tarjeta es obligatorio.";

        var bank = (await banks.GetAllAsync(familyId)).FirstOrDefault(b => b.Name.Equals(banco.Trim(), StringComparison.OrdinalIgnoreCase));
        if (bank is null) return $"Error: no existe un banco llamado '{banco}'. Usá bancos_listar, o crealo con banco_crear.";

        var (network, redError) = ParseRed(red);
        if (redError is not null) return redError;
        var (cardType, tipoError) = ParseTipo(tipo);
        if (tipoError is not null) return tipoError;
        if (diaCierre is < 1 or > 31) return "Error: el día de cierre tiene que estar entre 1 y 31.";
        if (diaVencimiento is < 1 or > 31) return "Error: el día de vencimiento tiene que estar entre 1 y 31.";

        var existente = (await cards.GetAllAsync(familyId)).FirstOrDefault(c => c.Label.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existente is not null) return $"La tarjeta '{existente.Label}' ya existe — no hace falta crearla de nuevo.";

        var result = await cards.RegisterAsync(familyId, bank.Id, nombre.Trim(), network!.Value, cardType!.Value,
            diaCierre, diaVencimiento, color ?? "#64748B");
        return result.Ok
            ? $"Tarjeta '{nombre.Trim()}' creada en {bank.Name} — cierra el {diaCierre}, vence el {diaVencimiento}."
            : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "tarjeta_editar")]
    [Description("Edita una tarjeta existente (nombre, red, tipo, días de cierre/vencimiento o color), identificándola por su nombre actual.")]
    public static async Task<string> TarjetaEditar(
        CardService cards,
        IHttpContextAccessor http,
        [Description("Nombre ACTUAL de la tarjeta (usar tarjetas_listar).")] string nombre,
        [Description("Nombre nuevo; si se omite, no cambia.")] string? nombreNuevo = null,
        [Description("Red nueva: 'Visa' o 'Mastercard'; si se omite, no cambia.")] string? red = null,
        [Description("Tipo nuevo: 'Credito' o 'Debito'; si se omite, no cambia.")] string? tipo = null,
        [Description("Día de cierre nuevo (1-31); si se omite, no cambia.")] int? diaCierre = null,
        [Description("Día de vencimiento nuevo (1-31); si se omite, no cambia.")] int? diaVencimiento = null,
        [Description("Color hex nuevo; si se omite, no cambia.")] string? color = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var card = (await cards.GetAllAsync(familyId)).FirstOrDefault(c => c.Label.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (card is null) return $"Error: no existe una tarjeta llamada '{nombre}'. Usá tarjetas_listar.";

        var network = card.Network;
        if (red is not null)
        {
            var (parsed, error) = ParseRed(red);
            if (error is not null) return error;
            network = parsed!.Value.ToString();
        }
        var cardType = card.Type;
        if (tipo is not null)
        {
            var (parsed, error) = ParseTipo(tipo);
            if (error is not null) return error;
            cardType = parsed!.Value.ToString();
        }
        if (diaCierre is < 1 or > 31) return "Error: el día de cierre tiene que estar entre 1 y 31.";
        if (diaVencimiento is < 1 or > 31) return "Error: el día de vencimiento tiene que estar entre 1 y 31.";

        var result = await cards.UpdateAsync(familyId, card.Id,
            nombreNuevo?.Trim() ?? card.Label,
            Enum.Parse<CardNetwork>(network), Enum.Parse<CardType>(cardType),
            diaCierre ?? card.ClosingDay, diaVencimiento ?? card.DueDay, color ?? card.Color);
        return result.Ok ? $"Tarjeta '{nombreNuevo?.Trim() ?? card.Label}' actualizada." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "tarjeta_desactivar")]
    [Description("Desactiva una tarjeta: deja de estar disponible para pagar, pero NO se borra y su historial queda intacto. Es la forma reversible de 'dar de baja' una tarjeta (no existe borrado por conversación).")]
    public static async Task<string> TarjetaDesactivar(
        CardService cards,
        IHttpContextAccessor http,
        [Description("Nombre exacto de la tarjeta (usar tarjetas_listar).")] string nombre)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var card = (await cards.GetAllAsync(familyId)).FirstOrDefault(c => c.Label.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (card is null) return $"Error: no existe una tarjeta llamada '{nombre}'. Usá tarjetas_listar.";
        if (!card.Active) return $"La tarjeta '{card.Label}' ya está inactiva.";

        var result = await cards.DeactivateAsync(familyId, card.Id);
        return result.Ok ? $"Tarjeta '{card.Label}' desactivada — se puede reactivar cuando quieras." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "tarjeta_activar")]
    [Description("Reactiva una tarjeta que estaba desactivada, para volver a poder pagar con ella.")]
    public static async Task<string> TarjetaActivar(
        CardService cards,
        IHttpContextAccessor http,
        [Description("Nombre exacto de la tarjeta (usar tarjetas_listar).")] string nombre)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var card = (await cards.GetAllAsync(familyId)).FirstOrDefault(c => c.Label.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (card is null) return $"Error: no existe una tarjeta llamada '{nombre}'. Usá tarjetas_listar.";
        if (card.Active) return $"La tarjeta '{card.Label}' ya está activa.";

        var result = await cards.ActivateAsync(familyId, card.Id);
        return result.Ok ? $"Tarjeta '{card.Label}' reactivada." : $"Error: {result.Error}";
    }

    // ── Personas ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "persona_crear")]
    [Description("Da de alta una persona de la familia. Necesario para poder decir que un gasto o un ítem del ticket es de esa persona.")]
    public static async Task<string> PersonaCrear(
        PersonService people,
        IHttpContextAccessor http,
        [Description("Nombre de la persona, ej. 'Meli'.")] string nombre,
        [Description("Emoji, opcional.")] string? emoji = null,
        [Description("Color hex, opcional.")] string? color = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        if (string.IsNullOrWhiteSpace(nombre)) return "Error: el nombre de la persona es obligatorio.";

        var existente = (await people.GetAllAsync(familyId, includeArchived: true))
            .FirstOrDefault(p => p.Name.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existente is not null)
            return existente.Archived
                ? $"'{existente.Name}' ya existe pero está archivada — no la crees de nuevo."
                : $"'{existente.Name}' ya existe — no hace falta crearla de nuevo.";

        var result = await people.RegisterAsync(familyId, nombre.Trim(), emoji ?? "", color ?? "#64748B");
        return result.Ok ? $"Persona '{nombre.Trim()}' creada." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "persona_editar")]
    [Description("Edita una persona existente (nombre, emoji o color), identificándola por su nombre actual.")]
    public static async Task<string> PersonaEditar(
        PersonService people,
        IHttpContextAccessor http,
        [Description("Nombre ACTUAL de la persona (usar personas_listar).")] string nombre,
        [Description("Nombre nuevo; si se omite, no cambia.")] string? nombreNuevo = null,
        [Description("Emoji nuevo; si se omite, no cambia.")] string? emoji = null,
        [Description("Color hex nuevo; si se omite, no cambia.")] string? color = null)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var person = (await people.GetAllAsync(familyId)).FirstOrDefault(p => p.Name.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (person is null) return $"Error: no existe una persona llamada '{nombre}'. Usá personas_listar.";

        var result = await people.UpdateAsync(familyId, person.Id,
            nombreNuevo?.Trim() ?? person.Name, emoji ?? person.Emoji, color ?? person.Color);
        return result.Ok ? $"Persona '{nombreNuevo?.Trim() ?? person.Name}' actualizada." : $"Error: {result.Error}";
    }

    [McpServerTool(Name = "persona_archivar")]
    [Description("Archiva una persona: deja de ofrecerse para asignarle gastos, pero NO se borra y su historial queda intacto. Es la forma reversible de 'dar de baja' una persona (no existe borrado por conversación).")]
    public static async Task<string> PersonaArchivar(
        PersonService people,
        IHttpContextAccessor http,
        [Description("Nombre exacto de la persona (usar personas_listar).")] string nombre)
    {
        var familyId = http.HttpContext!.GetFamilyId();
        var person = (await people.GetAllAsync(familyId)).FirstOrDefault(p => p.Name.Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
        if (person is null) return $"Error: no existe una persona activa llamada '{nombre}'. Usá personas_listar.";

        var result = await people.ArchiveAsync(familyId, person.Id);
        return result.Ok ? $"Persona '{person.Name}' archivada — su historial de gastos queda intacto." : $"Error: {result.Error}";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    static (CardNetwork? Value, string? Error) ParseRed(string red) => red.Trim().ToLowerInvariant() switch
    {
        "visa" => (CardNetwork.Visa, null),
        "mastercard" or "master" => (CardNetwork.Mastercard, null),
        _ => (null, $"Error: red '{red}' desconocida (Visa o Mastercard)."),
    };

    static (CardType? Value, string? Error) ParseTipo(string tipo) => tipo.Trim().ToLowerInvariant() switch
    {
        "credito" or "crédito" or "credit" => (CardType.Credit, null),
        "debito" or "débito" or "debit" => (CardType.Debit, null),
        _ => (null, $"Error: tipo '{tipo}' desconocido (Credito o Debito)."),
    };
}
