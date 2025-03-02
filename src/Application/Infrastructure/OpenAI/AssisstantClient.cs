namespace Application.Infrastructure.OpenAI;

#pragma warning disable OPENAI001

public interface IAssistantClient
{
    Task<string> NewThread(ThreadCreationOptions threadCreationOptions);
    Task AddMessage(string threadId, string nick, string role, string message);
    Task<string> RunAndWait(string threadId);
}

public class AssistantClient(IOptionsMonitor<OpenAIClientOptions> optionsMonitor, ILogger<AssistantClient> logger) : IAssistantClient
{
    readonly OpenAIClient openAIClient = new(optionsMonitor.CurrentValue.ApiKey);
    private readonly ILogger<AssistantClient> logger = logger;

    public async Task<string> NewThread(ThreadCreationOptions threadCreationOptions)
    {
        var thread = await openAIClient.GetAssistantClient().CreateThreadAsync(threadCreationOptions);
        return thread.Value.Id;
    }

    public async Task AddMessage(string threadId, string nick, string role, string message)
    {
        var assistantClient = openAIClient.GetAssistantClient();
        var messageCreationOptions = new MessageCreationOptions();
        messageCreationOptions.Metadata.Add("nick", nick);
        messageCreationOptions.Metadata.Add("role", role);

        var chatMessage = $"{message}";

        await assistantClient.CreateMessageAsync(threadId, MessageRole.User, [MessageContent.FromText(chatMessage)], messageCreationOptions);
    }

    public async Task<string> RunAndWait(string threadId)
    {
        var assistantClient = openAIClient.GetAssistantClient();
        var createRunClientResult = await assistantClient.CreateRunAsync(threadId, optionsMonitor.CurrentValue.Assistant);
        var threadRun = createRunClientResult.Value;

        do
        {
            Thread.Sleep(100);
            threadRun = await assistantClient.GetRunAsync(threadRun.ThreadId, threadRun.Id);
        } while (!threadRun.Status.IsTerminal);

        var messages = assistantClient.GetMessages(threadRun.ThreadId, new MessageCollectionOptions() { Order = MessageCollectionOrder.Descending });

        foreach (var message in messages)
        {
            foreach (var contentItem in message.Content)
            {
                logger.LogInformation("{Role}: {Message}", message.Role.ToString().ToUpper(), contentItem.Text);
            }
        }

        return messages.First().Content.First().Text;
    }
}

#pragma warning restore OPENAI001
