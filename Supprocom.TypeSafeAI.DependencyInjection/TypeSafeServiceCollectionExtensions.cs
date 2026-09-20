using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the Supprocom TypeSafe AI client with dependency injection.</summary>
public static class TypeSafeServiceCollectionExtensions
{
    /// <summary>The named <see cref="HttpClient"/> registration used by the integration.</summary>
    public const string HttpClientName = "Supprocom.TypeSafeAI";

    /// <summary>Registers TypeSafe AI using environment variables and SDK defaults.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddTypeSafeAI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return AddCore(services);
    }

    /// <summary>Registers TypeSafe AI with code-based configuration.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configure">Configures the TypeSafe client.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddTypeSafeAI(
        this IServiceCollection services,
        Action<Supprocom.TypeSafeAI.TypeSafeClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        AddCore(services);
        services.Configure(configure);
        return services;
    }

    /// <summary>Registers TypeSafe AI and binds options from a configuration section.</summary>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configuration">The configuration section to bind.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddTypeSafeAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        AddCore(services);
        services.AddOptions<Supprocom.TypeSafeAI.TypeSafeClientOptions>().Bind(configuration);
        return services;
    }

    private static IServiceCollection AddCore(IServiceCollection services)
    {
        services.AddOptions<Supprocom.TypeSafeAI.TypeSafeClientOptions>();
        services
            .AddHttpClient(HttpClientName, static client =>
                client.Timeout = System.Threading.Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
            });

        services.TryAddTransient(static provider =>
        {
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            return new Supprocom.TypeSafeAI.TypeSafeClient(
                httpClient,
                provider.GetRequiredService<IOptions<Supprocom.TypeSafeAI.TypeSafeClientOptions>>().Value,
                provider.GetService<ILogger<Supprocom.TypeSafeAI.TypeSafeClient>>(),
                disposeHttpClient: true);
        });
        services.TryAddTransient<Supprocom.TypeSafeAI.ITypeSafeClient>(static provider =>
            provider.GetRequiredService<Supprocom.TypeSafeAI.TypeSafeClient>());
        return services;
    }
}
