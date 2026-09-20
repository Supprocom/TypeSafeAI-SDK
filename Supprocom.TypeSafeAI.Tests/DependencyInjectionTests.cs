using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Supprocom.TypeSafeAI.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddTypeSafeAIRegistersInterfaceAndConcreteClient()
    {
        var services = new ServiceCollection();
        services.AddTypeSafeAI(options =>
        {
            options.ApiKey = "di-key";
            options.BaseUrl = new Uri("https://di.test/root");
            options.DefaultModel = "jev-di";
        });
        using var provider = services.BuildServiceProvider();

        var abstraction = provider.GetRequiredService<ITypeSafeClient>();
        var concrete = Assert.IsType<TypeSafeClient>(abstraction);

        Assert.Equal("https://di.test/root/", concrete.BaseUrl.AbsoluteUri);
        Assert.Equal("jev-di", concrete.DefaultModel);
        Assert.IsType<TypeSafeClient>(provider.GetRequiredService<TypeSafeClient>());
    }

    [Fact]
    public void AddTypeSafeAIBindsConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKey"] = "configuration-key",
                ["BaseUrl"] = "https://configuration.test/api",
                ["DefaultModel"] = "jev-configuration",
                ["Timeout"] = "00:00:07",
                ["Retry:MaxRetries"] = "4",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddTypeSafeAI(configuration);
        using var provider = services.BuildServiceProvider();

        var client = Assert.IsType<TypeSafeClient>(provider.GetRequiredService<ITypeSafeClient>());

        Assert.Equal("https://configuration.test/api/", client.BaseUrl.AbsoluteUri);
        Assert.Equal("jev-configuration", client.DefaultModel);
        Assert.Equal(TimeSpan.FromSeconds(7), client.Timeout);
        Assert.Equal(4, client.Retry.MaxRetries);
    }
}
