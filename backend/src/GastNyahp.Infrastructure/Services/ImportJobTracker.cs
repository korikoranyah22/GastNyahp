using System.Collections.Concurrent;

namespace GastNyahp.Infrastructure.Services;

public enum ImportJobStatus { Running, Completed, Failed }

/// <summary>Avance de la importación, por sección ("Cuotas", "Movimientos"…) con contadores de ítems.</summary>
public sealed record ImportProgress(string Section, int Done, int Total);

public sealed record ImportJobState(
    ImportJobStatus Status,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    ImportProgress? Progress,
    ImportSummary? Summary,
    string? Error);

/// <summary>
/// Estado en memoria de la importación por familia: el POST /api/import arranca el job en background (no
/// atado al request — un F5 o timeout del cliente no lo cancela) y la UI hace polling de GET /api/import/status.
/// En memoria a propósito: una sola instancia del backend, y si el proceso se reinicia a mitad de camino el
/// estado vuelve a "idle" — el guard de familia-con-datos + force sigue siendo la red de contención.
/// </summary>
public sealed class ImportJobTracker
{
    readonly ConcurrentDictionary<Guid, ImportJobState> _jobs = new();

    public ImportJobState? Get(Guid familyId) => _jobs.GetValueOrDefault(familyId);

    /// <summary>Marca la familia como importando; false si ya hay un job corriendo (un import a la vez).</summary>
    public bool TryStart(Guid familyId)
    {
        var fresh = new ImportJobState(ImportJobStatus.Running, DateTime.UtcNow, null, null, null, null);
        var current = _jobs.AddOrUpdate(familyId, fresh,
            (_, existing) => existing.Status == ImportJobStatus.Running ? existing : fresh);
        return ReferenceEquals(current, fresh);
    }

    public void Report(Guid familyId, ImportProgress progress) =>
        Update(familyId, s => s with { Progress = progress });

    public void Complete(Guid familyId, ImportSummary summary) =>
        Update(familyId, s => s with { Status = ImportJobStatus.Completed, FinishedAtUtc = DateTime.UtcNow, Summary = summary });

    public void Fail(Guid familyId, string error) =>
        Update(familyId, s => s with { Status = ImportJobStatus.Failed, FinishedAtUtc = DateTime.UtcNow, Error = error });

    void Update(Guid familyId, Func<ImportJobState, ImportJobState> mutate)
    {
        while (_jobs.TryGetValue(familyId, out var current) && !_jobs.TryUpdate(familyId, mutate(current), current)) { }
    }
}
