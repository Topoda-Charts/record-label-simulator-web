# RLS Unity cloud universal build

JL owns scope and acceptance. This is Unity build 2, the browser-capable observer hosted on Firebase. Windows HDRP work at `C:/dev/record-label-simulator` remains separate.

- Use Unity 6000.5.5f1 and the installed Unity CLI. Gameplay remains C#.
- Reuse the deterministic observer kernel, existing geometry, camera, movement and inspection.
- First port uses Unity's built-in render pipeline, a WebGL-compatible non-HDRP pipeline.
- Work in inspectable phases with CU-NN checkpoints. Only the integrating agent runs Unity. No competing editor processes.
- Agents own non-overlapping files. The integrating agent reviews and verifies diffs.
- Local browser snapshots remain authoritative. Firebase is hosting.
- Test, build the actual Unity player, inspect runtime and retain evidence before claiming verification.
- Sources: Linear TCS-182 and TCS-195; Drive observer specification `1G7nIoYHzhVgFwX1vDElF-Si_VPDslg51FwiPxMGiwyg`; Notion phase workflow `3a8caa6d78ed81e99eb3c8e3aa48b8f3`.
