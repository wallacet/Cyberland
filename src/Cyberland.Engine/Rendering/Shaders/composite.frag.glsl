#version 450
// Tonemap: lit + emissive + bloom. Vulkan FragCoord origin top-left. Use floor+0.5 so UVs are pixel centers
// whether FragCoord is integer grid or (x+0.5,y+0.5); matches bloom_extract and bloom chain sampling.
layout(set = 0, binding = 0) uniform sampler2D hdrTex;
layout(set = 0, binding = 1) uniform sampler2D emTex;
layout(set = 0, binding = 2) uniform sampler2D bloomTex;
layout(location = 0) out vec4 outC;
layout(push_constant) uniform Pp {
    float bloom;
    float exposure;
    float saturation;
    float emissiveHdrGain;
    // 0 = swapchain sRGB format encodes on write (output linear); 1 = apply pow(1/2.2) for UNORM swapchain.
    float applyManualDisplayGamma;
    float tonemapEnabled;
    float pad0;
    float pad1;
    vec4 colorGradingShadows;
    vec4 colorGradingMidtones;
    vec4 colorGradingHighlights;
} pp;

const vec3 LumW = vec3(0.299, 0.587, 0.114);

void main() {
    vec2 fullSizeTexels = vec2(textureSize(hdrTex, 0));
    vec2 texelUv = (floor(gl_FragCoord.xy) + vec2(0.5)) / fullSizeTexels;

    vec3 h = texture(hdrTex, texelUv).rgb;
    vec3 e = texture(emTex, texelUv).rgb;
    vec3 bloomSample = texture(bloomTex, texelUv).rgb;
    vec3 c = h + e * pp.emissiveHdrGain + bloomSample * pp.bloom;
    c *= pp.exposure;

    float L = dot(c, LumW);
    c = mix(vec3(L), c, pp.saturation);

    // Reinhard tonemap (skip when disabled for linear debug output).
    vec3 tonemapped = pp.tonemapEnabled > 0.5 ? c / (c + vec3(1.0)) : c;

    // Color grading: tint shadow / midtone / highlight luminance bands.
    float Lm = dot(tonemapped, LumW);
    float shadowW = 1.0 - smoothstep(0.0, 0.35, Lm);
    float highlightW = smoothstep(0.5, 1.0, Lm);
    float midW = 1.0 - shadowW - highlightW;
    vec3 graded = tonemapped * (pp.colorGradingShadows.rgb * shadowW
                              + pp.colorGradingMidtones.rgb * midW
                              + pp.colorGradingHighlights.rgb * highlightW);

    if (pp.applyManualDisplayGamma > 0.5)
        outC = vec4(pow(graded, vec3(1.0 / 2.2)), 1.0);
    else
        outC = vec4(graded, 1.0);
}
