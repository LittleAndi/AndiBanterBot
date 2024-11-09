namespace Application.Common;

public interface IConfigurationOptions
{
    static abstract string SectionName { get; }
}

public static class ConfigurationOptionsExtensions
{
    public static IServiceCollection AddConfigurationOptions<T>(this IServiceCollection services, IConfiguration configuration) where T : class, IConfigurationOptions, new()
    {
        // Bind options and add it to the DI container as Singleton
        var section = configuration.GetSection(T.SectionName);
        services.Configure<T>(section);

        // Add IOptionsMonitor to track changes
        services.AddSingleton(sp =>
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<T>>();
            var currentOptions = optionsMonitor.CurrentValue;

            // Subscribe to changes
            optionsMonitor.OnChange(updatedOptions =>
            {
                // Update any other logic if needed when options change
                Console.WriteLine($"Options of type {typeof(T).Name} have been updated.");

                // Here you could also call a custom method on the options instance if needed
                // e.g., currentOptions.OnConfigurationChanged(updatedOptions);
            });

            return currentOptions;
        });

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
