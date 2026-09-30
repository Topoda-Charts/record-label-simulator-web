import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  STAGES,
  DEFAULT_QUALITY,
  DEFAULT_TEMPO,
  advance,
  createState,
  receiptForStage,
} from "./labelLoop.js";

describe("labelLoop", () => {
  it("createState uses canon defaults", () => {
    const state = createState();
    assert.equal(state.stage, "Sheet Music");
    assert.equal(state.releases, 0);
    assert.equal(state.tempo, DEFAULT_TEMPO);
    assert.equal(state.quality, DEFAULT_QUALITY);
  });

  it("advance walks the full lifecycle and counts a release", () => {
    let state = createState({ tempo: 128, quality: 72 });
    const qualities = [state.quality];

    for (let step = 0; step < STAGES.length; step += 1) {
      const result = advance(state);
      assert.ok(result.receipt.length > 0);
      state = result.state;
      qualities.push(state.quality);
    }

    assert.equal(state.stage, "Sheet Music");
    assert.equal(state.releases, 1);
    assert.equal(state.tempo, 128);
    assert.deepEqual(qualities, [72, 72, 72, 72, 72]);
  });

  it("track receipt mentions reviews without changing quality", () => {
    const receipt = receiptForStage("Track", {
      tempo: 120,
      quality: 60,
      releases: 2,
    });
    assert.match(receipt, /Reviews filed/);
    assert.match(receipt, /Final Quality remains 60/);
  });
});
