#version 450
layout(set = 0, binding = 0) uniform sampler2D gbufAlbedo;
layout(set = 0, binding = 1) uniform sampler2D gbufNormal;
layout(location = 1) flat in uint vLightId;
layout(std430, set = 1, binding = 0) readonly buffer PointSsbo {
    vec4 data[];
} points;
layout(location = 0) out vec4 outAdd;
layout(push_constant) uniform Pc {
    vec4 screen;
} pc;
void main() {
    uint lid = vLightId;
    vec4 pr = points.data[lid * 2u + 0u];
    vec4 ci = points.data[lid * 2u + 1u];
    vec2 uv = (floor(gl_FragCoord.xy) + vec2(0.5)) / pc.screen.xy;
    vec4 al = texture(gbufAlbedo, uv);
    if (al.a < 0.001) {
        outAdd = vec4(0.0);
        return;
    }
    vec3 base = al.rgb;
    vec2 nEnc = texture(gbufNormal, uv).xy;
    vec2 nxy = nEnc * 2.0 - 1.0;
    float nz = sqrt(max(1.0 - dot(nxy, nxy), 0.0));
    vec3 N = normalize(vec3(nxy, nz));
    vec2 centerPx = vec2(pr.x, pc.screen.y - pr.y);
    vec2 fragPx = gl_FragCoord.xy;
    vec2 toPoint = centerPx - fragPx;
    float pDist = length(toPoint);
    float r = max(pr.z, 1e-4);
    float t = clamp(pDist / r, 0.0, 1.0);
    float expFall = max(pr.w, 0.1);
    float pAtt = pow(max(1.0 - t, 0.0), expFall);
    vec3 contrib = base * ci.rgb * (ci.a * pAtt);
    outAdd = vec4(contrib, 0.0);
}
