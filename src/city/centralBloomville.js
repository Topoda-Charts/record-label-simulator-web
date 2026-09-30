/** @typedef {"gable" | "flat" | "sawtooth"} RoofKind */

/** @typedef {{ id: string, x: number, z: number, width: number, depth: number, roofKind: RoofKind }} CityLot */

/** Meters — matches walkable Main Street slice (not a 96 m tile grid). */
export const STREET_WIDTH_M = 12;
export const LOT_SIZE_M = 24;
export const SIDEWALK_WIDTH_M = 2;

/** Along-street extent including curb-to-curb span for four 24 m bays (~108 m). */
export const MAIN_STREET_LENGTH_M = LOT_SIZE_M * 4 + STREET_WIDTH_M;

const NORTH_LOT_CENTER_Z = -(STREET_WIDTH_M / 2 + SIDEWALK_WIDTH_M + LOT_SIZE_M / 2);
const SOUTH_LOT_CENTER_Z = STREET_WIDTH_M / 2 + SIDEWALK_WIDTH_M + LOT_SIZE_M / 2;
const LOT_CENTERS_X = [-36, -12, 12, 36];

/** @param {number} centerX @param {number} centerZ @param {number} width @param {number} depth */
function lotRectFromCenter(centerX, centerZ, width, depth) {
  return {
    x: centerX - width / 2,
    z: centerZ - depth / 2,
    width,
    depth,
  };
}

/** @param {string} id @param {number} centerX @param {number} centerZ @param {number} width @param {number} depth @param {RoofKind} roofKind */
function makeLot(id, centerX, centerZ, width, depth, roofKind) {
  return {
    id,
    roofKind,
    ...lotRectFromCenter(centerX, centerZ, width, depth),
  };
}

/** @type {CityLot[]} */
const LOTS = [
  makeLot("lot-n-west", LOT_CENTERS_X[0], NORTH_LOT_CENTER_Z, LOT_SIZE_M, LOT_SIZE_M, "gable"),
  makeLot("lot-n-west-mid", LOT_CENTERS_X[1], NORTH_LOT_CENTER_Z, LOT_SIZE_M, LOT_SIZE_M, "gable"),
  makeLot("lot-n-east-mid", LOT_CENTERS_X[2], NORTH_LOT_CENTER_Z, LOT_SIZE_M, LOT_SIZE_M, "flat"),
  makeLot("lot-n-east", LOT_CENTERS_X[3], NORTH_LOT_CENTER_Z, LOT_SIZE_M, LOT_SIZE_M, "sawtooth"),
  makeLot("lot-s-west", LOT_CENTERS_X[0], SOUTH_LOT_CENTER_Z, LOT_SIZE_M, LOT_SIZE_M, "gable"),
  makeLot("lot-s-west-mid", LOT_CENTERS_X[1], SOUTH_LOT_CENTER_Z, LOT_SIZE_M, LOT_SIZE_M, "flat"),
  makeLot("lot-s-east-mid", LOT_CENTERS_X[2], SOUTH_LOT_CENTER_Z, LOT_SIZE_M, LOT_SIZE_M, "sawtooth"),
  makeLot("lot-s-east", LOT_CENTERS_X[3], SOUTH_LOT_CENTER_Z, LOT_SIZE_M, LOT_SIZE_M, "gable"),
  makeLot("city-hall-frontage", 0, NORTH_LOT_CENTER_Z - 11, 28, 22, "gable"),
];

/** Playable Central Bloomville data for god-view / scene consumers. Origin: center of Main Street. */
export const CENTRAL_BLOOMVILLE_SLICE = Object.freeze({
  areaId: "central-bloomville",
  name: "Central Bloomville",
  open: true,
  units: "meters",
  origin: "main-street-center",
  mainStreet: Object.freeze({
    axis: "x",
    length: MAIN_STREET_LENGTH_M,
    width: STREET_WIDTH_M,
    /** Street centerline runs through (0, 0). */
    center: Object.freeze({ x: 0, z: 0 }),
  }),
  lotSize: LOT_SIZE_M,
  lots: Object.freeze(LOTS.map((lot) => Object.freeze({ ...lot }))),
});

export function getCentralSlice() {
  return CENTRAL_BLOOMVILLE_SLICE;
}
