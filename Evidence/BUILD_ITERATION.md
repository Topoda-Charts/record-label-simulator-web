# WebGL build iteration note

## Observed in CU-04

- The first Unity CLI build hit its 1,800-second timeout (30 minutes). A resumed run reused cached work; elapsed workflow time therefore spans more than one attempt and must not be reported as a single compiler phase. See [initial log](CU-04-build.log) and [resume log](CU-04-build-resume.log).
- Shader processing totaled about 41.14 seconds across 96 passes. That is a small part of the delay, not its explanation.
- At the 15:57 AST log capture, `Link_WebGL_wasm` had advanced past 531 seconds (the same log shows it reach 555 seconds). The work includes `wasm-opt.exe --post-emscripten -O2`; this was still an in-progress build observation, not a final duration or failure.
- The build uses the `modules_wasm23` runtime module. Release WebGL optimization is present; the inspected settings do not establish that LTO is enabled. The explicit fast IL2CPP code-generation option is not configured.
- The sampled host was an i5-9300H, 4 cores/8 threads, 16 GB RAM, with about 2.5 GB free at the sample. This is one snapshot, not a resource profile.

## Smallest useful iteration plan

1. Keep the Unity version pinned at 6000.5.5f1 and retain the existing Library/Bee cache between runs.
2. For early graphics feedback, measure a local Windows Mono CLI preview separately from WebGL. For WebGL iteration, test a build-time-oriented profile without release-only LTO; confirm the available controls in the pinned Unity version. Use browser captures on the actual WebGL build for acceptance.
3. Keep a separate release profile. Unity documents that Web “Optimize for code size and build time” produces less code and faster builds with a runtime-performance tradeoff. Unity 6.0’s Web settings describe “Shorter Build Time” as the fastest option and Runtime Speed with LTO as a longer, shipping-oriented option; verify these controls in 6000.5 before applying them.
4. Record end-to-end wall time and phase times, cache state, build profile, and browser result for each run. No iteration-time improvement has been measured yet.

Community build-time reports vary widely and are anecdotal, not an SLA: [Unity discussion](https://discussions.unity.com/t/unity-6-webgl-building-takes-a-lot-of-time/1543746). Official references: [Web player code-generation optimization](https://docs.unity3d.com/6000.5/Documentation/Manual/web-optimization-player.html) and [Unity 6.0 Web build settings](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/building-distribution/web-build-settings).
