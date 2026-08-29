#version 300 es

precision highp float;

uniform sampler2D uTexture;
uniform vec2 uResolution;
uniform float uTime;

in vec2 vUv;

out vec4 FragColor;

void main()
{
    vec2 uv = vUv;

    // Horizontal wave distortion
    float wave =
        sin(
            uv.y * 40.0 +
            uTime * 4.0
        ) * 0.015;

    // Make the distortion much stronger
    // toward the center of the element.
    float strength =
        0.5 + 0.5 * sin(uTime * 2.0);

    uv.x += wave * strength;

    // RGB channel separation
    float chromatic =
        0.012 + 0.008 * sin(uTime * 3.0);

    float r =
        texture(
            uTexture,
            uv + vec2(chromatic, 0.0)
        ).r;

    float g =
        texture(
            uTexture,
            uv
        ).g;

    float b =
        texture(
            uTexture,
            uv - vec2(chromatic, 0.0)
        ).b;

    vec4 color =
        texture(
            uTexture,
            uv
        );

    FragColor =
        vec4(
            r,
            g,
            b,
            color.a
        );
}
