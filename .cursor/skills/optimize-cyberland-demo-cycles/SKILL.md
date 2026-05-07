---
name: optimize-cyberland-demo-cycles
description: Runs iterative performance optimization cycles for a selected Cyberland demo/mod using profiler output, code changes, and verification against a target FPS and max cycles. Use when the user asks to optimize a demo, hit a specific FPS target, run repeated profiling/refactor loops, or stop when GPU-bound.
disable-model-invocation: true
---

# Optimize Cyberland Demo Cycles

Use this skill to run structured optimization loops for a chosen Cyberland demo/mod.

## Required inputs

- `demo`: one of `hdr`, `snake`, `pong`, `brick`, `mousechase`, `idlegold`
- `target_fps`: numeric FPS goal (for example `2200`)
- `max_cycles`: positive integer loop cap

Optional knobs:

- `profile_seconds` (default `10`)
- `run_mode` for primary profiling (`Debug-Instrumented` by default for CPU bottleneck visibility)

## Core loop contract

Repeat cycles until one stop condition is met:

1. **Run selected demo with profiling enabled**
2. **Read generated profiler report**
3. **Create a plan for CPU bottlenecks**
4. **Execute the plan**
5. **Optionally add new profiling points**
6. **Re-test and continue/stop**

## Commands

Run from repo root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Run-CyberlandDemo-Test.ps1 -Demo <demo> -RunMode Debug-Instrumented -ProfileSeconds <seconds> -SkipClearArtifacts
```

This writes:

- frame profiler dump: `artifacts/profiles/<demo>-Debug-Instrumented-<timestamp>.txt`
- perf summary: `artifacts/profiles/<demo>-Debug-Instrumented-<timestamp>.perf.txt`

Use `Release-Perf` runs as secondary validation when needed.

## Cycle workflow

Copy this checklist and update it per cycle:

```text
Optimization cycle N / <max_cycles>
- [ ] Run profile
- [ ] Parse FPS + top CPU scopes
- [ ] Draft bottleneck plan
- [ ] Implement highest-impact fixes
- [ ] Add/update tests if behavior changed
- [ ] Re-run tests and coverage gate
- [ ] Re-profile and compare against baseline
- [ ] Decide stop/continue
```

### 1) Run profile

- Execute the profiling command for the selected demo.
- Capture:
  - measured FPS from `Perf summary | ... fps=...` (or `.perf.txt`)
  - startup cost and first-present timing (for context)
  - top frame scopes from profiler dump

### 2) Parse report

Focus on CPU-heavy scopes first:

- scheduler/system scopes (`Scheduler.RunFrame`, ECS systems)
- UI/text/layout scopes
- rendering submission/build scopes on CPU side
- avoid GPU-driver wait noise unless diagnosing GPU bound

### 3) Plan bottleneck fixes

Create a short, ordered plan:

1. highest total frame-time scope
2. highest call-count * per-call cost scope
3. avoid broad refactors that are not in profiler evidence

Each planned fix should include:

- why this scope is a bottleneck
- exact file/symbol changes
- expected impact
- risk level (low/medium/high)

### 4) Execute

- Implement fixes in dependency order.
- Keep edits tightly scoped to profiler-backed bottlenecks.
- If behavior changes, update tests in `tests/Cyberland.Engine.Tests`.
- Run engine gate:

```powershell
dotnet test tests/Cyberland.Engine.Tests/Cyberland.Engine.Tests.csproj -c Debug /p:CollectCoverage=true
```

### 5) Optional: add profiling points

Add instrumentation when existing scopes are too coarse to pick a clear next action:

- add focused `FrameProfilerScope` markers around suspected hot blocks
- keep names stable and high-signal
- remove noisy/temporary probes once no longer needed

### 6) Re-profile and decide

After each implementation cycle:

- re-run profiling
- compare FPS and top scopes vs prior cycle
- log delta and determine whether to continue

## Stop conditions

Stop when the first condition is met:

1. **Target hit:** measured FPS `>= target_fps`
2. **Max cycles reached:** cycle count equals `max_cycles`
3. **GPU bound:** CPU-side work is no longer dominant, and further CPU changes are unlikely to increase FPS materially

Use this GPU-bound heuristic (all should hold for confidence):

- profiler indicates render/present/wait path dominates frame time
- gameplay/ECS/UI CPU scopes are already relatively small
- one full cycle of CPU-focused changes yields little/no FPS gain

## Final output format

At the end, report:

```markdown
## Optimization Result
- Demo: <demo>
- Target FPS: <target_fps>
- Final FPS: <final_fps>
- Cycles run: <n>/<max_cycles>
- End reason: <hit target fps | max cycles reached | gpu bound>

## Cycle Summary
- Cycle 1: baseline fps, top bottlenecks, key changes, resulting fps
- Cycle 2: ...

## Net Improvements
- Highest-impact fixes:
  - <file/symbol>: <change> -> <observed impact>

## Remaining Bottlenecks
- <ordered list of remaining dominant scopes>

## Why Execution Ended
- <clear explanation tied to stop condition and profiler evidence>
```

## Guardrails

- Do not change coverage thresholds.
- Keep optimization claims tied to measured profiler/perf output.
- Prefer small, measurable iterations over speculative rewrites.
- If no meaningful bottleneck signal is available, add profiling points before large refactors.
