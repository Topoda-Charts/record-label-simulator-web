/**
 * Local label save slot — browser storage adapter only (no window, no network).
 * Authority is on-device; integrators pass a getItem/setItem storage object.
 */

export const STORAGE_KEY = "rlsim-label-state";

export const DEFAULT_LABEL_STATE = Object.freeze({
  stage: "Sheet Music",
  releases: 0,
  tempo: 120,
  quality: 60,
});

const VALID_STAGES = new Set([
  "Sheet Music",
  "Demo Recording",
  "Master",
  "Track",
]);

/**
 * @param {unknown} storage
 */
function assertStorage(storage) {
  if (
    !storage ||
    typeof storage.getItem !== "function" ||
    typeof storage.setItem !== "function"
  ) {
    throw new TypeError("storage must provide getItem and setItem");
  }
}

/**
 * @param {unknown} value
 * @returns {{ stage: string, releases: number, tempo: number, quality: number }}
 */
export function normalizeLabelState(value) {
  const base = { ...DEFAULT_LABEL_STATE };

  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return base;
  }

  const record = value;

  const stage = record.stage;
  if (typeof stage === "string" && VALID_STAGES.has(stage)) {
    base.stage = stage;
  }

  const releases = Number(record.releases);
  if (Number.isInteger(releases) && releases >= 0) {
    base.releases = releases;
  }

  const tempo = Number(record.tempo);
  if (Number.isFinite(tempo)) {
    base.tempo = tempo;
  }

  const quality = Number(record.quality);
  if (Number.isFinite(quality)) {
    base.quality = quality;
  }

  return base;
}

/**
 * @param {{ getItem: (key: string) => string | null, setItem: (key: string, value: string) => void }} storage
 * @param {{ stage?: string, releases?: number, tempo?: number, quality?: number }} state
 */
export function save(storage, state) {
  assertStorage(storage);
  const payload = normalizeLabelState(state);
  storage.setItem(STORAGE_KEY, JSON.stringify(payload));
}

/**
 * @param {{ getItem: (key: string) => string | null, setItem: (key: string, value: string) => void }} storage
 * @returns {{ stage: string, releases: number, tempo: number, quality: number }}
 */
export function load(storage) {
  assertStorage(storage);
  const raw = storage.getItem(STORAGE_KEY);
  if (raw === null || raw === "") {
    return { ...DEFAULT_LABEL_STATE };
  }

  try {
    return normalizeLabelState(JSON.parse(raw));
  } catch {
    return { ...DEFAULT_LABEL_STATE };
  }
}
