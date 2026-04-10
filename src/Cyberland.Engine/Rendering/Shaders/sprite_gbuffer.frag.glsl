#version 450
// Opaque G-buffer: linear albedo (premultiplied alpha) + tangent-space normal encoded in RT1.
layout(set = 0, binding = 0) uniform sampler2D albedo;
layout(set = 1, binding = 0) uniform sampler2D normalMap;
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outAlbedo;
layout(location = 1) out vec4 outNormal;
layout(push_constant) uniform Pc {
    vec4 centerHalf;
    vec4 uvRect;
    vec4 colorAlpha;
    vec4 emissive;
    vec4 screenRot;
    int mode;
    int useEmissiveMap;
} pc;
void main() {
    vec4 al = texture(albedo, vUv) * pc.colorAlpha;
    if (al.a < 0.02) discard;
    vec3 nSample = texture(normalMap, vUv).rgb * 2.0 - 1.0;
    float z = sqrt(max(1.0 - dot(nSample.xy, nSample.xy), 0.0));
    vec3 N = normalize(vec3(nSample.xy, z));
    outAlbedo = al;
    outNormal = vec4(N.xy * 0.5 + 0.5, 0.0, 1.0);
}
