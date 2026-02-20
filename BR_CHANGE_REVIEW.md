# BR Branch (`2ff5001`) vs Latest Main (`c70b56f`) Review

This review compares the BR branch commit `2ff5001` against the latest main commit you specified, `c70b56f` (not BR’s parent).

## Commit topology (what each side contains)

- Common ancestor: `8608627`.
- BR-only: `2ff5001`.
- Main-only after divergence:
  - `bd68df6` (`Pattern deduplication, font cache, reload improvements`)
  - `d42e539` (`GameObject.Find caching & monitor polling optimized`)
  - `f5d092c` (AssemblyInfo)
  - `c70b56f` (translation line updates)

This means BR should be evaluated against an already-optimized mainline, not the old baseline.

## What main (`c70`) already improved (and why it matters)

### 1) Hot-path lookup and cache improvements
- `MLCUtils.FindGameObjectCached()` introduced and consumed in runtime handlers.
- `ClearCaches()` added and called on scene/reload boundaries.
- `GetGameObjectPath` cache size tuned for larger scenes.

**Impact:** This reduces repeated `GameObject.Find(path)` cost and avoids stale cache growth.

### 2) Monitor and polling model refinements
- Mainline monitor structure was refactored with better path tracking and removal buffers.
- Visibility polling interval support was added (`VISIBILITY_POLLING_INTERVAL`).
- LateUpdate/ArrayList/Teletext paths were updated to use cached find helpers.

**Impact:** Lower per-frame overhead and fewer expensive tree scans.

### 3) Pattern + translator lifecycle robustness
- Pattern registry reset/dedup flow added (idempotent reload behavior).
- Font application path made safer (`sharedMaterial` handling instead of mutating shared textures badly).
- Runtime caches cleared on scene/reload in core lifecycle.

**Impact:** More stable long sessions, fewer duplicate patterns, cleaner reload semantics.

## What BR adds on top of that

### A) Incremental scene translation coroutine
- BR adds batched full-scene translation (`TRANSLATION_BATCH_SIZE`) driven by `LateUpdateHandler` coroutine.
- Main translation entry can delegate scene-wide scan to this incremental path.

**Value:** Good frame-time smoothing for heavy initial scans.

### B) Per-frame translation budget in monitor
- BR caps work each update (`TRANSLATIONS_PER_CYCLE = 5`).

**Value:** Predictable frame budget under churn.

### C) Main-menu radio/CD FSM patching
- New `UpdateMainMenu` waits for `Radio/Folk` + `Radio/CD` FSM readiness and patches specific labels.

**Value:** Solves strings that normal TextMesh flow may miss.

### D) Parsing/normalization behavior changes
- BR preserves leading spaces in value parsing more carefully.
- BR also changes key normalization to alphanumeric-only canonicalization.

**Value + Risk:** spacing handling is useful; key canonicalization may cause collisions.

## Head-to-head: main (`c70`) vs BR (`2ff`) for performance direction

## ✅ Keep from main as the default baseline
1. `FindGameObjectCached` + central cache clearing lifecycle.
2. Pattern reset/dedup + runtime cache reset hooks.
3. Safer font material handling and reload stability.
4. Existing polling improvements already landed in `d42e539`.

Given your note, this aligns with “main is already better than BR” for many performance primitives.

## ✅ Good BR ideas to adopt onto current main
1. **Incremental scene translation** (batched coroutine), but integrate with current main cache lifecycle.
2. **Translation budget per cycle**, but make it configurable (constant/config) and tune with your new monitor cadence.
3. **Main-menu FSM targeted patch** for Radio/CD, preferably guarded behind a feature flag.
4. **Leading-space-preserving value parsing** in translation/pattern loaders.

## ⚠️ BR ideas to avoid or adjust heavily
1. **Replacing key normalization with alnum-only globally** (`FormatUpperKey`) can merge distinct keys accidentally.
2. **Reverting/removing main’s find-cache improvements** should not happen.
3. **BR project identity/build-path edits** (`_BR` ID, machine-specific csproj paths) are branch-local and should stay out.
4. **Batch yield condition in BR coroutine** should be checked (avoid yielding immediately at `i == 0`).

## Proposed adoption plan (practical)

1. Start from `c70` behavior as baseline (do not regress main’s perf architecture).
2. Port BR incremental translation as an additive feature:
   - stop/restart coroutine on scene transitions,
   - use existing cache-clear lifecycle,
   - expose batch size via constants/config.
3. Port BR translation budget concept:
   - default conservative value,
   - optional debug metrics to tune throughput.
4. Port FSM hook only for known problematic labels (Radio/CD), feature-flagged.
5. Port spacing-preserving parsing, but keep current key normalization unless collision tests justify change.

## Discussion points for next step

If you want, next pass I can produce a concrete **“adopt list” patch plan** with file-by-file edits (no BR identity/build changes), for example:
- `LateUpdateHandler.cs`: incremental translation with corrected batching.
- `MWC_Localization_Core.cs`: lifecycle hooks for starting/stopping incremental pass.
- `UnifiedTextMeshMonitor.cs`: budget setting integrated with your main polling strategy.
- `PatternMatcher.cs` / translation loader: leading-space-preserving parse rules only.
- optional `UpdateMainMenu.cs`: behind config flag.
