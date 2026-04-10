#version 450
// Additive: albedo * emissiveScene (linear), matches former sprite_lit bleed term.
layout(set = 0, binding = 0) uniform sampler2D gbufAlbedo;
layout(set = 0, binding = 1) uniform sampler2D gbufNormal;
layout(set = 1, binding = 0) uniform sampler2D emissiveScene;
layout(location = 0) out vec4 outBleed;
layout(push_constant) uniform Pc {
    vec2 screenSize;
    vec2 padPc;
} pc;
void main() {
    vec2 uv = (floor(gl_FragCoord.xy) + vec2(0.5)) / pc.screenSize;
    vec3 base = texture(gbufAlbedo, uv).rgb;
    vec3 sceneEm = texture(emissiveScene, uv).rgb;
    texture(gbufNormal, uv);
    outBleed = vec4(base * sceneEm * 0.85, 0.0);
}
