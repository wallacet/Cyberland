#version 450
layout(set = 0, binding = 0) uniform sampler2D srcTex;
layout(location = 0) out vec4 outC;
layout(push_constant) uniform BloomGaussianPc {
    float dirX;
    float dirY;
    float radiusScale;
    float pad;
} pc;

void main() {
    vec2 srcSizeTexels = vec2(textureSize(srcTex, 0));
    vec2 srcUv = (floor(gl_FragCoord.xy) + vec2(0.5)) / srcSizeTexels;
    vec2 stepUv = vec2(pc.dirX, pc.dirY) * pc.radiusScale / srcSizeTexels;
    const float wg[5] = float[](0.2270270270, 0.1945945946, 0.1216216216, 0.0540540541, 0.0162162162);

    vec3 c = texture(srcTex, srcUv).rgb * wg[0];
    for (int i = 1; i <= 4; i++) {
        vec2 o = stepUv * float(i);
        c += (texture(srcTex, srcUv + o).rgb + texture(srcTex, srcUv - o).rgb) * wg[i];
    }
    outC = vec4(c, 1.0);
}
