import { overlayConfig } from "/overlays/_shared/overlay-client.js";

// Static/client-side only (#134) — no server-side data endpoint. startsAt
// rides on the browser-source URL as an ISO-8601 timestamp, e.g.
// ?startsAt=2026-08-01T18:00:00Z&title=Back+for+more+PUBG.
const config = overlayConfig({
    title: "Stream starting soon!",
    startsAt: "",
});

const titleEl = document.getElementById("title");
const countdownEl = document.getElementById("countdown");
const countdownValueEl = document.getElementById("countdown-value");
const fallbackEl = document.getElementById("fallback");

titleEl.textContent = config.title;

const startsAt = new Date(config.startsAt);
const hasValidTarget = !Number.isNaN(startsAt.getTime());

function formatDuration(ms) {
    const totalSeconds = Math.floor(ms / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    return [hours, minutes, seconds].map(n => String(n).padStart(2, "0")).join(":");
}

function showFallback() {
    countdownEl.hidden = true;
    fallbackEl.hidden = false;
}

function showCountdown() {
    countdownEl.hidden = false;
    fallbackEl.hidden = true;
}

if (!hasValidTarget) {
    showFallback();
} else {
    let timer;

    function tick() {
        const remaining = startsAt.getTime() - Date.now();
        if (remaining <= 0) {
            showFallback();
            clearInterval(timer);
            return;
        }
        showCountdown();
        countdownValueEl.textContent = formatDuration(remaining);
    }

    tick();
    timer = setInterval(tick, 1000);
}
