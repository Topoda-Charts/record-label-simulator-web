/** @typedef {{ id: string, name: string, open: boolean }} AreaRef */

/** Capital Bloomville nine Areas — only Central is open in the first session. */
const AREAS = Object.freeze([
  Object.freeze({ id: "central-bloomville", name: "Central Bloomville", open: true }),
  Object.freeze({ id: "north", name: "North", open: false }),
  Object.freeze({ id: "south", name: "South", open: false }),
  Object.freeze({ id: "east", name: "East", open: false }),
  Object.freeze({ id: "west", name: "West", open: false }),
  Object.freeze({ id: "northeast", name: "Northeast", open: false }),
  Object.freeze({ id: "northwest", name: "Northwest", open: false }),
  Object.freeze({ id: "southwest", name: "Southwest", open: false }),
  Object.freeze({ id: "southeast", name: "Southeast", open: false }),
]);

export function listAreas() {
  return AREAS.map((area) => ({ ...area }));
}
