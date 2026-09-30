/**
 * In-app defaults when Remote Config is unavailable (offline-of-Firebase).
 * These are the HUD fallbacks before fetchAndActivate completes or after a failed fetch.
 */
export const DEFAULTS = {
  label_title: "Record Label Simulator",
  tempo: "120",
  quality: "60",
};

export const REMOTE_CONFIG_KEYS = ["label_title", "tempo", "quality"];

/** Input bounds shared by /tune and the publish Cloud Function validator. */
export const TUNING_LIMITS = {
  label_title_max: 120,
  tempo: { min: 40, max: 240 },
  quality: { min: 0, max: 100 },
};
