#version 450
// Half-res bright pass: each half-res cell samples full HDR at the center of its 2×2 footprint (see uvFull below).
// composite.frag uses (floor(FragCoord)+0.5)/fullSz for hdr/em/bloom — stay consistent to avoid diagonal ghosting.
// HDR already includes emissive from the lit pass; bloom prefilter uses HDR only (avoids double-count / ghost bloom).
layout(set = 0, binding = 0) uniform sampler2D hdrTex;
layout(location = 0) out vec4 outC;
layout(push_constant) uniform BloomExtractPc {
    float threshold;
    float knee;
    float emissiveBloomGain;
    float pad0;
} pc;

vec3 prefilteredColor(vec3 c, float t, float kneeW) {
    float br = max(max(c.r, max(c.g, c.b)), 1e-5);
    float soft = clamp(br - t, 0.0, kneeW);
    soft = soft * soft / (4.0 * kneeW + 1e-5);
    float contr = max(br - t - soft, 0.0);
    return c * (contr / br);
}

void main() {
    vec2 fullSz = vec2(textureSize(hdrTex, 0));
    vec2 halfCoord = floor(gl_FragCoord.xy);
    vec2 uvFull = (halfCoord * 2.0 + vec2(0.5)) / fullSz;

    vec3 scene = texture(hdrTex, uvFull).rgb;
    vec3 bloom = prefilteredColor(scene, pc.threshold, pc.knee);
    outC = vec4(bloom, 1.0);
}
