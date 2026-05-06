#version 450
// Weighted blended OIT (McGuire-style weights).
// Keep reveal/transmittance in [0,1] so OneMinusSrcColor blending remains physically valid.
layout(set = 0, binding = 0) uniform sampler2D albedo;
layout(location = 0) in vec2 vUv;
layout(location = 1) in vec4 vColorAlpha;
layout(location = 0) out vec4 outAccum;
layout(location = 1) out vec4 outReveal;
void main() {
    vec4 al = texture(albedo, vUv) * vColorAlpha;
    float a = clamp(al.a, 0.0, 1.0);
    if (a < 0.004) discard;
    vec3 premul = al.rgb * a;
    float w = clamp(pow(a, 2.0) * 40.0 + 0.01, 0.05, 200.0);
    outAccum = vec4(premul * w, a * w);
    outReveal = vec4(a, 0.0, 0.0, 1.0);
}
