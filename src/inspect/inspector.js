/**
 * Read-only god-view inspector: lot selection and plain-language summaries.
 * Pure JS — no DOM, network, or Three.js.
 */

/** @type {string | null} */
let selectedLotId = null;

const ROOF_HINTS = Object.freeze({
  gable: "Gable roof on this parcel.",
  flat: "Flat roof on this parcel.",
  sawtooth: "Sawtooth roof on this parcel.",
});

/**
 * @returns {string | null}
 */
export function getSelectedLotId() {
  return selectedLotId;
}

/**
 * Remember the lot the player is inspecting.
 *
 * @param {string} lotId
 * @returns {string}
 */
export function select(lotId) {
  if (typeof lotId !== "string" || lotId.length === 0) {
    throw new TypeError("lotId must be a non-empty string.");
  }
  selectedLotId = lotId;
  return selectedLotId;
}

/** Clear the remembered lot selection. */
export function clear() {
  selectedLotId = null;
}

/**
 * Short plain-English inspector card for a city lot (and optional label pipeline).
 *
 * @param {{ id: string, roofKind?: string }} lot
 * @param {{ stage?: string } | null | undefined} [labelState]
 * @returns {string}
 */
export function summary(lot, labelState) {
  if (!lot || typeof lot !== "object") {
    throw new TypeError("lot must be a plain object.");
  }
  if (typeof lot.id !== "string" || lot.id.length === 0) {
    throw new TypeError("lot.id must be a non-empty string.");
  }

  const parts = [`Lot ${lot.id}.`];

  if (lot.roofKind !== undefined && lot.roofKind !== null && lot.roofKind !== "") {
    const hint = ROOF_HINTS[lot.roofKind];
    parts.push(hint ?? `Roof style: ${String(lot.roofKind)}.`);
  }

  if (labelState !== undefined && labelState !== null) {
    const stage = labelState.stage;
    if (typeof stage === "string" && stage.length > 0) {
      parts.push(`Label pipeline is at ${stage}.`);
    }
  }

  return parts.join(" ");
}
