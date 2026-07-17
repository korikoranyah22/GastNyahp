using Eventuous;

namespace GastNyahp.Integration.Tests.Support;

/// <summary>One fresh host (event store + read model) per test — xunit instantiates the class per test case.</summary>
public abstract class IntegrationTest : IAsyncLifetime
{
    protected GastNyahpTestHost Host = null!;

    public Task InitializeAsync()
    {
        Host = new GastNyahpTestHost();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await Host.DisposeAsync();

    /// <summary>Executes a command expecting success, then pumps pending events through all projections.</summary>
    protected async Task Ok<TState>(Task<Result<TState>> handling) where TState : State<TState>, new()
    {
        var result = await handling;
        Assert.True(result.Success, $"Command failed: {result.Exception?.Message}");
        await Host.ProjectPending();
    }

    /// <summary>Executes a command expecting a domain/concurrency failure (still pumps, to prove no stray events leaked).</summary>
    protected async Task Fails<TState>(Task<Result<TState>> handling) where TState : State<TState>, new()
    {
        var result = await handling;
        Assert.False(result.Success, "Command unexpectedly succeeded.");
        await Host.ProjectPending();
    }
}
