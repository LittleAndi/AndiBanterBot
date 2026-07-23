using System.Collections.Concurrent;

namespace OverlayService.Overlays;

public class OverlayRegistry : IOverlayRegistry
{
    private readonly ConcurrentDictionary<string, OverlayModule> modules = new(StringComparer.OrdinalIgnoreCase);

    public void Register(OverlayModule module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module.Slug);
        modules[module.Slug] = module;
    }

    public IReadOnlyCollection<OverlayModule> List() => modules.Values.ToArray();

    public bool TryGet(string slug, out OverlayModule module) => modules.TryGetValue(slug, out module!);
}
