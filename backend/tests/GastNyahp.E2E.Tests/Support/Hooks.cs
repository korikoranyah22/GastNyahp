using Reqnroll;

namespace GastNyahp.E2E.Tests.Support;

[Binding]
public sealed class Hooks(E2EWorld world)
{
    /// <summary>Every scenario starts inside its own fresh family, already authenticated — business features
    /// never have to mention credentials. The Familias feature tests the access rules explicitly via
    /// AnonymousClient and second families.</summary>
    [BeforeScenario]
    public Task BeforeScenario() => world.BootstrapDefaultFamilyAsync();
}
