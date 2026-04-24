#version 450
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
layout(location = 0) in vec2 inPos;
layout(location = 0) out vec2 vUv;
void main() {
    float c = cos(pc.screenRot.z);
    float s = sin(pc.screenRot.z);
    vec2 lh = inPos * pc.centerHalf.zw;
    vec2 rot = mat2(c, -s, s, c) * lh;
    vec2 px = pc.centerHalf.xy + rot;
    vUv = mix(pc.uvRect.xy, pc.uvRect.zw, inPos * 0.5 + 0.5);
    // Clip space must match the dynamic VkViewport (letterboxed physical rect), not the full swapchain extent.
    float physW = max(pc.viewportPhysical.z, 1.0);
    float physH = max(pc.viewportPhysical.w, 1.0);
    float ndcX = (px.x - pc.viewportPhysical.x) / physW * 2.0 - 1.0;
    float ndcY = (px.y - pc.viewportPhysical.y) / physH * 2.0 - 1.0;
    gl_Position = vec4(ndcX, ndcY, 0.0, 1.0);
}
