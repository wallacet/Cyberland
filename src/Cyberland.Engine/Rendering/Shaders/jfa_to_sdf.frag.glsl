#version 450
// JFA finalize: read the converged "nearest seed" map and the occluder mask; emit signed distance in SDF pixels.
// Negative inside occluders, positive outside; sign is taken from the mask sample (1 = inside, 0 = outside).
layout(set = 0, binding = 0) uniform sampler2D seedFinal;
layout(set = 0, binding = 1) uniform sampler2D occluderMask;
layout(push_constant) uniform JfaToSdfPush {
    vec4 sdfSizePx_unused; // .xy = SDF pixel size.
} pc;
layout(location = 0) in vec2 vUv;
layout(location = 0) out float outSdfPx;

void main() {
    vec2 sizeSdfPx = max(pc.sdfSizePx_unused.xy, vec2(1.0));
    vec2 fragSdfPx = floor(vUv * sizeSdfPx);
    vec2 seedNorm = texture(seedFinal, vUv).rg;
    float distSdfPx;
    if (seedNorm.x < -0.999 && seedNorm.y < -0.999) {
        distSdfPx = max(sizeSdfPx.x, sizeSdfPx.y);
    } else {
        vec2 seedSdfPx = (seedNorm * 0.5 + 0.5) * sizeSdfPx - 0.5;
        distSdfPx = length(seedSdfPx - fragSdfPx);
    }
    float inside = texture(occluderMask, vUv).r;
    outSdfPx = inside > 0.5 ? -distSdfPx : distSdfPx;
}
