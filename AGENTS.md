# Record Label Simulator — Web on Firebase

**Scope:** `C:\dev\record-label-simulator-web` — browser host for RLSim tuning and a lightweight 3D Central Bloomville slice. This is **not** the Windows HDRP Unity game at `C:\dev\record-label-simulator`.

## Layout

- `index.html` / `src/main.js` — live 3D view (Three.js) + Remote Config read
- `tune/` — `/tune` Remote Config form (one Save button)
- `functions/` — Cloud Function `publishRemoteConfig` (Admin SDK)
- `dist/` — Vite build output (Firebase Hosting `public`)

## Commands

```bash
npm install
npm run dev
npm run build
npm run preview
firebase deploy --only hosting,functions
```

## Rules

- Do **not** modify the Windows HDRP repo unless JL explicitly scopes that work.
- Do **not** commit secrets (service account JSON, `.env` with private keys).
- Firebase web `apiKey` in client config is public by design.
- Canon / who-is-who: Google Drive. How-to / routing: Notion. Execution: Linear.

## Licenses

See `THIRD_PARTY_LICENSES.md` for pinned npm dependencies.
