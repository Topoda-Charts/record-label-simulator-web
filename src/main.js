import { loadRemoteTuning } from "./config/firebase.js";
import { createBloomvilleScene } from "./scene/bloomvilleScene.js";

const LORE_FALLBACK =
  "Fallback flavor: In Era 2425 GST, Central Bloomville keeps its music capital calm—labels watch the plaza while Sheet Music becomes Demo Recording becomes Master before a Track ever charts.";

const titleEl = document.getElementById("label-title");
const tempoEl = document.getElementById("tempo-value");
const qualityEl = document.getElementById("quality-value");
const loreEl = document.getElementById("lore-line");
const statusEl = document.getElementById("config-status");
const canvas = document.getElementById("city-canvas");

loreEl.textContent = LORE_FALLBACK;

createBloomvilleScene(canvas);

async function applyRemoteTuning() {
  const { values, source } = await loadRemoteTuning();
  titleEl.textContent = values.label_title;
  tempoEl.textContent = values.tempo;
  qualityEl.textContent = values.quality;
  statusEl.textContent = `Remote Config: ${source}. Quality shown here is a tunable display number—not a claim that reviews rewrite Final Quality.`;
}

applyRemoteTuning();
