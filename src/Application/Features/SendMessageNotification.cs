namespace Application.Features;

public record SendMessageNotification(string Channel, string Message) : INotification;

public class SaveMessageNotificationHandler(IChatService chatService) : INotificationHandler<SendMessageNotification>
{
    private readonly IChatService chatService = chatService;

    public async Task Handle(SendMessageNotification notification, CancellationToken cancellationToken)
    {
        await chatService.SendMessage(notification.Channel, notification.Message, cancellationToken);
    }
}