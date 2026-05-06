#version 450
// Linear emissive radiance: optional emissive map (set 1); per-instance tint from vertex stage.
layout(set = 0, binding = 0) uniform sampler2D albedo;
layout(set = 1, binding = 0) uniform sampler2D emissiveMap;
layout(location = 0) in vec2 vUv;
layout(location = 1) in vec4 vColorAlpha;
layout(location = 2) in vec4 vEmissive;
layout(location = 3) flat in int vUseEmissiveMap;
layout(location = 0) out vec4 outEm;
void main() {
    vec4 a = texture(albedo, vUv) * vColorAlpha;
    vec3 eMap = texture(emissiveMap, vUv).rgb;
    vec3 e = vUseEmissiveMap > 0
        ? eMap * vEmissive.rgb * vEmissive.w
        : a.rgb * vEmissive.rgb * vEmissive.w;
    outEm = vec4(e, a.a);
}
