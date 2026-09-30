# Codex goal — Web on Firebase first deploy

## Goal

Ship a Firebase-hosted Vite static site with a Three.js Central Bloomville slice, Remote Config–driven label title/tempo/quality display, and a `/tune` page that saves via a Cloud Function publishing Remote Config.

## Context

- Parent Linear: TCS-182 (Web on Firebase).
- Firebase project: `record-label-simulator`.
- Nation/UI colors follow ObserverPalette / Gaia observer brief (Drive-aligned).
- Web build complements; does not replace, Windows HDRP.

## Constraints

- No secrets in git; no proprietary asset copies; pin npm versions.
- In-app Remote Config defaults: Record Label Simulator, 120, 60.
- Minimum Remote Config fetch interval ~60s in client.
- Do not claim reviews rewrite Final Quality on the live page.
- Do not edit `C:\dev\record-label-simulator` for this milestone.

## Done when

- GitHub repo `Topoda-Charts/record-label-simulator-web` has committed source.
- Firebase Hosting serves `dist/` at the real project URL.
- Live page shows 3D city slice + Remote Config values (defaults if fetch fails).
- `/tune` saves via `publishRemoteConfig` when functions deploy; otherwise clear auth/deploy message.
- TCS-182 comment lists live URL and `/tune` URL.
