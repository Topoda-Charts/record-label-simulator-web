# Record Label Simulator — Web on Firebase

Lightweight **Web on Firebase** host: Three.js view of Central Bloomville plus Remote Config tuning. Separate from the Windows HDRP Unity game.

- **Live (after deploy):** https://record-label-simulator.web.app
- **Tune:** https://record-label-simulator.web.app/tune/

See `AGENTS.md` for agent operating notes.

## Verification note (2026-09-30)

- `npm run build` passed; both hosted pages (`/` and `/tune/`) returned HTTP 200.
- The configured `publishRemoteConfig` endpoint returned HTTP 404, so saving from `/tune/` is not verified or live.
- Firebase Functions deployment and the project's Blaze status were not verified: the Firebase CLI was unavailable, and local CLI installation failed with npm `ECOMPROMISED`.
- Code review found that the function currently has no authentication check; restrict publishing before deploying it.

## Art provenance (this milestone)

- **3D geometry:** procedural district modules in Three.js (`src/scene/buildingModule.js`, `src/scene/bloomvilleScene.js`) — no external mesh packs.
- **Colors:** ObserverPalette / Gaia observer brief (Annglora `#CC99FF`, Byteria `#3333FF`, Crownia `#FFD700`, app surfaces `#FAF7F2`) — aligned with Drive canon; not a new canon lock for lighting/fog.
- **Raster textures:** none shipped in v0.1; no Leonardo or generated image batch in this pass.
- **Drive reference (concept, not copied into repo):** [TTH — Bloomville Cover Concept — Visual Identity Brief](https://docs.google.com/document/d/1qTcvwTq_NEn9IkVVePT81Fi082ucDMi2FT3fKVcfOyo/edit) (JL asset).
