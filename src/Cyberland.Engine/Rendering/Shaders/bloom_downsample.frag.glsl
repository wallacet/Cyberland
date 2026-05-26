#version 450
layout(set = 0, binding = 0) uniform sampler2D srcTex;
layout(location = 0) out vec4 outC;
layout(push_constant) uniform BloomResamplePc {
    float srcW;
    float srcH;
    float dstW;
    float dstH;
} pc;

void main() {
    vec2 srcSizeTexels = vec2(pc.srcW, pc.srcH);
    vec2 dstSizeTexels = vec2(pc.dstW, pc.dstH);
    vec2 dstCoordTexels = floor(gl_FragCoord.xy);
    dstCoordTexels = clamp(dstCoordTexels, vec2(0.0), max(dstSizeTexels - vec2(1.0), vec2(0.0)));
    vec2 srcCenterTexels = ((dstCoordTexels + vec2(0.5)) / dstSizeTexels) * srcSizeTexels;
    vec2 baseSrcTexels = floor(srcCenterTexels - vec2(0.5));
    vec2 maxSrcTexels = srcSizeTexels - vec2(0.5);

    vec3 c = vec3(0.0);
    c += texture(srcTex, clamp(baseSrcTexels + vec2(0.5, 0.5), vec2(0.5), maxSrcTexels) / srcSizeTexels).rgb;
    c += texture(srcTex, clamp(baseSrcTexels + vec2(1.5, 0.5), vec2(0.5), maxSrcTexels) / srcSizeTexels).rgb;
    c += texture(srcTex, clamp(baseSrcTexels + vec2(0.5, 1.5), vec2(0.5), maxSrcTexels) / srcSizeTexels).rgb;
    c += texture(srcTex, clamp(baseSrcTexels + vec2(1.5, 1.5), vec2(0.5), maxSrcTexels) / srcSizeTexels).rgb;
    outC = vec4(c * 0.25, 1.0);
}
