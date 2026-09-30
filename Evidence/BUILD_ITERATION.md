# WebGL build iteration note

## Observed in CU-04

- The first Unity CLI build hit its 1,800-second timeout (30 minutes). A resumed run reused cached work; elapsed workflow time therefore spans more than one attempt and must not be reported as a single compiler phase. See [initial log](CU-04-build.log) and [resume log](CU-04-build-resume.log).
- Shader processing totaled about 41.14 seconds across 96 passes. That is a small part of the delay, not its explanation.
- The completed resumed build spent about 555 seconds in `Link_WebGL_wasm`, including `wasm-opt.exe --post-emscripten -O2`. The first attempt plus resume took longer than that phase alone.
- The build uses the `modules_wasm23` runtime module. The inspected settings do not establish that LTO is enabled.
- The sampled host was an i5-9300H, 4 cores/8 threads, 16 GB RAM, with about 2.5 GB free at the sample. This is one snapshot, not a resource profile.

## Smallest useful iteration plan

1. Keep the Unity version pinned at 6000.5.5f1 and retain the existing Library/Bee cache between runs.
2. For earlier graphics feedback, a local Windows Mono CLI preview remains an available path. Browser acceptance still uses the actual WebGL player. The new WebGL Iteration entry point selects Debug native compilation and OptimizeSize IL2CPP generation.
3. Keep a separate Release entry point using Release native compilation and OptimizeSpeed. Unity 6.5 uses the generic IL2CPP settings; the older `EditorUserBuildSettings.webGLCodeOptimization` API is absent. Do not copy an older-version optimization snippet without checking the installed API.
4. Record end-to-end wall time and phase times, cache state, build profile, and browser result for each run. CU-05 succeeded with 1,435.20 build seconds (23m55s) and 28,083,182 bytes. Native work took about 20m27s, including a 554-second link. The changed native profile required new cache entries, so this is not a fully warm incremental timing. CU-06 then took 587.85 seconds (9m48s); its corrected iteration took 534.23 seconds (8m54s). The corrected actual browser player loaded in 3.64 seconds and completed the 95-second controls/snapshot verification with zero browser errors. These are measured builds on this host, not an estimate or frame-rate benchmark.

Community build-time reports vary widely and are anecdotal, not an SLA: [Unity discussion](https://discussions.unity.com/t/unity-6-webgl-building-takes-a-lot-of-time/1543746). Official references: [Web player code-generation optimization](https://docs.unity3d.com/6000.5/Documentation/Manual/web-optimization-player.html) and [Unity 6.0 Web build settings](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/building-distribution/web-build-settings).
