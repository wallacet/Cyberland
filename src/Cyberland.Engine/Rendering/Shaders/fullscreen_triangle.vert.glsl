#version 450
// Three-vertex fullscreen-triangle helper used by post / JFA / SDF stages. Emits clip-space coordinates that span
// the entire framebuffer with a single CmdDraw(3, 1, 0, 0). Output UV is in [0,1] +Y down — matches Vulkan
// framebuffer texel space (no flip required for sampler2D).
layout(location = 0) out vec2 vUv;
void main() {
    vec2 pos = vec2(float((gl_VertexIndex << 1) & 2), float(gl_VertexIndex & 2)) * 2.0 - 1.0;
    vUv = pos * 0.5 + 0.5;
    gl_Position = vec4(pos, 0.0, 1.0);
}
