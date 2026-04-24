#version 450
// Weighted blended OIT: glass/crystal — tint over opaque HDR, McGuire-style weights (simplified 2D).
layout(set = 0, binding = 0) uniform sampler2D albedo;
layout(set = 1, binding = 0) uniform sampler2D hdrOpaque;
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outAccum;
layout(location = 1) out vec4 outReveal;
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
    vec2 suv = gl_FragCoord.xy / pc.screenRot.xy;
    vec3 bg = texture(hdrOpaque, suv).rgb;
    vec4 al = texture(albedo, vUv) * pc.colorAlpha;
    float a = clamp(al.a, 0.0, 1.0);
    if (a < 0.004) discard;
    vec3 rgb = mix(bg, al.rgb, a);
    float w = clamp(pow(a, 2.0) * 40.0 + 0.01, 0.05, 200.0);
    vec3 premul = rgb * a;
    outAccum = vec4(premul * w, a * w);
    outReveal = vec4(a * w, 0.0, 0.0, 1.0);
}
