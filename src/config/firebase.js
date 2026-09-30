import { initializeApp } from "firebase/app";
import { getRemoteConfig, fetchAndActivate, getValue } from "firebase/remote-config";
import { DEFAULTS, REMOTE_CONFIG_KEYS } from "./defaults.js";

/** Firebase web minimum fetch interval for client SDK (seconds-scale HUD refresh). */
export const REMOTE_CONFIG_MIN_FETCH_INTERVAL_MS = 60_000;

/** Firebase web client config (public; not a secret). */
const firebaseConfig = {
  apiKey: "AIzaSyDiMfx3UnRc4lXgsPfTqXwBw-dl0t7jjzc",
  authDomain: "record-label-simulator.firebaseapp.com",
  databaseURL: "https://record-label-simulator-default-rtdb.firebaseio.com",
  projectId: "record-label-simulator",
  storageBucket: "record-label-simulator.firebasestorage.app",
  messagingSenderId: "518302586200",
  appId: "1:518302586200:web:d85e1b50f4926a2ff5412d",
  measurementId: "G-L7BJ6398LF",
};

const app = initializeApp(firebaseConfig);
const remoteConfig = getRemoteConfig(app);
remoteConfig.settings = {
  minimumFetchIntervalMillis: REMOTE_CONFIG_MIN_FETCH_INTERVAL_MS,
  fetchTimeoutMillis: 10_000,
};
remoteConfig.defaultConfig = { ...DEFAULTS };

/**
 * Loads tuning via fetchAndActivate + getValue per key, falling back to DEFAULTS.
 * @returns {{ values: typeof DEFAULTS, source: string }}
 */
export async function loadRemoteTuning() {
  const values = { ...DEFAULTS };
  let source = "in-app defaults";

  try {
    await fetchAndActivate(remoteConfig);
    source = "Firebase Remote Config";
    for (const key of REMOTE_CONFIG_KEYS) {
      const v = getValue(remoteConfig, key);
      if (v && String(v.asString()).length > 0) {
        values[key] = String(v.asString());
      }
    }
  } catch {
    source = "in-app defaults (Remote Config fetch failed)";
  }

  return { values, source };
}

export function tuneApiBaseUrl() {
  const host = window.location.hostname;
  if (host === "localhost" || host === "127.0.0.1") {
    return "http://127.0.0.1:5001/record-label-simulator/us-central1";
  }
  return "https://us-central1-record-label-simulator.cloudfunctions.net";
}
