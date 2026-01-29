using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Forklift.Tests;

public class HealthTests
{
    [Fact]
    public async Task UpAndRunning()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Forklift>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        var resourceNotificationService =
            app.Services.GetRequiredService<ResourceNotificationService>();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        // Wait for all resources to be running
        var resources = new[] { "postgres", "mongo", "redis", "rabbitmq", "eventstore", "unleash" };

        foreach (var resource in resources)
        {
            await resourceNotificationService.WaitForResourceAsync(
                resource,
                KnownResourceStates.Running,
                cts.Token
            );
        }
    }
}
