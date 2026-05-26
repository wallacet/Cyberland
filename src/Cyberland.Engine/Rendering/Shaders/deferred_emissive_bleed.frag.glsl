#version 450
// Additive: albedo * emissiveScene (linear), matches former sprite_lit bleed term.
// emissiveToHdrGain controls how much emissive feeds the HDR scene color via this bleed path;
// composite.frag.glsl adds emissive again from the dedicated emissive texture with the same gain.
layout(set = 0, binding = 0) uniform sampler2D gbufAlbedo;
layout(set = 1, binding = 0) uniform sampler2D emissiveScene;
layout(location = 0) out vec4 outBleed;
layout(push_constant) uniform Pc {
    vec2 screenSize;
    float emissiveToHdrGain;
    float padPc;
} pc;
void main() {
    vec2 texelUv = (floor(gl_FragCoord.xy) + vec2(0.5)) / pc.screenSize;
    vec3 base = texture(gbufAlbedo, texelUv).rgb;
    vec3 sceneEm = texture(emissiveScene, texelUv).rgb;
    outBleed = vec4(base * sceneEm * pc.emissiveToHdrGain, 0.0);
}
