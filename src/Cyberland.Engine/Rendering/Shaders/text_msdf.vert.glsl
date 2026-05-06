#version 450

layout(location = 0) in vec2 inPos;
layout(location = 1) in vec4 inCenterHalfPx;
layout(location = 2) in vec4 inUvRect;
layout(location = 3) in vec4 inColor;
layout(location = 4) in vec4 inMsdfParams;

layout(location = 0) out vec2 vUv;
layout(location = 1) out vec4 vColor;
layout(location = 2) out float vMsdfPixelRange;

layout(push_constant) uniform PushData
{
    vec4 viewportPhysical;
    vec2 screenSize;
    float edgeSharpness;
    float pad0;
} pushData;

void main()
{
    vec2 uv01 = inPos * 0.5 + 0.5;
    vec2 px = inCenterHalfPx.xy + inPos * inCenterHalfPx.zw;
    vec2 vpMin = pushData.viewportPhysical.xy;
    vec2 vpSize = pushData.viewportPhysical.zw;
    float physW = max(vpSize.x, 1.0);
    float physH = max(vpSize.y, 1.0);
    vec2 ndc;
    ndc.x = ((px.x - vpMin.x) / physW) * 2.0 - 1.0;
    ndc.y = ((px.y - vpMin.y) / physH) * 2.0 - 1.0;
    gl_Position = vec4(ndc, 0.0, 1.0);

    vUv = mix(inUvRect.xy, inUvRect.zw, uv01);
    vColor = inColor;
    vMsdfPixelRange = inMsdfParams.x;
}
