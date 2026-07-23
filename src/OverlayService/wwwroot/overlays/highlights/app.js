import { pollEvents, overlayConfig } from "/overlays/_shared/overlay-client.js";

const config = overlayConfig({ pollIntervalMs: 7000, toastDurationMs: 6000 });
const pollIntervalMs = Number(config.pollIntervalMs);
const toastDurationMs = Number(config.toastDurationMs);

const container = document.getElementById("toasts");
const seenClipIds = new Set();
let seenInitialized = false;

function showToast(event) {
    const toast = document.createElement("div");
    toast.className = "toast";
    toast.textContent = `🎬 Clipped ${event.highlightDescription}!`;
    container.appendChild(toast);

    requestAnimationFrame(() => toast.classList.add("visible"));

    setTimeout(() => {
        toast.classList.remove("visible");
        toast.addEventListener("transitionend", () => toast.remove(), { once: true });
    }, toastDurationMs);
}

pollEvents("/overlay/highlights/events", pollIntervalMs, (events) => {
    // First poll only seeds seenClipIds - an overlay reload (or a TwitchService
    // restart repopulating the buffer) shouldn't replay old clips as new toasts.
    if (!seenInitialized) {
        for (const event of events) seenClipIds.add(event.clipId);
        seenInitialized = true;
        return;
    }

    for (const event of events) {
        if (seenClipIds.has(event.clipId)) continue;
        seenClipIds.add(event.clipId);
        showToast(event);
    }
});
