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

## Authority and verification

- [Current build execution: TCS-182](https://linear.app/topoda-charts-studios/issue/TCS-182/stand-up-the-second-unity-project-for-the-cloud-universal-player)
- [Shared behavior translation: TCS-195](https://linear.app/topoda-charts-studios/issue/TCS-195/translate-the-label-loop-and-member-rules-into-the-cloud-unity-project)
- [Observer specification](https://docs.google.com/document/d/1G7nIoYHzhVgFwX1vDElF-Si_VPDslg51FwiPxMGiwyg/edit)
- [Current phase workflow](https://app.notion.com/p/3a8caa6d78ed81e99eb3c8e3aa48b8f3?pvs=204)

Each Member is one person. City Hall is a civic landmark. Production remains Sheet Music → Demo Recording → Master → released Track. This observer has no player label-management route. Economy values remain provisional, versioned observer tuning. Firebase does not calculate the simulation or own snapshots.

Implementation and build/runtime verification status are recorded in `Evidence/checkpoints.json`. Source presence alone is not a verified player build or JL acceptance.
