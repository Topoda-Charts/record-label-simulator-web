import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  clear,
  getSelectedLotId,
  select,
  summary,
} from "./inspector.js";

describe("inspector", () => {
  it("select remembers a lot and clear drops it", () => {
    clear();
    assert.equal(getSelectedLotId(), null);
    assert.equal(select("lot-n-west"), "lot-n-west");
    assert.equal(getSelectedLotId(), "lot-n-west");
    clear();
    assert.equal(getSelectedLotId(), null);
  });

  it("summary includes lot id and roof hint", () => {
    const text = summary({ id: "lot-n-east-mid", roofKind: "flat" });
    assert.match(text, /Lot lot-n-east-mid/);
    assert.match(text, /Flat roof/);
  });

  it("summary adds label stage when label state is passed", () => {
    const text = summary(
      { id: "lot-s-west", roofKind: "gable" },
      { stage: "Demo Recording" },
    );
    assert.match(text, /Label pipeline is at Demo Recording/);
    assert.doesNotMatch(text, /Final Quality/i);
    assert.doesNotMatch(text, /review/i);
  });

  it("summary omits label line when label state is omitted", () => {
    const text = summary({ id: "city-hall-frontage" });
    assert.doesNotMatch(text, /Label pipeline/);
  });
});
