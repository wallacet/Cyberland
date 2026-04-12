# Implementation pass: single-path deferred rendering optimization

**Basis:** prior rendering audit (deferred stack, bloom, per-sprite binds, SSBO clear, `WaitForFences`); this doc is the **execution plan**.

**Non-goals:**

- No **forward**, **simplified**, or **quality-tier** alternate renderer. One pipeline, one mental model.
- No commitment to **backwards compatibility** for `IRenderer`, GPU resource layouts, or mod-facing contracts unless we choose to keep names stable for ergonomics.

**Goals:**

- Keep **one** HDR deferred + post stack; reduce **fixed per-frame cost**, **CPU/GPU command amplification**, and **unnecessary work** so a minimal scene (e.g. Pong) is cheap enough that hitching is not expected on baseline hardware.
- **Breaking changes and architecture changes inside `Cyberland.Engine` are authorized** (host/mods adapt as needed).

---

## 1. Lighting uploads (high ROI, localized)

**Problem:** [`UploadPointLightSsboData`](f:/dev/Cyberland/src/Cyberland.Engine/Rendering/VulkanRenderer.Deferred.Pipelines.Lighting.cs) maps a host-visible SSBO sized for `MaxPointLights` and **`span.Clear()`’s the entire span** every frame before writing `n` lights.

**Work:**

- Replace full-buffer clear with **writing only `n` point lights** and passing **`n` (or effective count)** into the deferred point pass so the shader / draw call only considers valid entries—or zero a **small tail** if the shader must read a dense prefix only.
- Audit [`UpdateLightingFrameData`](f:/dev/Cyberland/src/Cyberland.Engine/Rendering/VulkanRenderer.Deferred.Pipelines.Lighting.cs) for redundant map/unmap; consider **persistent mapped** uniform/SSBO memory for per-frame writes (still single path).

**Validation:** Engine tests remain **100% line coverage** on `Cyberland.Engine`; extend tests for new upload behavior / count plumbing.

---

## 2. Conditional passes within the same graph (not a second renderer)

**Problem:** [`VulkanRenderer.Deferred.Recording.cs`](f:/dev/Cyberland/src/Cyberland.Engine/Rendering/VulkanRenderer.Deferred.Recording.cs) records **WBOIT** + **transparent resolve** even when `transparentSpriteCount == 0`.

**Work:**

- When there are **no transparent sprites** in `FramePlan`, **skip** WBOIT framebuffer passes and **resolve** HDR directly to the next stage (composite input), preserving identical output for opaque-only frames.
- Keep one codepath: **branch inside recording**, not a separate `VulkanRendererLite`.

**Risk:** Edge cases (transparency off mid-frame); verify with visual tests or capture comparison.

---

## 3. Reduce duplicate sprite rasterization (architectural, single path)

**Problem:** Opaque sprites are drawn **twice** per frame: **emissive** pass then **G-buffer** pass (loops over the same sorted list with different pipelines).

**Work (choose one direction, may combine phases):**

- **A — Merge passes:** Single render pass / subpass or one MRT pass that outputs **both** emissive contribution and G-buffer targets needed for deferred lighting, eliminating the second full-screen triangle workload for sprites. Requires **shader + render pass + framebuffer layout** changes; **breaking** for internal GPU contracts only.
- **B — Batch draws:** Keep two passes short-term but reduce **per-sprite** `CmdBindDescriptorSets` / `CmdPushConstants` cost via **instancing**, **multi-draw indirect**, or **bindless** texture access so each pass issues fewer commands.

Prefer **A** if it cuts bandwidth and pass count without hurting quality; use **B** if pass merge is too risky in one iteration.

---

## 4. Sprite bind / descriptor strategy (largest structural win)

**Problem:** [`DrawSprite`](f:/dev/Cyberland/src/Cyberland.Engine/Rendering/VulkanRenderer.Deferred.Recording.cs) binds **two descriptor sets per sprite per pass**; text adds many small quads → many binds.

**Work:**

- Move toward **fewer binds per frame**: e.g. **bindless** (descriptor indexing / `NonUniformEXT` as needed), **texture2DArray** atlases with stable handles, or **instanced** quads with a per-instance **texture index** / UV rect buffer.
- May require **shader changes** across sprite pipelines (emissive, G-buffer, WBOIT) and **descriptor layout** overhaul—explicitly allowed.

---

## 5. Bloom and post (tune fixed topology, not a second path)

**Problem:** [`BloomPipeline`](f:/dev/Cyberland/src/Cyberland.Engine/Rendering/VulkanRenderer.BloomPipeline.cs) and [`DeferredRenderingConstants`](f:/dev/Cyberland/src/Cyberland.Engine/Rendering/DeferredRenderingConstants.cs) encode **fixed** pyramid depth and blur ping-pong count.

**Work:**

- After (1–4), **profile** remaining cost; then reduce **redundant** blur steps or merge passes where quality allows, or skip cheap clears when bloom is logically off (already partially handled when `bloomOn` is false—tighten any redundant full clears).
- Avoid introducing a “low quality bloom”; stay one implementation with **better** internals.

---

## 6. GPU/CPU sync and frame pacing

**Problem:** Traces showed **`Vk.WaitForFences`** as a large exclusive block—often a symptom of **GPU work variance**, not a bug.

**Work:**

- Revisit **`MaxFramesInFlight`** ([`VulkanRenderer.cs`](f:/dev/Cyberland/src/Cyberland.Engine/Rendering/VulkanRenderer.cs)), swapchain image count, and present mode interactions **after** GPU cost drops—optional **triple buffering** if it reduces bubbles without adding excess latency.
- Re-measure with `dotnet-trace` + optional GPU capture.

---

## 7. Engineering constraints (repo rules)

- **`Cyberland.Engine.Tests`:** maintain **100% line coverage** on `Cyberland.Engine` after changes ([`test-cyberland-engine`](f:/dev/Cyberland/.cursor/skills/test-cyberland-engine/SKILL.md)).
- **Mods:** update demos (e.g. Pong) only if public engine APIs or content contracts change; no requirement to preserve old APIs.

---

## Suggested implementation order

1. **§1** lighting uploads + **§2** opaque-only WBOIT skip (fast validation, lower risk).
2. **§3** merge emissive+G-buffer or **§4** bind reduction (bigger diffs; may split across PRs).
3. **§5** bloom/post tightening using profiles.
4. **§6** sync/pacing knobs once the frame is cheaper.

---

## Out of scope for this pass

- A second “simple” or “forward” renderer.
- Preserving deprecated `IRenderer` shapes unless convenient.
