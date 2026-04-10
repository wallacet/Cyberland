#version 450
// Tent upsample of coarse mip; optional fine mip add (fineBlend). Internal pyramid passes use fineBlend=0 to avoid double lobes.
layout(set = 0, binding = 0) uniform sampler2D coarseTex;
layout(set = 0, binding = 1) uniform sampler2D fineTex;
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
    vec2 dstPx = floor(gl_FragCoord.xy);
    dstPx = clamp(dstPx, vec2(0.0), max(dstSz - vec2(1.0), vec2(0.0)));
    vec2 srcPxCenter = ((dstPx + vec2(0.5)) / max(dstSz, vec2(1.0))) * srcSz;
    vec2 uv = srcPxCenter / max(srcSz, vec2(1.0));
    vec2 texel = 1.0 / max(srcSz, vec2(1.0));
    vec2 minUv = vec2(0.5) / max(srcSz, vec2(1.0));
    vec2 maxUv = (srcSz - vec2(0.5)) / max(srcSz, vec2(1.0));

    vec3 tent = vec3(0.0);
    tent += texture(coarseTex, clamp(uv + texel * vec2(-1.0, -1.0), minUv, maxUv)).rgb * 1.0;
    tent += texture(coarseTex, clamp(uv + texel * vec2( 0.0, -1.0), minUv, maxUv)).rgb * 2.0;
    tent += texture(coarseTex, clamp(uv + texel * vec2( 1.0, -1.0), minUv, maxUv)).rgb * 1.0;
    tent += texture(coarseTex, clamp(uv + texel * vec2(-1.0,  0.0), minUv, maxUv)).rgb * 2.0;
    tent += texture(coarseTex, clamp(uv + texel * vec2( 0.0,  0.0), minUv, maxUv)).rgb * 4.0;
    tent += texture(coarseTex, clamp(uv + texel * vec2( 1.0,  0.0), minUv, maxUv)).rgb * 2.0;
    tent += texture(coarseTex, clamp(uv + texel * vec2(-1.0,  1.0), minUv, maxUv)).rgb * 1.0;
    tent += texture(coarseTex, clamp(uv + texel * vec2( 0.0,  1.0), minUv, maxUv)).rgb * 2.0;
    tent += texture(coarseTex, clamp(uv + texel * vec2( 1.0,  1.0), minUv, maxUv)).rgb * 1.0;
    tent *= (1.0 / 16.0);

    vec2 uvFine = (dstPx + vec2(0.5)) / max(dstSz, vec2(1.0));
    vec3 fine = texture(fineTex, uvFine).rgb;
    outC = vec4(tent + pc.fineBlend * fine, 1.0);
}
