import { DEFAULTS, TUNING_LIMITS } from "./defaults.js";

/** Plain-language descriptions for the three Remote Config knobs (HUD + /tune). */
export const TUNING_KNOB_HELP = {
  label_title:
    "Title on the live page header. Cosmetic for the web host; not a save-game label name.",
  tempo:
    "Display BPM on the live HUD. Does not drive audio or simulation timing in this build.",
  quality:
    "Display number on the live HUD only. It is not Final Quality from the music lifecycle and is not changed by critic or audience systems.",
};

export function normalizeTuningForm(form) {
  return {
    label_title: String(form.label_title.value).trim(),
    tempo: String(form.tempo.value),
    quality: String(form.quality.value),
  };
}

export function applyTuningToForm(form, values) {
  form.label_title.value = values.label_title ?? DEFAULTS.label_title;
  form.tempo.value = values.tempo ?? DEFAULTS.tempo;
  form.quality.value = values.quality ?? DEFAULTS.quality;
}

export function validateTuningPayload(payload) {
  if (!payload || typeof payload !== "object") {
    return "Missing tuning values.";
  }

  const title = payload.label_title;
  if (!title || typeof title !== "string" || title.length === 0) {
    return "Label title is required.";
  }
  if (title.length > TUNING_LIMITS.label_title_max) {
    return `Label title must be ${TUNING_LIMITS.label_title_max} characters or fewer.`;
  }

  const tempoNum = Number(payload.tempo);
  if (
    !Number.isFinite(tempoNum) ||
    tempoNum < TUNING_LIMITS.tempo.min ||
    tempoNum > TUNING_LIMITS.tempo.max
  ) {
    return `Tempo must be between ${TUNING_LIMITS.tempo.min} and ${TUNING_LIMITS.tempo.max}.`;
  }

  const qualityNum = Number(payload.quality);
  if (
    !Number.isFinite(qualityNum) ||
    qualityNum < TUNING_LIMITS.quality.min ||
    qualityNum > TUNING_LIMITS.quality.max
  ) {
    return `Quality must be between ${TUNING_LIMITS.quality.min} and ${TUNING_LIMITS.quality.max}.`;
  }

  return null;
}

/**
 * User-facing result when POST /publishRemoteConfig fails or is unreachable.
 * Does not imply a live publish succeeded.
 */
export function describePublishFailure(error, httpStatus) {
  if (httpStatus === 404) {
    return "Save could not publish: publishRemoteConfig is not deployed. Edit values here locally; the live page still reads Remote Config or in-app defaults.";
  }
  if (error?.message?.includes("Failed to fetch")) {
    return "Save could not publish: the browser could not reach publishRemoteConfig (function not deployed, emulator offline, or network blocked). Nothing was written to Firebase Remote Config.";
  }
  if (error?.message) {
    return `Save could not publish: ${error.message}`;
  }
  return "Save could not publish. Nothing was written to Firebase Remote Config.";
}
