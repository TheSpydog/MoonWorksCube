#version 450

layout(location = 0) in vec3 pos;
layout(location = 1) in vec4 color;

layout(location = 0) out vec4 vColor;

layout(set = 2, binding = 0) uniform UBO
{
	mat4 vp;
} ubo;

void main()
{
    gl_Position = ubo.vp * vec4(pos, 1.0);
    vColor = color;
}
