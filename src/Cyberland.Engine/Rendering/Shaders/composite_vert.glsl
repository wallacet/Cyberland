#version 450
// Fullscreen triangle (gl_VertexIndex 0..2): shared by bloom/composite post passes.
// Per-vertex varyings cannot cover the full [0,1]^2 texture square with the usual 3 UV corners
// (0,0),(1,0),(0,1) — that triangle only satisfies u+v<=1, so the lower-right of the framebuffer
// never maps to high (u,v). Fragment stages use gl_FragCoord for stable full-surface sampling.
void main() {
    vec2 pos = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(pos * 2.0 - 1.0, 0.0, 1.0);
}
