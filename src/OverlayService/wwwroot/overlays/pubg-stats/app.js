import { overlayConfig, pollEvents } from "/overlays/_shared/overlay-client.js";

const config = overlayConfig({ pollIntervalMs: 7000 });

const card = document.getElementById("card");
const mapEl = document.getElementById("map");
const placementEl = document.getElementById("placement");
const killsEl = document.getElementById("kills");
const damageEl = document.getElementById("damage");
const headshotsEl = document.getElementById("headshots");

let lastMatchId = null;
let highlightTimer = null;

function formatDamage(damage) {
    return damage == null ? "–" : Math.round(damage).toString();
}

function formatPlacement(winPlace) {
    return winPlace == null ? "–" : `#${winPlace}`;
}

function render(match) {
    const stats = match?.playerStats ?? null;

    mapEl.textContent = match?.mapName ?? "No matches yet";
    placementEl.textContent = formatPlacement(stats?.winPlace);
    killsEl.textContent = stats?.kills ?? "–";
    damageEl.textContent = formatDamage(stats?.damageDealt);
    headshotsEl.textContent = stats?.headshotKills ?? "–";

    card.classList.toggle("empty", stats === null);

    if (match && match.matchId !== lastMatchId) {
        if (lastMatchId !== null) {
            highlightNewMatch();
        }
        lastMatchId = match.matchId;
    }
}

function highlightNewMatch() {
    card.classList.add("updated");
    clearTimeout(highlightTimer);
    highlightTimer = setTimeout(() => card.classList.remove("updated"), 4000);
}

pollEvents("/overlay/pubg-stats/events", Number(config.pollIntervalMs), render);
