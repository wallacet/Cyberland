#version 450
// Tent upsample of coarse mip to half-res. fineTex binding kept for descriptor layout compatibility (not sampled).
layout(set = 0, binding = 0) uniform sampler2D coarseTex;
layout(set = 0, binding = 1) uniform sampler2D fineTex;
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
    vec2 srcCenterTexels = ((dstCoordTexels + vec2(0.5)) / max(dstSizeTexels, vec2(1.0))) * srcSizeTexels;
    vec2 srcUv = srcCenterTexels / max(srcSizeTexels, vec2(1.0));
    vec2 texelStep = 1.0 / max(srcSizeTexels, vec2(1.0));
    vec2 minSrcUv = vec2(0.5) / max(srcSizeTexels, vec2(1.0));
    vec2 maxSrcUv = (srcSizeTexels - vec2(0.5)) / max(srcSizeTexels, vec2(1.0));

    vec3 tent = vec3(0.0);
    tent += texture(coarseTex, clamp(srcUv + texelStep * vec2(-1.0, -1.0), minSrcUv, maxSrcUv)).rgb * 1.0;
    tent += texture(coarseTex, clamp(srcUv + texelStep * vec2( 0.0, -1.0), minSrcUv, maxSrcUv)).rgb * 2.0;
    tent += texture(coarseTex, clamp(srcUv + texelStep * vec2( 1.0, -1.0), minSrcUv, maxSrcUv)).rgb * 1.0;
    tent += texture(coarseTex, clamp(srcUv + texelStep * vec2(-1.0,  0.0), minSrcUv, maxSrcUv)).rgb * 2.0;
    tent += texture(coarseTex, clamp(srcUv + texelStep * vec2( 0.0,  0.0), minSrcUv, maxSrcUv)).rgb * 4.0;
    tent += texture(coarseTex, clamp(srcUv + texelStep * vec2( 1.0,  0.0), minSrcUv, maxSrcUv)).rgb * 2.0;
    tent += texture(coarseTex, clamp(srcUv + texelStep * vec2(-1.0,  1.0), minSrcUv, maxSrcUv)).rgb * 1.0;
    tent += texture(coarseTex, clamp(srcUv + texelStep * vec2( 0.0,  1.0), minSrcUv, maxSrcUv)).rgb * 2.0;
    tent += texture(coarseTex, clamp(srcUv + texelStep * vec2( 1.0,  1.0), minSrcUv, maxSrcUv)).rgb * 1.0;
    tent *= (1.0 / 16.0);

    outC = vec4(tent, 1.0);
}
