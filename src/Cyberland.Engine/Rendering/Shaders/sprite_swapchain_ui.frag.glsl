#version 450
// After HDR composite: straight-alpha over the swapchain (glyph atlas is premultiplied; unpremul for correct blend).
layout(set = 0, binding = 0) uniform sampler2D albedo;
layout(set = 1, binding = 0) uniform sampler2D normalMap;
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(push_constant) uniform Pc {
    vec4 centerHalf;
    vec4 uvRect;
    vec4 colorAlpha;
    vec4 emissive;
    vec4 viewportPhysical;
    vec4 screenRot;
    int mode;
    int useEmissiveMap;
} pc;
void main() {
    vec4 al = texture(albedo, vUv) * pc.colorAlpha;
    if (al.a < 0.02) discard;
    // normalMap is bound (shared layout with G-buffer pass) but unused here.
    float a = al.a;
    vec3 rgb = a > 1e-4 ? al.rgb / a : vec3(0.0);
    outColor = vec4(rgb, a);
}
