#version 450

layout(set = 0, binding = 0) uniform sampler2D texAlbedo;

layout(location = 0) in vec2 vUv;
layout(location = 1) in vec4 vColor;
layout(location = 2) in float vMsdfPixelRange;

layout(location = 0) out vec4 outColor;

layout(push_constant) uniform PushData
{
    vec4 viewportPhysical;
    // screenSize.xy occupies bytes 16–23 for push-constant layout alignment with the C# TextMsdfPushData struct; not read by this stage.
    vec2 screenSize;
    float edgeSharpness;
    float pad0;
} pushData;

float median3(vec3 v)
{
    return max(min(v.r, v.g), min(max(v.r, v.g), v.b));
}

float screenPxRange(float pxRange, vec2 glyphUv)
{
    vec2 unitRange = vec2(pxRange) / vec2(textureSize(texAlbedo, 0));
    vec2 screenTexSize = vec2(1.0) / fwidth(glyphUv);
    return max(0.5 * dot(unitRange, screenTexSize), 1.0);
}

void main()
{
    vec4 mtsdf = textureLod(texAlbedo, vUv, 0.0);
    float msdfSigned = median3(mtsdf.rgb) - 0.5;
    float sdfSigned = mtsdf.a - 0.5;
    // Blend toward single-channel SDF near ambiguous channel crossings to reduce color-corner artifacts.
    float msdfAbs = abs(msdfSigned);
    float sdfBlend = clamp((0.06 - msdfAbs) / 0.06, 0.0, 1.0);
    float signedDistance = mix(msdfSigned, sdfSigned, sdfBlend);
    float pxDistance = signedDistance * screenPxRange(max(vMsdfPixelRange, 1.0), vUv) * pushData.edgeSharpness;
    float coverage = clamp(pxDistance + 0.5, 0.0, 1.0);
    float alpha = coverage * vColor.a;
    outColor = vec4(vColor.rgb, alpha);
}
