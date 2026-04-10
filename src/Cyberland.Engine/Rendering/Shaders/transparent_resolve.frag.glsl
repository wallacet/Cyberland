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
    vec2 uv = (floor(gl_FragCoord.xy) + vec2(0.5)) / pc.screenSize;
    vec3 opaqueCol = texture(hdrOpaque, uv).rgb;
    vec4 accum = texture(accumTex, uv);
    float reveal = texture(revealTex, uv).r;
    reveal = clamp(reveal, 0.0, 1.0);
    vec3 transCol = accum.rgb / max(accum.a, 1e-4);
    vec3 c = transCol * (1.0 - reveal) + opaqueCol * reveal;
    outHdr = vec4(c, 1.0);
}
