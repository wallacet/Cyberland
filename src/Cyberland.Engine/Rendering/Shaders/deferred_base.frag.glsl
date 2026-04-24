#version 450
// Fullscreen: ambient + N directionals + M spots from G-buffer (linear). Point lights handled separately.
layout(set = 0, binding = 0) uniform sampler2D gbufAlbedo;
layout(set = 0, binding = 1) uniform sampler2D gbufNormal;
layout(set = 1, binding = 0) uniform LightingUbo {
    vec4 ambient;
    vec4 counts; // x = nDir, y = nSpot
} ubo;
layout(std430, set = 1, binding = 1) readonly buffer DirectionalSsbo {
    vec4 d[];
} dirBuf;
layout(std430, set = 1, binding = 2) readonly buffer SpotSsbo {
    vec4 s[];
} spotBuf;
layout(location = 0) out vec4 outHdr;
layout(push_constant) uniform Pc {
    vec2 screenSize;
    vec2 padPc;
} pc;
void main() {
    vec2 uv = (floor(gl_FragCoord.xy) + vec2(0.5)) / pc.screenSize;
    vec4 al = texture(gbufAlbedo, uv);
    if (al.a < 0.001) {
        outHdr = vec4(0.02, 0.02, 0.06, 1.0);
        return;
    }
    vec3 base = al.rgb;
    vec2 nEnc = texture(gbufNormal, uv).xy;
    vec2 nxy = nEnc * 2.0 - 1.0;
    float nz = sqrt(max(1.0 - dot(nxy, nxy), 0.0));
    vec3 N = normalize(vec3(nxy, nz));
    // Summed linear RGB in ambient.xyz (see CPU); no separate intensity multiply.
    vec3 amb = base * ubo.ambient.xyz;
    vec3 lit = amb;
    uint nDir = uint(ubo.counts.x + 0.5);
    for (uint i = 0u; i < nDir; i++) {
        vec4 di = dirBuf.d[i * 2u + 0u];
        vec4 ci = dirBuf.d[i * 2u + 1u];
        vec3 Ld = normalize(vec3(di.xy, 0.8));
        float ndl = max(dot(N, Ld), 0.0);
        lit += base * ci.rgb * (di.z * ndl);
    }
    uint nSpot = uint(ubo.counts.y + 0.5);
    vec2 fragWorld = vec2(gl_FragCoord.x, pc.screenSize.y - gl_FragCoord.y);
    for (uint j = 0u; j < nSpot; j++) {
        vec4 spr = spotBuf.s[j * 3u + 0u];
        vec4 sdc = spotBuf.s[j * 3u + 1u];
        vec4 sci = spotBuf.s[j * 3u + 2u];
        vec2 toSpot = fragWorld - spr.xy;
        float sDist = length(toSpot);
        vec2 sDir = normalize(sdc.xy);
        vec2 toFrag = sDist > 1e-4 ? normalize(toSpot) : sDir;
        float coneDot = dot(sDir, toFrag);
        float coneT = smoothstep(sdc.w, sdc.z, coneDot);
        float sAtt = clamp(1.0 - sDist / max(spr.z, 1e-4), 0.0, 1.0) * coneT;
        lit += base * sci.rgb * (sci.w * sAtt);
    }
    outHdr = vec4(lit, al.a);
}
