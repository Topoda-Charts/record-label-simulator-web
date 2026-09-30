/** @typedef {{ id: string, name: string, open: boolean }} AreaRef */

/**
 * Capital Bloomville nine Areas — only Central is open in the first session.
 * Order: center, N/S/W/E sides, then corners clockwise (NW → NE → SW → SE).
 */
const AREAS = Object.freeze([
  Object.freeze({ id: "central-bloomville", name: "Central Bloomville", open: true }),
  Object.freeze({ id: "north-bloomville", name: "North Bloomville", open: false }),
  Object.freeze({ id: "south-bloomville", name: "South Bloomville", open: false }),
  Object.freeze({ id: "west-bloomville", name: "West Bloomville", open: false }),
  Object.freeze({ id: "east-bloomville", name: "East Bloomville", open: false }),
  Object.freeze({ id: "northwest-bloomville", name: "Northwest Bloomville", open: false }),
  Object.freeze({ id: "northeast-bloomville", name: "Northeast Bloomville", open: false }),
  Object.freeze({ id: "southwest-bloomville", name: "Southwest Bloomville", open: false }),
  Object.freeze({ id: "southeast-bloomville", name: "Southeast Bloomville", open: false }),
]);

export function listAreas() {
  return AREAS.map((area) => ({ ...area }));
}
