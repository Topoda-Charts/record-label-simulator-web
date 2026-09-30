import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  DEFAULT_LABEL_STATE,
  STORAGE_KEY,
  load,
  normalizeLabelState,
  save,
} from "./labelState.js";

function memoryStorage() {
  const map = new Map();
  return {
    getItem(key) {
      return map.has(key) ? map.get(key) : null;
    },
    setItem(key, value) {
      map.set(key, String(value));
    },
  };
}

describe("labelState", () => {
  it("uses canon defaults when nothing is stored", () => {
    const storage = memoryStorage();
    assert.deepEqual(load(storage), DEFAULT_LABEL_STATE);
  });

  it("round-trips stage, releases, tempo, and quality", () => {
    const storage = memoryStorage();
    const state = {
      stage: "Master",
      releases: 3,
      tempo: 128,
      quality: 72,
    };
    save(storage, state);
    assert.equal(storage.getItem(STORAGE_KEY).includes('"Master"'), true);
    assert.deepEqual(load(storage), state);
  });

  it("returns defaults for broken JSON", () => {
    const storage = memoryStorage();
    storage.setItem(STORAGE_KEY, "{not json");
    assert.deepEqual(load(storage), DEFAULT_LABEL_STATE);
  });

  it("fills missing or invalid fields with defaults", () => {
    assert.deepEqual(
      normalizeLabelState({ stage: "Nope", releases: -1, tempo: "x" }),
      DEFAULT_LABEL_STATE,
    );
    assert.deepEqual(
      normalizeLabelState({ releases: 2, quality: 55 }),
      { ...DEFAULT_LABEL_STATE, releases: 2, quality: 55 },
    );
  });
});
