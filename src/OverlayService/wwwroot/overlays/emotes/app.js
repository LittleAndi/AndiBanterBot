// Emote rain overlay: polls /overlay/emotes/events for recent emote occurrences
// and spawns one animated <img> per occurrence. Dedupes client-side by message id
// + fragment index since the endpoint just replays recent chat/recent each poll
// rather than tracking per-viewer cursors.

import { overlayConfig, pollEvents } from "/overlays/_shared/overlay-client.js";

const config = overlayConfig({
    direction: "down", // down | up | left | right
    speed: "6", // seconds to cross the viewport
    size: "64", // emote size in px
    pollMs: "2500",
});

const DIRECTIONS = ["down", "up", "left", "right"];
const direction = DIRECTIONS.includes(config.direction) ? config.direction : "down";
const speedSeconds = Math.max(1, parseFloat(config.speed) || 6);
const emoteSize = Math.max(16, parseInt(config.size, 10) || 64);
const pollMs = Math.max(500, parseInt(config.pollMs, 10) || 2500);

// Bounds the dedupe set so a long-running browser source doesn't leak memory.
const MAX_SEEN = 500;

const stage = document.getElementById("stage");
const seen = new Set();

function occurrenceKey(occurrence) {
    return `${occurrence.messageId}:${occurrence.fragmentIndex}`;
}

function randomLane() {
    return 5 + Math.random() * 90;
}

function driftJitter() {
    return (Math.random() - 0.5) * 20;
}

function pickPath() {
    const lane = randomLane();
    const drift = `${lane + driftJitter()}vw`;

    switch (direction) {
        case "up":
            return { startX: `${lane}vw`, startY: "110vh", endX: drift, endY: "-10vh" };
        case "left":
            return { startX: "110vw", startY: `${lane}vh`, endX: "-10vw", endY: `${lane + driftJitter()}vh` };
        case "right":
            return { startX: "-10vw", startY: `${lane}vh`, endX: "110vw", endY: `${lane + driftJitter()}vh` };
        case "down":
        default:
            return { startX: `${lane}vw`, startY: "-10vh", endX: drift, endY: "110vh" };
    }
}

function spawnEmote(occurrence) {
    const path = pickPath();
    const duration = speedSeconds + (Math.random() - 0.5) * speedSeconds * 0.4;

    const img = document.createElement("img");
    img.className = "emote";
    img.src = occurrence.url;
    img.alt = "";
    img.width = emoteSize;
    img.height = emoteSize;
    img.style.setProperty("--start-x", path.startX);
    img.style.setProperty("--start-y", path.startY);
    img.style.setProperty("--end-x", path.endX);
    img.style.setProperty("--end-y", path.endY);
    img.style.setProperty("--duration", `${duration}s`);
    img.addEventListener("animationend", () => img.remove());

    stage.appendChild(img);
}

function trimSeen() {
    if (seen.size <= MAX_SEEN) return;
    const excess = seen.size - MAX_SEEN;
    const iterator = seen.values();
    for (let i = 0; i < excess; i++) {
        seen.delete(iterator.next().value);
    }
}

function handleData(occurrences) {
    for (const occurrence of occurrences) {
        const key = occurrenceKey(occurrence);
        if (seen.has(key)) continue;
        seen.add(key);
        spawnEmote(occurrence);
    }
    trimSeen();
}

pollEvents("/overlay/emotes/events", pollMs, handleData, (err) => console.error("emotes overlay poll failed", err));
