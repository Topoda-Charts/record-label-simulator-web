import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  AREA_OPEN_ORDER,
  STARTING_OPEN_AREA,
  cloudHeldMessage,
  nextToOpen,
  openAreaCountForReleases,
  shouldOpen,
} from "./bloomvilleAreas.js";

describe("bloomville area expansion", () => {
  it("starts with Central Bloomville only at release 0", () => {
    assert.equal(STARTING_OPEN_AREA, "Central Bloomville");
    assert.equal(openAreaCountForReleases(0), 1);
    assert.equal(shouldOpen(0), false);
    assert.equal(nextToOpen(["Central Bloomville"]), "North Bloomville");
  });

  it("opens the next area every two releases", () => {
    assert.equal(shouldOpen(1), false);
    assert.equal(shouldOpen(2), true);
    assert.equal(openAreaCountForReleases(2), 2);
    assert.equal(shouldOpen(3), false);
    assert.equal(shouldOpen(4), true);
    assert.equal(openAreaCountForReleases(4), 3);
  });

  it("walks nextToOpen in canon order", () => {
    const open = [AREA_OPEN_ORDER[0]];
    for (let i = 1; i < AREA_OPEN_ORDER.length; i += 1) {
      assert.equal(nextToOpen(open), AREA_OPEN_ORDER[i]);
      open.push(AREA_OPEN_ORDER[i]);
    }
    assert.equal(nextToOpen(open), null);
  });

  it("stops gating after Southeast", () => {
    assert.equal(AREA_OPEN_ORDER.at(-1), "Southeast Bloomville");
    assert.equal(openAreaCountForReleases(16), 9);
    assert.equal(shouldOpen(16), true);
    assert.equal(openAreaCountForReleases(18), 9);
    assert.equal(shouldOpen(18), false);
  });

  it("describes unbuilt areas as cloud-held", () => {
    const message = cloudHeldMessage("North Bloomville");
    assert.match(message, /held in the cloud until it opens/i);
    assert.doesNotMatch(message, /fog of war/i);
  });
});
