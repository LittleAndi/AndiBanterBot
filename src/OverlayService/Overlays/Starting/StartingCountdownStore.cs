using System.Text.Json;

namespace OverlayService.Overlays.Starting;

// Persists the "starting soon" countdown target server-side instead of on the
// OBS browser-source URL. OBS stores whatever URL you paste into a Browser
// source and won't pick up edits without reopening its properties, so baking
// a fresh timestamp into the URL before every stream meant re-pasting it each
// time. Setting the target here instead means the URL never has to change -
// see StartingOverlayModule's /overlay/starting/events endpoints and
// app.js's poll of that endpoint.
public interface IStartingCountdownStore
{
    DateTimeOffset? Get();
    void Set(DateTimeOffset? startsAt);
}

public class StartingCountdownStore : IStartingCountdownStore
{
    private readonly ILogger<StartingCountdownStore> logger;
    private readonly string storePath;
    private readonly object gate = new();
    private DateTimeOffset? startsAt;

    public StartingCountdownStore(IConfiguration configuration, ILogger<StartingCountdownStore> logger)
    {
        this.logger = logger;

        storePath = configuration["Overlays:StartingCountdownStorePath"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AndiBanterBot", "starting-countdown.json");

        startsAt = Load();
    }

    public DateTimeOffset? Get()
    {
        lock (gate)
        {
            return startsAt;
        }
    }

    public void Set(DateTimeOffset? value)
    {
        lock (gate)
        {
            startsAt = value;
            Save(value);
        }
    }

    private DateTimeOffset? Load()
    {
        try
        {
            if (File.Exists(storePath))
            {
                var stored = JsonSerializer.Deserialize<StoredValue>(File.ReadAllText(storePath));
                return stored?.StartsAt;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load starting countdown store from {StorePath}, starting empty", storePath);
        }

        return null;
    }

    private void Save(DateTimeOffset? value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);
            File.WriteAllText(storePath, JsonSerializer.Serialize(new StoredValue(value)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not persist starting countdown store to {StorePath}", storePath);
        }
    }

    private record StoredValue(DateTimeOffset? StartsAt);
}
