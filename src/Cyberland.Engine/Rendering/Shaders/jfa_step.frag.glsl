#version 450
// JFA jump: for each texel, examine 9 neighbors at offset ±stepSdfPx, keep the closest valid seed.
// Seeds are encoded in SNORM as the source texel's normalized coordinate.
layout(set = 0, binding = 0) uniform sampler2D seedPrev;
layout(push_constant) uniform JfaStepPush {
    vec4 sdfSizePx_stepPx; // .xy = SDF size in SDF pixels. .z = step in SDF pixels.
} pc;
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec2 outSeed;

void main() {
    vec2 sizeSdfPx = max(pc.sdfSizePx_stepPx.xy, vec2(1.0));
    vec2 stepUv = vec2(pc.sdfSizePx_stepPx.z, pc.sdfSizePx_stepPx.z) / sizeSdfPx;
    vec2 fragSdfPx = floor(vUv * sizeSdfPx);
    vec2 best = vec2(-1.0);
    float bestDistSdfPx2 = 1e30;

    for (int oy = -1; oy <= 1; oy++) {
        for (int ox = -1; ox <= 1; ox++) {
            vec2 sampleUv = vUv + vec2(ox, oy) * stepUv;
            if (sampleUv.x < 0.0 || sampleUv.x > 1.0 || sampleUv.y < 0.0 || sampleUv.y > 1.0)
                continue;
            vec2 seedNorm = texture(seedPrev, sampleUv).rg;
            if (seedNorm.x < -0.999 && seedNorm.y < -0.999)
                continue; // sentinel = no seed
            // Decode seed center back to integer SDF texels (inverse of the half-texel encoding).
            vec2 seedSdfPx = (seedNorm * 0.5 + 0.5) * sizeSdfPx - 0.5;
            vec2 toSeedSdfPx = seedSdfPx - fragSdfPx;
            float distSdfPx2 = dot(toSeedSdfPx, toSeedSdfPx);
            if (distSdfPx2 < bestDistSdfPx2) {
                bestDistSdfPx2 = distSdfPx2;
                best = seedNorm;
            }
        }
    }

    outSeed = best;
}
