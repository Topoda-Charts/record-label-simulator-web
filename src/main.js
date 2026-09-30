import { DEFAULTS } from "./config/defaults.js";
import { loadRemoteTuning } from "./config/firebase.js";
import { createBloomvilleScene } from "./scene/bloomvilleScene.js";

const LORE_LINE = "Central Bloomville, Era 2425.";

const WORK_STAGES = ["Sheet Music", "Demo Recording", "Master", "Track"];

const titleEl = document.getElementById("label-title");
const tempoEl = document.getElementById("tempo-value");
const qualityEl = document.getElementById("quality-value");
const loreEl = document.getElementById("lore-line");
const statusEl = document.getElementById("config-status");
const stageEl = document.getElementById("stage-label");
const advanceBtn = document.getElementById("advance-btn");
const canvas = document.getElementById("city-canvas");

loreEl.textContent = LORE_LINE;
tempoEl.textContent = DEFAULTS.tempo;
qualityEl.textContent = DEFAULTS.quality;

const scene = createBloomvilleScene(canvas);
let stageIndex = 0;

function renderStage() {
  stageEl.textContent = WORK_STAGES[stageIndex];
  scene.setActiveBuilding(stageIndex);
}

renderStage();

advanceBtn.addEventListener("click", () => {
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
