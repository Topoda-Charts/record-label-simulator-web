/**
 * Pure label production loop: Sheet Music → Demo Recording → Master → Track.
 * No DOM, network, or Three.js — safe to run in Node or the browser.
 */

export const STAGES = Object.freeze([
  "Sheet Music",
  "Demo Recording",
  "Master",
  "Track",
]);

export const DEFAULT_TEMPO = 120;
export const DEFAULT_QUALITY = 60;

const NEXT_STAGE = Object.freeze({
  "Sheet Music": "Demo Recording",
  "Demo Recording": "Master",
  Master: "Track",
  Track: "Sheet Music",
});

/**
 * @param {{ tempo?: number, quality?: number, stage?: string, releases?: number }} [overrides]
 * @returns {{ stage: string, releases: number, tempo: number, quality: number }}
 */
export function createState(overrides = {}) {
  const tempo =
    overrides.tempo !== undefined ? Number(overrides.tempo) : DEFAULT_TEMPO;
  const quality =
    overrides.quality !== undefined
      ? Number(overrides.quality)
      : DEFAULT_QUALITY;

  return normalizeState({
    stage: overrides.stage ?? STAGES[0],
    releases: overrides.releases ?? 0,
    tempo,
    quality,
  });
}

/**
 * @param {{ stage: string, releases: number, tempo: number, quality: number }} state
 * @returns {{ stage: string, releases: number, tempo: number, quality: number }}
 */
export function normalizeState(state) {
  if (!state || typeof state !== "object") {
    throw new TypeError("Label loop state must be a plain object.");
  }

  const stage = state.stage;
  if (!STAGES.includes(stage)) {
    throw new RangeError(`Unknown production stage: ${String(stage)}`);
  }

  const tempo = Number(state.tempo);
  const quality = Number(state.quality);
  const releases = Number(state.releases);

  if (!Number.isFinite(tempo) || !Number.isFinite(quality)) {
    throw new RangeError("Tempo and quality must be finite numbers.");
  }
  if (!Number.isInteger(releases) || releases < 0) {
    throw new RangeError("Releases must be a non-negative integer.");
  }

  return { stage, releases, tempo, quality };
}

/**
 * Receipt for completing the current stage (read before the next stage begins).
 *
 * @param {string} stage
 * @param {{ tempo: number, quality: number, releases: number }} context
 * @returns {string}
 */
export function receiptForStage(stage, context) {
  const { tempo, quality, releases } = context;

  switch (stage) {
    case "Sheet Music":
      return `Songwriter signed off the chart at ${tempo} BPM — sheet music is ready for the booth.`;
    case "Demo Recording":
      return `Recording artist cut the demo at ${tempo} BPM; the take is queued for mastering.`;
    case "Master":
      return `Producer locked the master at Final Quality ${quality}; release prep can begin.`;
    case "Track": {
      const releaseNumber = releases + 1;
      return `Track ${releaseNumber} is out on the catalog. Reviews filed, and Final Quality remains ${quality}.`;
    }
    default:
      throw new RangeError(`Unknown production stage: ${String(stage)}`);
  }
}

/**
 * Advance one production step. Leaving Track counts a release and returns to Sheet Music.
 * Tempo and quality are carried through unchanged; reviews never rewrite Final Quality.
 *
 * @param {{ stage: string, releases: number, tempo: number, quality: number }} state
 * @returns {{ state: { stage: string, releases: number, tempo: number, quality: number }, receipt: string }}
 */
export function advance(state) {
  const current = normalizeState(state);
  const receipt = receiptForStage(current.stage, current);

  if (current.stage === "Track") {
    return {
      state: {
        ...current,
        releases: current.releases + 1,
        stage: "Sheet Music",
      },
      receipt,
    };
  }

  return {
    state: {
      ...current,
      stage: NEXT_STAGE[current.stage],
    },
    receipt,
  };
}
