import { pollEvents, overlayConfig } from "/overlays/_shared/overlay-client.js";

// Tunables: ?max=12 (visible messages) and ?fontSize=16 (px), per the config
// model established in overlay-client.js.
const config = overlayConfig({ max: 12, fontSize: 16 });
const maxVisible = Math.max(1, parseInt(config.max, 10) || 12);
document.documentElement.style.setProperty("--chat-font-size", `${parseInt(config.fontSize, 10) || 16}px`);

const DEFAULT_CHATTER_COLOR = "#c9c9c9";
const emoteUrl = (emoteId) => `https://static-cdn.jtvnw.net/emoticons/v2/${emoteId}/default/dark/3.0`;

const feed = document.getElementById("chat-feed");

// messageId -> rendered element, tracks what's currently on screen so re-polls
// only touch what actually changed (new messages, deletions, drops off the window).
const rendered = new Map();

function renderBadge(badge) {
    const el = document.createElement("span");
    el.className = "badge";
    el.textContent = (badge.setId || "?").charAt(0).toUpperCase();
    el.title = badge.setId;
    return el;
}

function renderFragment(fragment) {
    if (fragment.emote) {
        const img = document.createElement("img");
        img.className = "emote";
        img.src = emoteUrl(fragment.emote.id);
        img.alt = fragment.text;
        img.title = fragment.text;
        return img;
    }
    return document.createTextNode(fragment.text);
}

function buildMessage(item) {
    const el = document.createElement("div");
    el.className = "message entering";
    el.dataset.messageId = item.messageId;

    const badges = document.createElement("span");
    badges.className = "badges";
    for (const badge of item.badges ?? []) {
        badges.appendChild(renderBadge(badge));
    }
    el.appendChild(badges);

    const name = document.createElement("span");
    name.className = "chatter";
    name.textContent = item.chatterUserName;
    name.style.color = item.color || DEFAULT_CHATTER_COLOR;
    el.appendChild(name);

    el.appendChild(document.createTextNode(":"));

    const text = document.createElement("span");
    text.className = "text";
    for (const fragment of item.fragments ?? []) {
        text.appendChild(renderFragment(fragment));
    }
    el.appendChild(text);

    applyDeletedState(el, item.isDeleted);

    // Applied on the next frame so the initial "entering" state actually paints
    // before the transition to the resting state kicks in.
    requestAnimationFrame(() => el.classList.remove("entering"));

    return el;
}

function applyDeletedState(el, isDeleted) {
    el.classList.toggle("deleted", Boolean(isDeleted));
}

function removeMessage(el) {
    rendered.delete(el.dataset.messageId);
    el.classList.add("leaving");
    el.addEventListener("transitionend", () => el.remove(), { once: true });
}

function render(items) {
    // chat/recent is newest-first; keep only the visible window and flip it back
    // to oldest-first so appendChild lands new messages at the bottom.
    const latest = items.slice(0, maxVisible).reverse();
    const latestIds = new Set(latest.map((item) => item.messageId));

    for (const [messageId, el] of rendered) {
        if (!latestIds.has(messageId)) {
            removeMessage(el);
        }
    }

    for (const item of latest) {
        const existing = rendered.get(item.messageId);
        if (existing) {
            applyDeletedState(existing, item.isDeleted);
            continue;
        }

        const el = buildMessage(item);
        rendered.set(item.messageId, el);
        feed.appendChild(el);
    }
}

pollEvents("/overlay/chat/events", 2500, render, (err) => console.error("Chat overlay poll failed", err));
