#version 450
layout(set = 0, binding = 0) uniform sampler2D srcTex;
layout(location = 0) out vec4 outC;
layout(push_constant) uniform BloomResamplePc {
    float srcW;
    float srcH;
    float dstW;
    float dstH;
    float fineBlend;
} pc;

void main() {
    vec2 srcSz = vec2(pc.srcW, pc.srcH);
    vec2 dstSz = vec2(pc.dstW, pc.dstH);
    vec2 dstCoord = floor(gl_FragCoord.xy);
    dstCoord = clamp(dstCoord, vec2(0.0), max(dstSz - vec2(1.0), vec2(0.0)));
    vec2 srcPx = ((dstCoord + vec2(0.5)) / dstSz) * srcSz;
    vec2 basePx = floor(srcPx - vec2(0.5));
    vec2 maxPx = srcSz - vec2(0.5);

    vec3 c = vec3(0.0);
    c += texture(srcTex, clamp(basePx + vec2(0.5, 0.5), vec2(0.5), maxPx) / srcSz).rgb;
    c += texture(srcTex, clamp(basePx + vec2(1.5, 0.5), vec2(0.5), maxPx) / srcSz).rgb;
    c += texture(srcTex, clamp(basePx + vec2(0.5, 1.5), vec2(0.5), maxPx) / srcSz).rgb;
    c += texture(srcTex, clamp(basePx + vec2(1.5, 1.5), vec2(0.5), maxPx) / srcSz).rgb;
    outC = vec4(c * 0.25, 1.0);
}
