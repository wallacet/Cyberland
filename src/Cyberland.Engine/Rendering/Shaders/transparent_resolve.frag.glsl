#version 450
// Resolve WBOIT + opaque HDR (linear) into final HDR before bloom.
layout(set = 0, binding = 0) uniform sampler2D hdrOpaque;
layout(set = 0, binding = 1) uniform sampler2D accumTex;
layout(set = 0, binding = 2) uniform sampler2D revealTex;
layout(location = 0) out vec4 outHdr;
layout(push_constant) uniform Pc {
    vec2 screenSize;
    vec2 padPc;
} pc;
void main() {
    vec2 screenUv = (floor(gl_FragCoord.xy) + vec2(0.5)) / pc.screenSize;
    vec3 opaqueCol = texture(hdrOpaque, screenUv).rgb;
    vec4 accum = texture(accumTex, screenUv);
    // Reveal stores multiplicative transmittance product (clear=1, blend dst*=1-alpha per fragment).
    float reveal = texture(revealTex, screenUv).r;
    reveal = clamp(reveal, 0.0, 1.0);
    // Avoid dividing tiny/noisy accum.a (could inflate transCol and leave smears next to clears edges).
    vec3 transCol = accum.a > 1e-4 ? (accum.rgb / accum.a) : vec3(0.0);
    // Weighted OIT must keep reveal (background transmittance) consistent with accumulation: if no weighted
    // transparent color survived into this pixel, transmittance must stay at clear (=1). FP16 blend drift or
    // driver edge cases can otherwise pair ~0 accum.a with reveal<<1 and crush opaque HDR — reads as missing
    // sprites (e.g. opaque gameplay quads under semi-transparent zones when any transparent draws exist).
    if (accum.a <= 1e-4)
        reveal = 1.0;
    vec3 c = transCol * (1.0 - reveal) + opaqueCol * reveal;
    outHdr = vec4(c, 1.0);
}
