import { overlayConfig } from "/overlays/_shared/overlay-client.js";

// Static outro screen: no polling, no countdown — just a sign-off message the
// streamer can tailor per stream via query-string config (?title=, ?message=,
// ?nextStream=) without editing the overlay itself.
const config = overlayConfig({
    title: "That's a Wrap!",
    message: "Thanks for hanging out — chat was a vibe today.",
    nextStream: "",
});

document.getElementById("title").textContent = config.title;
document.getElementById("message").textContent = config.message;

if (config.nextStream) {
    const nextStreamEl = document.getElementById("next-stream");
    nextStreamEl.textContent = `Next stream: ${config.nextStream}`;
    nextStreamEl.hidden = false;
}
