#version 450
// Half-res bright pass: each half-res cell samples full HDR at the center of its 2×2 footprint (see uvFull below).
// composite.frag uses (floor(FragCoord)+0.5)/fullSz for hdr/em/bloom — stay consistent to avoid diagonal ghosting.
// HDR already includes emissive from the lit pass; bloomSourceGain tunes how strongly bright source color feeds bloom
// without changing the main composite tonemapping path.
layout(set = 0, binding = 0) uniform sampler2D hdrTex;
layout(location = 0) out vec4 outC;
layout(push_constant) uniform BloomExtractPc {
    float threshold;
    float knee;
    float bloomSourceGain;
    float pad0;
} pc;

vec3 prefilteredColor(vec3 c, float thresholdLum, float kneeW) {
    float br = max(max(c.r, max(c.g, c.b)), 1e-5);
    float soft = clamp(br - thresholdLum, 0.0, kneeW);
    soft = soft * soft / (4.0 * kneeW + 1e-5);
    float contr = max(br - thresholdLum - soft, 0.0);
    return c * (contr / br);
}

void main() {
    vec2 fullSizeTexels = vec2(textureSize(hdrTex, 0));
    vec2 halfCoordTexels = floor(gl_FragCoord.xy);
    vec2 srcUvFull = (halfCoordTexels * 2.0 + vec2(0.5)) / fullSizeTexels;

    vec3 scene = texture(hdrTex, srcUvFull).rgb;
    vec3 bloom = prefilteredColor(scene, pc.threshold, pc.knee) * pc.bloomSourceGain;
    outC = vec4(bloom, 1.0);
}
