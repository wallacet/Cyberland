#version 450
// Shadow occluder rasterizer. Vertex layout matches sprite_gbuffer/G-buffer instances; we project sprite-space corners
// through the active camera into NDC, but the target framebuffer is the R8 occluder mask scaled by SdfScale.
//
// COORDINATE SPACE
// ----------------
// Input `inCenterHalfPx.xy` is WORLD (+Y up). We rotate by the sprite's world rotation, translate, then project to
// swapchain px (via push constants encoding viewport offsets) and finally NDC. The mask framebuffer covers the
// swapchain rect at SDF scale; no Y flip happens inside the shader past the world-up to viewport-down step.
//
// The world→swapchain projection below (lines 32-39) mirrors ShadowSdfCamera.WorldToSwapchainPx — keep in sync.
// Covered by ShadowSpaceConventionsTests.OccluderVertPush_agrees_with_ShadowSdfCamera.
layout(push_constant) uniform OccluderPush {
    vec4 viewportSize_cameraRotCos_cameraRotSin; // .xy viewport size (world px). .z cos(-camRot). .w sin(-camRot).
    vec4 cameraPos_physicalOffset;               // .xy camera pos world. .zw physical offset (swapchain px).
    vec4 physicalScale_sdfScale_screen;          // .x physical scale. .y sdf scale. .zw screen (sdf px = swapchain*sdfScale).
} pc;
layout(location = 0) in vec2 inPos;                 // [-1,1] unit-square corner.
layout(location = 1) in vec4 inCenterHalfWorld;     // .xy center world. .zw half-extents world.
layout(location = 2) in vec4 inUvRect;              // ignored for mask path (kept to share buffer with G-buffer).
layout(location = 3) in vec4 inColorAlpha;          // .w = alpha multiplier; passed through for cutoff.
layout(location = 4) in vec4 inEmissive;            // ignored for mask path (kept to share buffer with G-buffer).
layout(location = 5) in vec4 inRotAndFlags;         // .z = world rotation, .w = useEmissiveMap flag.
layout(location = 0) out vec2 vUv;
layout(location = 1) out float vAlpha;

void main() {
    float rot = inRotAndFlags.z;
    float cs = cos(rot);
    float ss = sin(rot);
    vec2 lh = inPos * inCenterHalfWorld.zw;
    vec2 rotLh = mat2(cs, -ss, ss, cs) * lh;
    vec2 cornerWorld = inCenterHalfWorld.xy + rotLh;

    vec2 d = cornerWorld - pc.cameraPos_physicalOffset.xy;
    float cc = pc.viewportSize_cameraRotCos_cameraRotSin.z;
    float ssn = pc.viewportSize_cameraRotCos_cameraRotSin.w;
    vec2 r = vec2(d.x * cc - d.y * ssn, d.x * ssn + d.y * cc);
    vec2 vpPx = vec2(
        r.x + pc.viewportSize_cameraRotCos_cameraRotSin.x * 0.5,
        pc.viewportSize_cameraRotCos_cameraRotSin.y * 0.5 - r.y);
    vec2 swapchainPx = pc.cameraPos_physicalOffset.zw + vpPx * pc.physicalScale_sdfScale_screen.x;
    vec2 sdfPx = swapchainPx * pc.physicalScale_sdfScale_screen.y;
    vec2 screen = max(pc.physicalScale_sdfScale_screen.zw, vec2(1.0));
    vec2 ndc = (sdfPx / screen) * 2.0 - 1.0;
    gl_Position = vec4(ndc, 0.0, 1.0);

    vUv = inPos * 0.5 + 0.5;
    vAlpha = inColorAlpha.w;
}
