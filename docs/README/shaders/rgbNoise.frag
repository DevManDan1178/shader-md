#version 300 es

precision highp float;

// from https://www.shadertoy.com/view/DddGzX

/*
    @default effectAlpha 1.0
    @default totalAlpha 1.0
*/

uniform sampler2D uTexture;
uniform vec2 uResolution;
uniform float uTime;
uniform float effectAlpha;
uniform float totalAlpha;

in vec2 vUv;
out vec4 FragColor;

float rand(vec2 uv, float t) {
    return fract(sin(dot(uv, vec2(1225.6548, 321.8942))) * 4251.4865 + t);
}

void main() {
    vec2 uv = vUv;
    vec4 original = texture(uTexture, uv);

    float r = rand(uv, uTime);
    float g = rand(uv + 0.1, uTime);
    float b = rand(uv + 0.2, uTime);

    vec3 noiseColor = vec3(r, g, b);
    vec3 finalColor = mix(original.rgb, noiseColor, effectAlpha);

    FragColor = vec4(finalColor, original.a * totalAlpha);
}
