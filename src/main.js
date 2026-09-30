import { DEFAULTS } from "./config/defaults.js";
import { loadRemoteTuning } from "./config/firebase.js";
import { createBloomvilleScene } from "./scene/bloomvilleScene.js";

const LORE_LINE =
  "Central Bloomville, Era 2425. Beyond this street, cloud and soft light hold what Bloomville has not opened yet.";

const WORK_STAGES = ["Sheet Music", "Demo Recording", "Master", "Track"];

const titleEl = document.getElementById("label-title");
const tempoEl = document.getElementById("tempo-value");
const qualityEl = document.getElementById("quality-value");
const loreEl = document.getElementById("lore-line");
const statusEl = document.getElementById("config-status");
const stageEl = document.getElementById("stage-label");
const receiptEl = document.getElementById("receipt-line");
const releaseEl = document.getElementById("release-count");
const advanceBtn = document.getElementById("advance-btn");
const canvas = document.getElementById("city-canvas");

const STAGE_COPY = {
  "Sheet Music": "Pages are on the desk. The songwriter is not done.",
  "Demo Recording": "The artist is in the room. The demo is taking shape.",
  Master: "The producer is closing the master. Watch the quality number.",
  Track: "The track is out. It can chart. Advance starts the next pages.",
};

loreEl.textContent = LORE_LINE;
tempoEl.textContent = DEFAULTS.tempo;
qualityEl.textContent = DEFAULTS.quality;

const scene = createBloomvilleScene(canvas);
let stageIndex = 0;
let releases = 0;

function renderStage() {
  const stage = WORK_STAGES[stageIndex];
  stageEl.textContent = stage;
  receiptEl.textContent = STAGE_COPY[stage];
  releaseEl.textContent = String(releases);
  scene.setActiveBuilding(stageIndex);
}

renderStage();

advanceBtn.addEventListener("click", () => {
  const leaving = WORK_STAGES[stageIndex];
  if (leaving === "Track") {
    releases += 1;
  }
  stageIndex = (stageIndex + 1) % WORK_STAGES.length;
  renderStage();
});

async function applyRemoteTuning() {
  const { values, source } = await loadRemoteTuning();
  titleEl.textContent = values.label_title;
  tempoEl.textContent = values.tempo;
  qualityEl.textContent = values.quality;
  statusEl.textContent = `Remote Config: ${source}.`;
}

applyRemoteTuning();
