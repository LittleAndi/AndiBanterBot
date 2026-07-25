import { overlayConfig, pollEvents } from "/overlays/_shared/overlay-client.js";

// "Stream starting soon" screen (#134, #151). The title still rides the
// browser-source URL's query string, but the countdown target is set from the
// Web control room and persisted server-side (StartingOverlayModule) - so
// this polls /overlay/starting/events instead of reading ?startsAt= once at
// load. That's what lets the streamer change the target every stream without
// ever touching the URL pasted into OBS.
const config = overlayConfig({
    title: "Stream starting soon!",
});

const titleEl = document.getElementById("title");
const countdownEl = document.getElementById("countdown");
const countdownValueEl = document.getElementById("countdown-value");
const fallbackEl = document.getElementById("fallback");

titleEl.textContent = config.title;

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

let startsAt = null;

function render() {
    if (!startsAt) {
        showFallback();
        return;
    }
    const remaining = startsAt.getTime() - Date.now();
    if (remaining <= 0) {
        showFallback();
        return;
    }
    showCountdown();
    countdownValueEl.textContent = formatDuration(remaining);
}

setInterval(render, 1000);

// 20s is cheap over a multi-hour OBS session while still surfacing a freshly
// set target well ahead of the stream actually starting.
pollEvents("/overlay/starting/events", 20000, data => {
    startsAt = data.startsAt ? new Date(data.startsAt) : null;
    render();
});
