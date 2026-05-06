#version 450
// Instanced sprite quad: per-vertex quad inPos; per-instance transform and material in locations 1–5.
// Push holds only the letterboxed viewport rect (swapchain pixels); matches dynamic VkViewport scissor.
layout(push_constant) uniform Pc {
    vec4 viewportPhysical;
} pc;
layout(location = 0) in vec2 inPos;
layout(location = 1) in vec4 inCenterHalf;
layout(location = 2) in vec4 inUvRect;
layout(location = 3) in vec4 inColorAlpha;
layout(location = 4) in vec4 inEmissive;
layout(location = 5) in vec4 inRotAndFlags;
layout(location = 0) out vec2 vUv;
layout(location = 1) out vec4 vColorAlpha;
layout(location = 2) out vec4 vEmissive;
layout(location = 3) flat out int vUseEmissiveMap;
void main() {
    float rot = inRotAndFlags.z;
    float c = cos(rot);
    float s = sin(rot);
    vec2 lh = inPos * inCenterHalf.zw;
    vec2 rotLh = mat2(c, -s, s, c) * lh;
    vec2 px = inCenterHalf.xy + rotLh;
    vUv = mix(inUvRect.xy, inUvRect.zw, inPos * 0.5 + 0.5);
    vColorAlpha = inColorAlpha;
    vEmissive = inEmissive;
    vUseEmissiveMap = int(inRotAndFlags.w + 0.5);
    float physW = max(pc.viewportPhysical.z, 1.0);
    float physH = max(pc.viewportPhysical.w, 1.0);
    float ndcX = (px.x - pc.viewportPhysical.x) / physW * 2.0 - 1.0;
    float ndcY = (px.y - pc.viewportPhysical.y) / physH * 2.0 - 1.0;
    gl_Position = vec4(ndcX, ndcY, 0.0, 1.0);
}
