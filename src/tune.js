import { DEFAULTS } from "./config/defaults.js";
import { loadRemoteTuning, tuneApiBaseUrl } from "./config/firebase.js";

const form = document.getElementById("tune-form");
const statusEl = document.getElementById("tune-status");
const authNote = document.getElementById("tune-auth-note");

function setStatus(text, kind) {
  statusEl.textContent = text;
  statusEl.className = "tune-status";
  if (kind === "ok") statusEl.classList.add("tune-status--ok");
  if (kind === "err") statusEl.classList.add("tune-status--err");
}

authNote.textContent =
  "Save publishes Remote Config through a Cloud Function. If the function is not deployed yet, values still load from in-app defaults on the live page.";

async function initTuneForm() {
  const { values } = await loadRemoteTuning();
  form.label_title.value = values.label_title;
  form.tempo.value = values.tempo;
  form.quality.value = values.quality;
}

initTuneForm();

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  const payload = {
    label_title: form.label_title.value.trim(),
    tempo: String(form.tempo.value),
    quality: String(form.quality.value),
  };

  setStatus("Saving…", null);
  form.querySelector("button").disabled = true;

  try {
    const res = await fetch(`${tuneApiBaseUrl()}/publishRemoteConfig`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    const body = await res.json().catch(() => ({}));
    if (!res.ok) {
      throw new Error(body.error || `Save failed (${res.status})`);
    }
    setStatus("Saved. Live page picks up changes within about a minute.", "ok");
  } catch (err) {
    setStatus(
      err.message.includes("Failed to fetch")
        ? "Save could not reach the Cloud Function. Deploy functions or use Firebase auth; the live page still uses defaults."
        : err.message,
      "err",
    );
  } finally {
    form.querySelector("button").disabled = false;
  }
});

if (Object.keys(DEFAULTS).length === 0) {
  setStatus("Defaults missing.", "err");
}
