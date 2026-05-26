#version 450
// After HDR composite: straight-alpha over the swapchain (unpremul for correct blend).
// normalMap (set 1) is declared but not sampled — kept so the dual-texture descriptor layout stays valid.
layout(set = 0, binding = 0) uniform sampler2D albedo;
layout(set = 1, binding = 0) uniform sampler2D normalMap;
layout(location = 0) in vec2 vUv;
layout(location = 1) in vec4 vColorAlpha;
layout(location = 0) out vec4 outColor;
void main() {
    vec4 al = texture(albedo, vUv) * vColorAlpha;
    if (al.a < 0.02) discard;
    float a = al.a;
    vec3 rgb = a > 1e-4 ? al.rgb / a : vec3(0.0);
    outColor = vec4(rgb, a);
}
