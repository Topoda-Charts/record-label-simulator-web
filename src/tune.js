import { DEFAULTS } from "./config/defaults.js";
import { REMOTE_CONFIG_MIN_FETCH_INTERVAL_MS, loadRemoteTuning, tuneApiBaseUrl } from "./config/firebase.js";
import {
  TUNING_KNOB_HELP,
  applyTuningToForm,
  describePublishFailure,
  normalizeTuningForm,
  validateTuningPayload,
} from "./config/tuning.js";

const form = document.getElementById("tune-form");
const loadStatusEl = document.getElementById("tune-load-status");
const statusEl = document.getElementById("tune-status");
const authNote = document.getElementById("tune-auth-note");
const saveButton = form.querySelector("button[type=submit]");

function setStatus(el, text, kind) {
  el.textContent = text;
  el.className = "tune-status";
  if (kind === "ok") el.classList.add("tune-status--ok");
  if (kind === "err") el.classList.add("tune-status--err");
}

for (const el of document.querySelectorAll("[data-hint]")) {
  const key = el.getAttribute("data-hint");
  if (TUNING_KNOB_HELP[key]) {
    el.textContent = TUNING_KNOB_HELP[key];
  }
}

const fetchSeconds = Math.round(REMOTE_CONFIG_MIN_FETCH_INTERVAL_MS / 1000);
authNote.textContent =
  `Save tries to POST to publishRemoteConfig (no auth on that endpoint). This department does not deploy Cloud Functions. If the function is missing or the project is not on Blaze, Save will fail plainly and only this form changes — not live Remote Config. Live clients pick up published values on the next fetch (about ${fetchSeconds}s).`;

async function initTuneForm() {
  setStatus(loadStatusEl, "Loading current values…", null);
  const { values, source } = await loadRemoteTuning();
  applyTuningToForm(form, values);
  setStatus(loadStatusEl, `Loaded from ${source}.`, "ok");
}

initTuneForm();

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  const payload = normalizeTuningForm(form);
  const validationError = validateTuningPayload(payload);
  if (validationError) {
    setStatus(statusEl, validationError, "err");
    return;
  }

  setStatus(statusEl, "Saving…", null);
  saveButton.disabled = true;

  let httpStatus;
  try {
    const res = await fetch(`${tuneApiBaseUrl()}/publishRemoteConfig`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    httpStatus = res.status;
    const body = await res.json().catch(() => ({}));
    if (!res.ok) {
      throw new Error(body.error || `Save failed (${res.status})`);
    }
    setStatus(
      statusEl,
      `Published to Remote Config. Live HUD refetches within about ${fetchSeconds} seconds.`,
      "ok",
    );
  } catch (err) {
    setStatus(statusEl, describePublishFailure(err, httpStatus), "err");
  } finally {
    saveButton.disabled = false;
  }
});

if (Object.keys(DEFAULTS).length === 0) {
  setStatus(statusEl, "In-app defaults missing.", "err");
}
