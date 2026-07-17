using GastNyahp.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GastNyahp.Infrastructure;

public class BusinessDayOptions
{
    public bool Enabled { get; set; } = true;
    /// <summary>Local wall-clock time at which the new business day opens (HH:mm).</summary>
    public string OpenTime { get; set; } = "06:00";
    public string TimeZone { get; set; } = "America/Argentina/Buenos_Aires";
}

/// <summary>
/// The daily heartbeat of the app (DOMAIN_MODEL.md §13.1): opens today's BusinessDay on startup (catch-up
/// after restarts) and then at OpenTime every day. Opening is idempotent per date at the aggregate level.
/// We check <see cref="BusinessDayService.IsOpenAsync"/> first so the common catch-up case (the day is already
/// open after a restart) skips the command entirely — otherwise every boot would append with ExpectedVersion
/// NoStream, get rejected, and log a full optimistic-concurrency stack trace for a completely normal situation.
/// The aggregate guard still protects the rare genuine race between the check and the append.
/// </summary>
public sealed class BusinessDayScheduler(
    BusinessDayService businessDays,
    IOptions<BusinessDayOptions> options,
    ILogger<BusinessDayScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        var openTime = TimeOnly.Parse(options.Value.OpenTime);

        while (!ct.IsCancellationRequested)
        {
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            var today = DateOnly.FromDateTime(nowLocal.DateTime).ToString("yyyy-MM-dd");

            if (await businessDays.IsOpenAsync(today, ct))
            {
                logger.LogDebug("[BusinessDay] Día hábil {Date} ya estaba abierto", today);
            }
            else
            {
                var result = await businessDays.OpenAsync(today, ct);
                if (result.Ok)
                    logger.LogInformation("[BusinessDay] Día hábil {Date} abierto", today);
                else
                    // Perdimos la carrera contra otra instancia entre el check y el append: el día quedó abierto igual.
                    logger.LogDebug("[BusinessDay] Día hábil {Date} ya estaba abierto (carrera)", today);
            }

            var delay = NextDelay(nowLocal, openTime);
            logger.LogDebug("[BusinessDay] Próxima apertura en {Delay}", delay);
            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                return; // clean shutdown
            }
        }
    }

    /// <summary>Time until the NEXT occurrence of openTime strictly after nowLocal (today if still ahead,
    /// otherwise tomorrow). Pure — unit-tested directly.</summary>
    public static TimeSpan NextDelay(DateTimeOffset nowLocal, TimeOnly openTime)
    {
        var todayOpen = nowLocal.Date + openTime.ToTimeSpan();
        var next = nowLocal.DateTime < todayOpen ? todayOpen : todayOpen.AddDays(1);
        return next - nowLocal.DateTime;
    }
}
