// Shared client-side helper for AndiBanterBot OBS browser-source overlays.
// Overlays are plain HTML/CSS/JS (not Blazor) so animation stays client-driven
// and smooth inside OBS. Modules start on polling rather than SSE/SignalR, per
// the milestone's internal-comms decision. No module uses this yet — it's the
// platform primitive every future overlay page builds on.

// Reads per-instance config from the browser-source URL's query string
// (e.g. ?theme=dark&position=br), layered over the supplied defaults. This is
// the whole config model — nothing is persisted server-side.
export function overlayConfig(defaults = {}) {
    const params = new URLSearchParams(window.location.search);
    const config = { ...defaults };
    for (const [key, value] of params) {
        config[key] = value;
    }
    return config;
}

// Polls `url` every `intervalMs`, invoking `onData` with the parsed JSON on each
// successful response. The next tick is scheduled only after the previous one
// settles, so a slow or unreachable endpoint can't stack overlapping requests.
// Returns a stop() function.
export function pollEvents(url, intervalMs, onData, onError) {
    let stopped = false;
    let timer = null;

    async function tick() {
        if (stopped) return;
        try {
            const response = await fetch(url, { cache: "no-store" });
            if (response.ok) {
                onData(await response.json());
            } else if (onError) {
                onError(new Error(`Overlay events request failed: ${response.status}`));
            }
        } catch (err) {
            if (onError) onError(err);
        } finally {
            if (!stopped) timer = setTimeout(tick, intervalMs);
        }
    }

    tick();

    return function stop() {
        stopped = true;
        if (timer) clearTimeout(timer);
    };
}
