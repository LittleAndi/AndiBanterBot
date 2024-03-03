using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Common;

public interface IConfigurationOptions
{
    static abstract string SectionName { get; }
}

public static class ConfigurationOptionsExtensions
{
    public static IServiceCollection AddConfigurationOptions<T>(this IServiceCollection services, IConfiguration configuration, out T options) where T : class, IConfigurationOptions
    {
        options = configuration.GetRequiredSection<T>(T.SectionName);
        services.AddSingleton(options.GetType(), options);
        return services;
    }

    public static T GetRequiredSection<T>(this IConfiguration configuration, string sectionName)
    {
        return configuration.GetSectionOrDefault<T>(sectionName) ?? throw new Exception($"Unable to parse '{sectionName}' from configuration");
    }

    public static T? GetSectionOrDefault<T>(this IConfiguration configuration, string sectionName)
    {
        return configuration.GetSection(sectionName).Get<T>();
    }
}
