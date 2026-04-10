#version 450
// Fullscreen: ambient + directional + spot from G-buffer (linear). Point lights handled separately.
layout(set = 0, binding = 0) uniform sampler2D gbufAlbedo;
layout(set = 0, binding = 1) uniform sampler2D gbufNormal;
layout(set = 1, binding = 0) uniform LightingUbo {
    vec4 ambient;
    vec4 dirDirIntensity;
    vec4 dirColor;
    vec4 pointPosRadius;
    vec4 pointColorIntensity;
    vec4 pointFalloff;
    vec4 spotPosRadius;
    vec4 spotDirCosOuter;
    vec4 spotColorIntensity;
} lights;
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
    vec3 Ld = normalize(vec3(lights.dirDirIntensity.xy, 0.8));
    float ndl = max(dot(N, Ld), 0.0);
    vec3 directional = base * lights.dirColor.rgb * (lights.dirDirIntensity.z * ndl);
    vec3 amb = base * lights.ambient.rgb * lights.ambient.w;
    vec3 lit = amb + directional;
    vec2 fragWorld = vec2(gl_FragCoord.x, pc.screenSize.y - gl_FragCoord.y);
    vec2 toSpot = fragWorld - lights.spotPosRadius.xy;
    float sDist = length(toSpot);
    vec2 sDir = normalize(lights.spotDirCosOuter.xy);
    vec2 toFrag = sDist > 1e-4 ? normalize(toSpot) : sDir;
    float coneDot = dot(sDir, toFrag);
    float coneT = smoothstep(lights.spotDirCosOuter.w, lights.spotDirCosOuter.z, coneDot);
    float sAtt = clamp(1.0 - sDist / max(lights.spotPosRadius.z, 1e-4), 0.0, 1.0) * coneT;
    lit += base * lights.spotColorIntensity.rgb * (lights.spotColorIntensity.w * sAtt);
    outHdr = vec4(lit, al.a);
}
