/**
 * Bloomville capital area unlock order and release gates.
 * Pure data + logic — no DOM, network, or Three.js.
 */

/** Display names in unlock order (Central first, Southeast last). */
export const AREA_OPEN_ORDER = Object.freeze([
  "Central Bloomville",
  "North Bloomville",
  "South Bloomville",
  "West Bloomville",
  "East Bloomville",
  "Northwest Bloomville",
  "Northeast Bloomville",
  "Southwest Bloomville",
  "Southeast Bloomville",
]);

export const STARTING_OPEN_AREA = AREA_OPEN_ORDER[0];

const RELEASES_PER_AREA = 2;

/**
 * @param {number} releases
 * @returns {number} How many areas may be open at this release count (1–9).
 */
export function openAreaCountForReleases(releases) {
  const count = Number(releases);
  if (!Number.isInteger(count) || count < 0) {
    throw new RangeError("Releases must be a non-negative integer.");
  }
  const extra = Math.floor(count / RELEASES_PER_AREA);
  return Math.min(AREA_OPEN_ORDER.length, 1 + extra);
}

/**
 * Playable gate: a new area becomes eligible every two catalog releases.
 * Release 0 keeps only Central; never treats nine areas as eligible at once.
 *
 * @param {number} releases
 * @returns {boolean}
 */
export function shouldOpen(releases) {
  const count = Number(releases);
  if (!Number.isInteger(count) || count < 0) {
    throw new RangeError("Releases must be a non-negative integer.");
  }
  if (count === 0 || count % RELEASES_PER_AREA !== 0) {
    return false;
  }
  return openAreaCountForReleases(count) > openAreaCountForReleases(count - RELEASES_PER_AREA);
}

/**
 * Next closed area name in canon order, or null when every area is open.
 *
 * @param {readonly string[] | Set<string>} openNames
 * @returns {string | null}
 */
export function nextToOpen(openNames) {
  const open = normalizeOpenNames(openNames);
  for (const name of AREA_OPEN_ORDER) {
    if (!open.has(name)) {
      return name;
    }
  }
  return null;
}

/**
 * Player-facing copy for areas not yet streamed into the live slice.
 *
 * @param {string} areaName
 * @returns {string}
 */
export function cloudHeldMessage(areaName) {
  const name = String(areaName ?? "").trim();
  if (!name) {
    throw new RangeError("Area name is required.");
  }
  return `${name} stays held in the cloud until it opens.`;
}

/**
 * @param {readonly string[] | Set<string>} openNames
 * @returns {Set<string>}
 */
function normalizeOpenNames(openNames) {
  if (openNames instanceof Set) {
    return new Set(openNames);
  }
  if (Array.isArray(openNames)) {
    return new Set(openNames);
  }
  throw new TypeError("openNames must be an array or Set of area names.");
}
