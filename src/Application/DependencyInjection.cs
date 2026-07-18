namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IMessageProcessor, MessageProcessor>();
        services.AddSingleton<IInstructionService, InstructionService>();
        services.AddSingleton<IRewardService, RewardService>();
        services.AddHostedService<ChatEventWiring>();
        // services.AddHostedService<PubgBackgroundService>();
        return services;
    }
}
