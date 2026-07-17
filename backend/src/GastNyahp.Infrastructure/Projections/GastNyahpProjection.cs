using System.Reflection;
using Eventuous;
using GastNyahp.Domain.Banks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using EventHandler = Eventuous.Subscriptions.EventHandler;

namespace GastNyahp.Infrastructure.Projections;

/// <summary>
/// Base for all GastNyahp read-model projections. The static ctor registers every [EventType] of the Domain
/// assembly in the global TypeMap BEFORE any derived ctor runs its On&lt;T&gt; registrations — EventHandler
/// consults the type map when wiring handlers.
/// </summary>
public abstract class GastNyahpProjection : EventHandler
{
    static GastNyahpProjection() => TypeMap.RegisterKnownEventTypes(typeof(BankEvents).Assembly);

    /// <summary>
    /// Saves, swallowing the Postgres 23505 unique_violation. A projection may see the same insert twice
    /// (synchronous read-your-writes + the $all subscription replay) — an unswallowed 23505 would wedge the
    /// shared subscription checkpoint forever. See eventuous-projection-readmodel.
    /// </summary>
    protected static async Task SaveIgnoringDuplicate(DbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Inserted concurrently by the other path — already projected, nothing to do.
        }
    }
}

/// <summary>Extracts the aggregate id from a stream name ("bank-{guid}" → guid). Events that are pure state
/// transitions (BankUpdated, CardActivated, ...) don't repeat the aggregate id — the stream IS the identity.</summary>
public static class StreamIds
{
    public static Guid GuidFrom(StreamName stream, string prefix)
    {
        var value = stream.ToString();
        if (!value.StartsWith(prefix + "-", StringComparison.Ordinal))
            throw new InvalidOperationException($"Stream '{value}' does not match prefix '{prefix}-'.");
        return Guid.Parse(value.AsSpan(prefix.Length + 1));
    }
}
