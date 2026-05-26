#version 450
// JFA seed: read the R8 occluder mask, write each "filled" texel's own SDF-pixel coordinate (encoded -1..1 SNORM),
// and write (-1, -1) sentinel for "empty" texels (the next steps recognize sentinel as "no seed").
layout(set = 0, binding = 0) uniform sampler2D occluderMask;
layout(push_constant) uniform JfaInitPush {
    vec4 sdfSizePx_invSize; // .xy = SDF pixel size (texels), .zw = 1.0 / size.
} pc;
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec2 outSeed;

void main() {
    float covered = texture(occluderMask, vUv).r;
    if (covered < 0.5) {
        outSeed = vec2(-1.0);
        return;
    }
    // Encode the texel's center (integer + 0.5) so that texel (0,0) maps to a value
    // strictly above the (-1,-1) sentinel, avoiding the corner-seed aliasing bug.
    vec2 sampleSdfPx = floor(vUv * pc.sdfSizePx_invSize.xy);
    vec2 norm = ((sampleSdfPx + 0.5) / max(pc.sdfSizePx_invSize.xy, vec2(1.0))) * 2.0 - 1.0;
    outSeed = clamp(norm, vec2(-1.0), vec2(1.0));
}
