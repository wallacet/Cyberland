#version 450
// Linear emissive radiance: optional emissive map (set 1) is first-class; else albedo × tint × intensity.
layout(set = 0, binding = 0) uniform sampler2D albedo;
layout(set = 1, binding = 0) uniform sampler2D emissiveMap;
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outEm;
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
    vec4 a = texture(albedo, vUv) * pc.colorAlpha;
    vec3 eMap = texture(emissiveMap, vUv).rgb;
    vec3 e = pc.useEmissiveMap > 0
        ? eMap * pc.emissive.rgb * pc.emissive.w
        : a.rgb * pc.emissive.rgb * pc.emissive.w;
    outEm = vec4(e, a.a);
}
