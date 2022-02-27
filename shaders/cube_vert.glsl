#version 450

layout(location = 0) in vec3 inPos;
layout(location = 1) in vec4 inColor;

layout(location = 0) out vec4 vColor;

layout(set = 2, binding = 0) uniform UBO
{
	mat4 ViewProjection;
} ubo;

void main()
{
    gl_Position = ubo.ViewProjection * vec4(inPos, 1.0);
    vColor = inColor;
}
