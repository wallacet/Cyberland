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
    float emissiveBloomGain;
    // 0 = swapchain sRGB format encodes on write (output linear m); 1 = apply pow(1/2.2) for UNORM swapchain.
    float applyManualDisplayGamma;
    float pad1;
} pp;

const vec3 LumW = vec3(0.299, 0.587, 0.114);

void main() {
    vec2 fullSz = vec2(textureSize(hdrTex, 0));
    vec2 uv = (floor(gl_FragCoord.xy) + vec2(0.5)) / fullSz;

    vec3 h = texture(hdrTex, uv).rgb;
    vec3 e = texture(emTex, uv).rgb;
    vec3 bloom = texture(bloomTex, uv).rgb;
    vec3 c = h + e * pp.emissiveHdrGain + bloom * pp.bloom;
    c *= pp.exposure;

    float L = dot(c, LumW);
    c = mix(vec3(L), c, pp.saturation);
    vec3 m = c / (c + vec3(1.0));
    if (pp.applyManualDisplayGamma > 0.5)
        outC = vec4(pow(m, vec3(1.0 / 2.2)), 1.0);
    else
        outC = vec4(m, 1.0);
}
