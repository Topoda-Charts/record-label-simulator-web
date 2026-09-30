# CU-07 — Local neighborhood controls and member movement

Implemented in Unity 6000.5.5f1 C# using the built-in render pipeline. This is an observer alpha, not the player-operated Internal MVP.

## Verified locally

- 27/27 EditMode tests passed, including forced pair overlap and bounded recovery search.
- WebGL build: 519.04 seconds, 28,191,601 bytes, C# source commit `24bf6cee7f0afee69ad3017a9c504adef9e9cb88`.
- Actual Chrome player loaded in 2.69 seconds. Controls, snapshots and readback completed in 60.40 seconds on Intel UHD 630. This is not a frame-rate benchmark.
- Nine local Creator Members had live capsule colliders. The sampled minimum spacing after skips was 0.78 m, with zero unresolved overlaps and zero recovery failures. No recovery was needed in this browser run; recovery algorithms were exercised by EditMode tests.
- Day and week skips reached their exact target dates and matched reference digests from ordinary five-minute ticking. Week progress was captured. The day skip completed before the polling capture observed its progress panel.
- Settings opt-in gated Tab diagnostics; N stepped exactly five minutes while paused. Stats and Cash visibility, mouse wheel zoom, HUD scroll isolation, member selection/follow/cancel, production history and snapshot controls worked.
- September 30 produced 293 Track records; the saved digest survived browser reload.
- No browser runtime exceptions. Retained warnings: deprecated manual Unity filesystem synchronization and overlapping `FS.syncfs` calls during rapid saves. Reload passed despite these warnings; save-flush serialization remains a limitation.

Receipts: `CU-07-editmode-verified.xml`, `CU-07-build.json`, `CU-07-browser.json`. Actual player captures are in `Captures/CU-07/`.

## Current presentation and limits

Central Bloomville now presents three recorded Annglora production workplaces and City Hall in a 24 m lot / 12 m street blockout. Record Labels remain organizations in the simulation rather than separate headquarters props. The deterministic fixture retains eight Labels and 24 Members globally; only the nine local Creator Members are presented.

The world is sparse, roofs and people use simple geometry, and the cloud boundary uses oversized primitive shapes. Full housing demand, Area opening/shrink, household records, non-Creator occupations and staggered employment are not implemented. Building reputation no longer changes physical size. Exact growth thresholds, capacity values and shrink disposition remain explicit source questions, not invented canon.

## Guidance and delegation

The integrating agent refreshed TCS-197/TCS-198 and the current neighborhood Drive owner at the checkpoint boundary. The existing Drive owner was updated with JL's collision/recovery, Area–Lot–Structure, growth/shrink, roof, player-stats and developer-overlay requirements. Notion's existing phase workflow now contains a short execution learning receipt.

Luna/max agents handled movement, source guidance and HUD/statistics in separate files. The parent inspected their diffs, corrected the locality and obsolete headquarters test, implemented yielding authoritative skips, and alone ran Unity CLI. All agent assignments and browser QA processes finished. The local preview has a fixed expiry; no indefinite monitor is required.

Sources: [TCS-197](https://linear.app/topoda-charts-studios/issue/TCS-197/neighborhood-world-connective-tissue-growth-direction-members-slots), [TCS-198](https://linear.app/topoda-charts-studios/issue/TCS-198/smooth-unity-time-1x-2x-4x-skip-dayweek-as-simulated-time-no-hard), [neighborhood scope](https://docs.google.com/document/d/1reXTP4bJcwLslcNoEGR5mR-vlEnv3NqistAJ_TO8pNM/edit), [phase workflow](https://app.notion.com/p/3a8caa6d78ed81e99eb3c8e3aa48b8f3).

Unity guidance: [Web technical limitations](https://docs.unity3d.com/6000.5/Documentation/Manual/webgl-technical-overview.html), [Character Controller](https://docs.unity3d.com/6000.5/Documentation/Manual/class-CharacterController.html), [AI Navigation](https://docs.unity3d.com/6000.5/Documentation/Manual/com.unity.ai.navigation.html). This implementation reuses the existing grid movement and capsule checks; no NavMesh package or baked NavMesh was added. Physics does not alter the canonical deterministic digest.
