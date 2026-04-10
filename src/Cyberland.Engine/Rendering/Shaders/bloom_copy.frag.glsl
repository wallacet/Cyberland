#version 450
// Copy the top-left sub-rectangle of half-res bloom1 into a smaller bloomDown framebuffer (same pixel centers as upsample viewport).
layout(set = 0, binding = 0) uniform sampler2D bloomHalfTex;
layout(location = 0) out vec4 outC;

void main() {
    vec2 sz = vec2(textureSize(bloomHalfTex, 0));
    vec2 uv = (floor(gl_FragCoord.xy) + vec2(0.5)) / sz;
    outC = vec4(texture(bloomHalfTex, uv).rgb, 1.0);
}
