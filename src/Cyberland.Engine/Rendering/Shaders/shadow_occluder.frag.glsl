#version 450
// Shadow occluder rasterizer: writes 1.0 into the R8 mask where a CastsShadow sprite covers a swapchain pixel.
// The mask feeds the JFA seed; values are interpreted as "this texel is inside an occluder."
layout(set = 0, binding = 0) uniform sampler2D albedo;
layout(location = 0) in vec2 vUv;
layout(location = 1) in float vAlpha;
layout(location = 0) out float outMask;

void main() {
    vec4 al = texture(albedo, vUv);
    float coverage = al.a * vAlpha;
    if (coverage < 0.02)
        discard;
    outMask = 1.0;
}
