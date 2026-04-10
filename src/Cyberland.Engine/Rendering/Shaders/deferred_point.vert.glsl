#version 450
// Instanced quad (6 verts): each instance is one point light; bounds are screen-space circle in pixels.
layout(location = 0) in vec2 inPos;
layout(location = 1) flat out uint vLightId;
layout(std430, set = 1, binding = 0) readonly buffer PointSsbo {
    vec4 data[];
} points;
layout(push_constant) uniform Pc {
    vec4 screen;
} pc;
void main() {
    vLightId = gl_InstanceIndex;
    uint lid = gl_InstanceIndex;
    vec4 pr = points.data[lid * 2u + 0u];
    vec2 centerPx = vec2(pr.x, pc.screen.y - pr.y);
    float R = max(pr.z, 4.0);
    vec2 corner = inPos * R * 1.15;
    vec2 px = centerPx + corner;
    float ndcX = px.x / pc.screen.x * 2.0 - 1.0;
    float ndcY = px.y / pc.screen.y * 2.0 - 1.0;
    gl_Position = vec4(ndcX, ndcY, 0.0, 1.0);
}
