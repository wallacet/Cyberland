// Shared shadow-SDF cone-trace helpers. Included into tiled_deferred_lighting.frag.glsl (sole production consumer)
// by the shader baker (and at runtime via EngineShaderSources.LoadFragmentWithShadowInclude).
//
// COORDINATE SPACE CONTRACT (READ FIRST)
// --------------------------------------
// Inputs to sdfSoftShadow() / sdfDirectionalShadow() are in WORLD space (+Y up); the SwapchainFrag variants accept
// fragSwapchainPx directly and only project the light, saving one worldToSwapchainPx per light. Marches happen ENTIRELY
// in SWAPCHAIN px (+Y down). SDF samples are stored in SDF TEXELS and converted to swapchain px via
// (1 / cam.sdfScale). There is exactly one world->swapchain conversion site (`worldToSwapchainPx`) and exactly one
// SDF-texel->swapchain-px conversion site (at the SDF tap). Every named local carries its space suffix; do not
// introduce bare `pos`, `t`, or `dist` locals — review will reject them.
layout(set = 2, binding = 0) uniform sampler2D shadowSdf;
layout(set = 2, binding = 1) uniform ShadowSdfParamsUbo {
    vec4 sdfSizeScale_kSoftDepthBias; // .xy = reserved (unused), .z = sdfScale (texels per swapchain px), .w = kSoft.
    vec4 enabledSamples_dirTraceWorldDist; // .x = enabled (0/1), .y = max cone-trace samples, .z = directional trace dist (world px), .w = depthBias (world px).
} shadowSdfParams;

struct CameraData {
    vec2 cameraPosWorld;
    float cameraRotRad;
    vec2 viewportSizeWorldPx;
    vec2 physicalOffsetSwapchainPx;
    float physicalScale;
    vec2 screenSizeSwapchainPx;
    float shadowEnabled;
};

// World (+Y up, px) -> swapchain (+Y down, px). Single canonical helper; mirror of ShadowSdfCamera.WorldToSwapchainPx.
vec2 worldToSwapchainPx(vec2 pWorld, CameraData cam) {
    vec2 d = pWorld - cam.cameraPosWorld;
    float c = cos(-cam.cameraRotRad);
    float s = sin(-cam.cameraRotRad);
    vec2 r = vec2(d.x * c - d.y * s, d.x * s + d.y * c);
    vec2 vpPx = vec2(r.x + cam.viewportSizeWorldPx.x * 0.5, cam.viewportSizeWorldPx.y * 0.5 - r.y);
    return cam.physicalOffsetSwapchainPx + vpPx * cam.physicalScale;
}

// Inverse of worldToSwapchainPx. Single canonical helper; mirror of ShadowSdfCamera.SwapchainPxToWorld.
vec2 swapchainPixelToWorld(vec2 swapPxTopLeft, CameraData cam) {
    vec2 vpPx = (swapPxTopLeft - cam.physicalOffsetSwapchainPx) / max(cam.physicalScale, 1e-4);
    float rx = vpPx.x - cam.viewportSizeWorldPx.x * 0.5;
    float ry = cam.viewportSizeWorldPx.y * 0.5 - vpPx.y;
    float c = cos(cam.cameraRotRad);
    float s = sin(cam.cameraRotRad);
    vec2 dx = vec2(rx * c - ry * s, rx * s + ry * c);
    return cam.cameraPosWorld + dx;
}

// Inner cone-trace loop operating entirely in swapchain pixels (+Y down). All public shadow
// functions project their inputs into this space and delegate here; this is the single source of
// truth for the Iñigo Quilez soft-shadow march: vis = clamp(min(k * d(t) / t), 0, 1).
// Uniform parameters (kSoft, maxSamples, depthBias) are read from the ShadowSdfParamsUbo.
float _marchConeSdf(vec2 fragSwapchainPx, vec2 lightSwapchainPx, CameraData cam) {
    vec2 toLightSwapchainPx = lightSwapchainPx - fragSwapchainPx;
    float totalSwapchainPx = length(toLightSwapchainPx);
    if (totalSwapchainPx < 1.0)
        return 1.0;
    vec2 marchDirSwapchainPx = toLightSwapchainPx / totalSwapchainPx;

    float sdfScale = max(shadowSdfParams.sdfSizeScale_kSoftDepthBias.z, 1e-4);
    float kSoft = shadowSdfParams.sdfSizeScale_kSoftDepthBias.w;
    int maxSamples = int(shadowSdfParams.enabledSamples_dirTraceWorldDist.y + 0.5);
    if (maxSamples <= 0)
        maxSamples = 32;
    float depthBiasWorld = shadowSdfParams.enabledSamples_dirTraceWorldDist.w;
    float depthBiasSwapchainPx = depthBiasWorld * cam.physicalScale;

    float tSwapchainPx = max(2.0, depthBiasSwapchainPx);
    float vis = 1.0;
    for (int i = 0; i < 64; i++) {
        if (i >= maxSamples || tSwapchainPx >= totalSwapchainPx)
            break;
        vec2 sampleSwapchainPx = fragSwapchainPx + marchDirSwapchainPx * tSwapchainPx;
        // SDF images are sized to SdfSizePx (= screen * sdfScale); UV = swapchainPx / screenSize.
        vec2 sampleUv = sampleSwapchainPx / max(cam.screenSizeSwapchainPx, vec2(1.0));
        float distSdfPx = textureLod(shadowSdf, sampleUv, 0.0).r;
        float distSwapchainPx = distSdfPx / sdfScale;
        if (distSwapchainPx < 0.05)
            return 0.0;
        float ratio = kSoft * distSwapchainPx / tSwapchainPx;
        if (ratio < vis)
            vis = ratio;
        tSwapchainPx += max(distSwapchainPx, 1.0);
    }
    return clamp(vis, 0.0, 1.0);
}

// Iñigo Quilez soft shadow: projects both frag and light from world to swapchain px, then marches.
float sdfSoftShadow(vec2 fragWorld, vec2 lightWorld, CameraData cam) {
    if (cam.shadowEnabled < 0.5 || shadowSdfParams.enabledSamples_dirTraceWorldDist.x < 0.5)
        return 1.0;
    return _marchConeSdf(worldToSwapchainPx(fragWorld, cam), worldToSwapchainPx(lightWorld, cam), cam);
}

// Variant taking fragment position already in swapchain pixels, avoiding redundant projection per-light.
float sdfSoftShadowSwapchainFrag(vec2 fragSwapchainPx, vec2 lightWorld, CameraData cam) {
    if (cam.shadowEnabled < 0.5 || shadowSdfParams.enabledSamples_dirTraceWorldDist.x < 0.5)
        return 1.0;
    return _marchConeSdf(fragSwapchainPx, worldToSwapchainPx(lightWorld, cam), cam);
}

// Variant taking BOTH fragment and light positions already in swapchain pixels (avoids worldToSwapchainPx entirely).
// Use when the light's swapchain-px position is already available in the SSBO (e.g. spot row 0).
float sdfSoftShadowSwapchainFragSwapchainLight(vec2 fragSwapchainPx, vec2 lightSwapchainPx, CameraData cam) {
    if (cam.shadowEnabled < 0.5 || shadowSdfParams.enabledSamples_dirTraceWorldDist.x < 0.5)
        return 1.0;
    return _marchConeSdf(fragSwapchainPx, lightSwapchainPx, cam);
}

// Directional cone-trace with pre-computed fragment swapchain position.
float sdfDirectionalShadowSwapchainFrag(vec2 fragSwapchainPx, vec2 fragWorld, vec2 dirWorld, CameraData cam) {
    if (cam.shadowEnabled < 0.5 || shadowSdfParams.enabledSamples_dirTraceWorldDist.x < 0.5)
        return 1.0;
    float dirLen = length(dirWorld);
    if (dirLen < 1e-6)
        return 1.0;
    vec2 dirNorm = dirWorld / dirLen;
    float traceWorldDist = max(shadowSdfParams.enabledSamples_dirTraceWorldDist.z, 1.0);
    vec2 virtualLightWorld = fragWorld + dirNorm * traceWorldDist;
    return sdfSoftShadowSwapchainFrag(fragSwapchainPx, virtualLightWorld, cam);
}

// Directional cone-trace: march WORLD-direction `dirWorld` from frag for `dirTraceWorldDist` world px.
float sdfDirectionalShadow(vec2 fragWorld, vec2 dirWorld, CameraData cam) {
    if (cam.shadowEnabled < 0.5 || shadowSdfParams.enabledSamples_dirTraceWorldDist.x < 0.5)
        return 1.0;
    float dirLen = length(dirWorld);
    if (dirLen < 1e-6)
        return 1.0;
    vec2 dirNorm = dirWorld / dirLen;
    float traceWorldDist = max(shadowSdfParams.enabledSamples_dirTraceWorldDist.z, 1.0);
    vec2 virtualLightWorld = fragWorld + dirNorm * traceWorldDist;
    return sdfSoftShadow(fragWorld, virtualLightWorld, cam);
}
