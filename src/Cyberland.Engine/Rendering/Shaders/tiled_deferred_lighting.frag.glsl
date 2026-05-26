#version 450
// Tiled deferred lighting: single fullscreen pass performing all deferred lighting (directional, spot, point).
// Reads the G-buffer, iterates directional lights globally, then reads per-tile bins to iterate only the
// spot lights and point lights that overlap the current tile.
//
// LIGHTING MODELS
// - Directional: Lambertian N·L with synthetic Z = DIRECTIONAL_SYNTHETIC_Z (treating the 2D
//   direction as a 3D vector with a fixed vertical component). Only this light type uses G-buffer normals.
// - Spot: radial distance attenuation (world-space) × cone smoothstep (world-space). No N·L;
//   spot intensity is purely distance/cone based.
// - Point: radial falloff pow(1 - d/r, exponent) in world space. No N·L; point intensity is
//   purely distance based.
// The absence of N·L for spot/point is intentional for this 2D engine: normals are tangent-space
// approximations that add visual depth for broad directional lighting but don't benefit localized
// lights where the attenuation function already handles falloff.
//
// COORDINATE SPACE
// ----------------
// gl_FragCoord.xy is SWAPCHAIN px (+Y down). fragWorld is reconstructed once via swapchainPixelToWorld() from the
// shadow_sdf_sampling.glsl include. Shadow visibility uses the SDF cone-trace with WORLD inputs; the trace handles
// the conversion to swapchain px and SDF px internally.
// Spot and point attenuation both operate in WORLD space (zoom-invariant). Spot SSBO row 0 `posSwapchainPx` is used
// by the cone-trace optimization (sdfSoftShadowSwapchainFragSwapchainLight) to avoid redundant world→swapchain
// projection per fragment.

// Fixed vertical component for directional light 3D direction synthesis.
// Directional lights are 2D (XY world direction); this Z lifts them slightly out-of-plane for N·L
// shading so surface normals contribute visible directional contrast.
#define DIRECTIONAL_SYNTHETIC_Z 0.8

// --- set 0: G-buffer samplers ---
layout(set = 0, binding = 0) uniform sampler2D gbufAlbedo;
layout(set = 0, binding = 1) uniform sampler2D gbufNormal;

// --- set 1: all lighting data (UBO + SSBOs + tile bins) ---
layout(set = 1, binding = 0) uniform LightingUbo {
    vec4 ambient;
    vec4 counts; // x = nDir, y = nSpot
} ubo;

layout(std430, set = 1, binding = 1) readonly buffer DirectionalSsbo {
    vec4 d[];
} dirBuf;

layout(std430, set = 1, binding = 2) readonly buffer SpotSsbo {
    vec4 s[];
} spotBuf;

layout(std430, set = 1, binding = 3) readonly buffer PointSsbo {
    vec4 data[];
} points;

// Each tile bin stores (count, indexOffset) as two ints packed into one ivec2.
layout(std430, set = 1, binding = 4) readonly buffer TileBinSsbo {
    ivec2 bins[];
} tileBins;

layout(std430, set = 1, binding = 5) readonly buffer TileIndexSsbo {
    int indices[];
} tileIndices;

layout(std430, set = 1, binding = 6) readonly buffer SpotTileBinSsbo {
    ivec2 bins[];
} spotTileBins;

layout(std430, set = 1, binding = 7) readonly buffer SpotTileIndexSsbo {
    int indices[];
} spotTileIndices;

// --- set 2: shadow SDF (injected by shadow_sdf_sampling.glsl include) ---

layout(location = 0) out vec4 outHdr;

layout(push_constant) uniform Pc {
    vec4 screenSizeSwapchainPx_pad;
    vec4 cameraPosWorld_cameraRotRad;
    vec4 viewportSizeWorld_physicalScale;
    vec4 physicalRectSwapchainPx;
    vec4 shadowSettings;
    vec4 tileSizeAndCounts; // .x = tileSizeSwapchainPx, .y = tilesX, .z = tilesY, .w = maxLightsPerTile
} pc;

void main() {
    vec2 texelUv = (floor(gl_FragCoord.xy) + vec2(0.5)) / pc.screenSizeSwapchainPx_pad.xy;
    vec4 al = texture(gbufAlbedo, texelUv);
    if (al.a < 0.02) {
        outHdr = vec4(0.02, 0.02, 0.06, 1.0);
        return;
    }
    vec3 base = al.rgb;
    vec2 nEnc = texture(gbufNormal, texelUv).xy;
    vec2 nxy = nEnc * 2.0 - 1.0;
    float nz = sqrt(max(1.0 - dot(nxy, nxy), 0.0));
    vec3 N = normalize(vec3(nxy, nz));
    vec3 amb = base * ubo.ambient.xyz;
    vec3 lit = amb;

    CameraData cam;
    cam.cameraPosWorld = pc.cameraPosWorld_cameraRotRad.xy;
    cam.cameraRotRad = pc.cameraPosWorld_cameraRotRad.z;
    cam.viewportSizeWorldPx = pc.viewportSizeWorld_physicalScale.xy;
    cam.physicalOffsetSwapchainPx = pc.physicalRectSwapchainPx.xy;
    cam.physicalScale = pc.viewportSizeWorld_physicalScale.z;
    cam.screenSizeSwapchainPx = pc.screenSizeSwapchainPx_pad.xy;
    cam.shadowEnabled = pc.shadowSettings.x;

    vec2 fragSwapchainPx = gl_FragCoord.xy;
    vec2 fragWorld = swapchainPixelToWorld(fragSwapchainPx, cam);

    // --- Directional lights (global, not tiled) ---
    uint nDir = uint(ubo.counts.x + 0.5);
    for (uint i = 0u; i < nDir; i++) {
        vec4 di = dirBuf.d[i * 3u + 0u]; // camera-rotated dir (.xy) + intensity (.z)
        vec4 ci = dirBuf.d[i * 3u + 1u]; // color (.rgb) + castsShadow (.w)
        vec4 dw = dirBuf.d[i * 3u + 2u]; // world direction (.xy)
        vec3 Ld = normalize(vec3(di.xy, DIRECTIONAL_SYNTHETIC_Z));
        float ndl = max(dot(N, Ld), 0.0);
        if (ndl <= 0.0) continue;
        float vis = (ci.w > 0.5) ? sdfDirectionalShadowSwapchainFrag(fragSwapchainPx, fragWorld, dw.xy, cam) : 1.0;
        lit += base * ci.rgb * (di.z * ndl) * vis;
    }

    // --- Spot lights via tile bins ---
    // Tile grid covers the physical viewport, not the full swapchain.
    // Subtract the viewport origin to get grid-local coordinates.
    float tileSizePx = pc.tileSizeAndCounts.x;
    int tilesX = int(pc.tileSizeAndCounts.y + 0.5);
    int tilesY = int(pc.tileSizeAndCounts.z + 0.5);
    vec2 tileOriginPx = pc.physicalRectSwapchainPx.xy;
    int tileX = int(floor((fragSwapchainPx.x - tileOriginPx.x) / tileSizePx));
    int tileY = int(floor((fragSwapchainPx.y - tileOriginPx.y) / tileSizePx));
    int tileIdx = tileY * tilesX + tileX;
    int maxBins = tilesX * tilesY;

    int spotTileCount = 0;
    int spotTileOffset = 0;
    if (tileIdx >= 0 && tileIdx < maxBins) {
        ivec2 spotBin = spotTileBins.bins[tileIdx];
        spotTileCount = spotBin.x;
        spotTileOffset = spotBin.y;
    }

    for (int j = 0; j < spotTileCount && j < int(pc.tileSizeAndCounts.w); j++) {
        int sid = spotTileIndices.indices[spotTileOffset + j];
        vec4 sprSwapchainPx = spotBuf.s[sid * 4u + 0u];
        vec4 sdc = spotBuf.s[sid * 4u + 1u];
        vec4 sci = spotBuf.s[sid * 4u + 2u];
        vec4 sposWorld = spotBuf.s[sid * 4u + 3u];
        vec2 toSpotWorld = fragWorld - sposWorld.xy;
        float sDistWorld = length(toSpotWorld);
        float rWorld = max(sposWorld.z, 1e-4);
        vec2 sDirWorld = normalize(sdc.xy);
        vec2 toFragDirWorld = sDistWorld > 1e-4 ? normalize(toSpotWorld) : sDirWorld;
        float coneDot = dot(sDirWorld, toFragDirWorld);
        float coneT = smoothstep(sdc.w, sdc.z, coneDot);
        float falloffExp = sposWorld.w;
        float sAtt = pow(max(1.0 - sDistWorld / rWorld, 0.0), falloffExp) * coneT;
        if (sAtt <= 1e-4) continue;
        float castsShadow = sprSwapchainPx.w > 0.5 ? 1.0 : 0.0;
        float vis = castsShadow > 0.5 ? sdfSoftShadowSwapchainFragSwapchainLight(fragSwapchainPx, sprSwapchainPx.xy, cam) : 1.0;
        lit += base * sci.rgb * (sci.w * sAtt) * vis;
    }

    // --- Point lights via tile bins (reusing tile coords computed above) ---
    int tileCount = 0;
    int tileOffset = 0;
    if (tileIdx >= 0 && tileIdx < maxBins) {
        ivec2 bin = tileBins.bins[tileIdx];
        tileCount = bin.x;
        tileOffset = bin.y;
    }

    for (int k = 0; k < tileCount && k < int(pc.tileSizeAndCounts.w); k++) {
        int lid = tileIndices.indices[tileOffset + k];
        vec4 prWorld = points.data[lid * 3u + 0u];
        vec4 pci = points.data[lid * 3u + 1u];
        vec4 sh = points.data[lid * 3u + 2u];
        vec2 toLightWorld = prWorld.xy - fragWorld;
        float pDistWorld = length(toLightWorld);
        float rWorld = max(prWorld.z, 1e-4);
        float falloffNorm = clamp(pDistWorld / rWorld, 0.0, 1.0);
        float expFall = prWorld.w;
        float pAtt = pow(max(1.0 - falloffNorm, 0.0), expFall);
        if (pAtt <= 1e-4) continue;
        float castsShadow = sh.x > 0.5 ? 1.0 : 0.0;
        float vis = castsShadow > 0.5 ? sdfSoftShadowSwapchainFrag(fragSwapchainPx, prWorld.xy, cam) : 1.0;
        lit += base * pci.rgb * (pci.a * pAtt) * vis;
    }

    outHdr = vec4(lit, al.a);
}
