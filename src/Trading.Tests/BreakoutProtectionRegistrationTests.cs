using Microsoft.Extensions.DependencyInjection;
using Trading.Worker;
using Trading.Worker.Extensions;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// StrategyManager takes BreakoutProtectionService in its constructor. When the service was not registered, resolving
/// StrategyManager threw on every SignalWorker cycle and no signal could be generated. The service holds per-session
/// breakout state, so it must also be a singleton: a scoped instance would restart its state machine every cycle.
/// </summary>
public class BreakoutProtectionRegistrationTests
{
    [Fact]
    public void AddSignalEngine_registersBreakoutProtectionService_asSingleton()
    {
        var services = new ServiceCollection().AddSignalEngine();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(BreakoutProtectionService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void StrategyManager_isRegistered_alongsideItsBreakoutDependency()
    {
        var services = new ServiceCollection().AddSignalEngine();

        Assert.Contains(services, d => d.ServiceType == typeof(StrategyManager));
        Assert.Contains(services, d => d.ServiceType == typeof(BreakoutProtectionService));
    }
}
