# Product

## Register

product

## Platform

web

## Users

LittleAndi, the streamer and bot owner, is the sole user — this is a single-operator control room, not a multi-tenant product. It's used live, mid-stream: fast glances between moments of actually playing or talking to chat, often under time pressure and split attention, so clarity has to hold up even when read quickly and imperfectly.

## Product Purpose

This is the console for AndiBanterBot's entire stack: Twitch auth and connection health, chat (reading and sending), bot controls (rewards, clips, whatever the Twitch API and the bot support), stream/game analytics (starting with PUBG), and eventually AI behavior and browser-source overlays for the stream itself. Success looks like the streamer trusting this one tool over piecing together Twitch's own dashboard, Streamlabs/OBS widgets, and raw logs — a single place that shows the bot is alive and lets them act on it.

## Positioning

Everything here is wired directly into the bot's own stack and AI logic — no third-party dashboard or generic config panel sitting in between. Because the whole system (Twitch integration, chat, AI, analytics) is owned end to end, this tool can show and do things a generic streaming dashboard never could.

## Brand Personality

Playful and personality-driven — it should feel like it belongs to the same bot that banters in chat, not a sterile admin console. **Fun over corporate**, characterful over neutral, but never at the cost of being readable in a hurry.

## Anti-references

Not a generic SaaS admin template: no cards-for-everything, no gradient text, no default-Bootstrap-scaffold look — which is exactly the starting point today, and the thing to move furthest away from.

## Design Principles

1. **Direct integration, not abstraction** — every screen reflects real state from the bot/Twitch/AI stack, not a generic wrapper over someone else's service.
2. **Built by the streamer, for the streamer** — single-operator assumptions everywhere; no roles/permissions/multi-tenant scaffolding until an actual second user shows up.
3. **Glanceable under pressure** — status has to read correctly at a fast, imperfect glance mid-stream; never gate meaning on color alone.
4. **Personality over sterility** — matches the bot's own playful voice; avoid corporate blandness.
5. **Calm signal, not noise** — motion communicates a state change, it never performs for attention; reduced motion is a first-class mode, not an afterthought.

## Accessibility & Inclusion

WCAG AA baseline. Status indicators (EventSub connection, token health, chat/bot activity) must never rely on color alone — pair with icon, shape, or text. Respect reduced-motion preferences; live-status updates should stay calm rather than draw the eye away from the actual stream.
