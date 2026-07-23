namespace OverlayService.Overlays;

// Catalog of registered overlay modules. Drives the /overlay/{slug} routing and
// backs the picker UI in Web. Deliberately generic (register + list + lookup):
// each real module registers itself from its own future issue, so the registry
// starts empty rather than pre-seeding placeholder entries for planned modules.
public interface IOverlayRegistry
{
    void Register(OverlayModule module);

    IReadOnlyCollection<OverlayModule> List();

    bool TryGet(string slug, out OverlayModule module);
}
