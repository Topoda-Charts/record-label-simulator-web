# Record Label Simulator — Unity cloud observer

This is Unity build 2: a browser-capable C# observer, compiled by Unity CLI and delivered by Firebase Hosting. The separate Windows HDRP project remains Local HD.

The first port reuses the existing deterministic simulation, eight-label observer fixture, Bloomville geometry, visible Member movement, camera, UI Toolkit inspector, event history and snapshots. It uses Unity's WebGL-compatible built-in renderer. The existing August 31 handoff remains available and observation can continue through September with the same simulation rules.

## Run locally

Project: `UnityCloud`, Unity `6000.5.5f1`. Unity CLI is installed at `C:\Users\jlram\AppData\Local\Unity\bin\unity.exe`.

```powershell
unity run ./UnityCloud -- -executeMethod Topoda.RLS.Editor.CloudObserverBuild.GenerateScene
unity test ./UnityCloud --mode EditMode --output ./Evidence/CU-03-editmode.xml -- -rlsSnapshotRoot C:/dev/record-label-simulator-web/UnityCloud/TestResults/ObserverSnapshots
unity build ./UnityCloud --target WebGL --execute-method Topoda.RLS.Editor.CloudObserverBuild.BuildWebGL --output-path ./UnityCloud/Builds/WebGL
node ./Evidence/serve-unity.cjs
```

The bounded preview server serves the Unity player at `http://localhost:8080` when built and build status at `/status`. It expires after 60 minutes. POST `/__stop` closes it early.

Use `CloudObserverBuild.BuildWebGLIteration` during HUD iteration to select Debug native compilation and OptimizeSize IL2CPP generation. Retain `Library/Bee` between runs. CU-07 is built into `UnityCloud/Builds/CU-07/WebGL` and inspected at `/candidate/` while the last verified build remains at `/`.

Wheel input uses Input System 1.19's uniform units, smoothly zooms toward the cursor, and is ignored over HUD panels. Right/middle drag pans; Alt + left drag orbits; Home restores the overview. Click a member to inspect and follow it. Manual camera movement cancels following. The Production control opens a tray of recorded output links; its actions inspect the simulation without directing a label.

CU-07 presents the three recorded Annglora workplaces and their nine Creator Members in the Central slice. All eight organizations and 24 Members remain in the deterministic world; an organization is not a headquarters building. Local Members have capsule bounds, wall checks, separation and bounded movement recovery. Exact collision and recovery values are implementation tuning.

Player controls are Pause and 1x/2x/4x. Skip day/week cooperatively runs every authoritative five-minute tick with progress feedback and saves before/after. Stats and individual metrics can be hidden. Developer diagnostics default off; opt in through Settings, then use Tab. These changes do not implement housing demand, Area expansion/shrink, non-Creator occupations, or the full employment schedule. Current source and actual runtime evidence must be checked separately.

## Authority and verification

- [Current build execution: TCS-182](https://linear.app/topoda-charts-studios/issue/TCS-182/stand-up-the-second-unity-project-for-the-cloud-universal-player)
- [Shared behavior translation: TCS-195](https://linear.app/topoda-charts-studios/issue/TCS-195/translate-the-label-loop-and-member-rules-into-the-cloud-unity-project)
- [Neighborhood connective tissue: TCS-197](https://linear.app/topoda-charts-studios/issue/TCS-197/neighborhood-world-connective-tissue-growth-direction-members-slots)
- [Simulated time controls: TCS-198](https://linear.app/topoda-charts-studios/issue/TCS-198/smooth-unity-time-1x-2x-4x-skip-dayweek-as-simulated-time-no-hard)
- [Neighborhood scope](https://docs.google.com/document/d/1reXTP4bJcwLslcNoEGR5mR-vlEnv3NqistAJ_TO8pNM/edit)
- [Observer specification](https://docs.google.com/document/d/1G7nIoYHzhVgFwX1vDElF-Si_VPDslg51FwiPxMGiwyg/edit)
- [Current phase workflow](https://app.notion.com/p/3a8caa6d78ed81e99eb3c8e3aa48b8f3?pvs=204)

Each Member is one person. City Hall is a civic landmark. Production remains Sheet Music → Demo Recording → Master → released Track. This observer has no player label-management route. Economy values remain provisional, versioned observer tuning. Firebase does not calculate the simulation or own snapshots.

Implementation and build/runtime verification status are recorded in `Evidence/checkpoints.json`. Source presence alone is not a verified player build or JL acceptance.
