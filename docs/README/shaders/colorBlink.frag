#version 300 es

precision highp float;

uniform sampler2D uTexture;
uniform vec2 uResolution;
uniform float uTime;

/*
    @default flashColorA [0.95, 0.98, 1.0]
    @default flashColorB [0.55, 0.80, 1.0]
    @default flashAmount 1.0
    @default alphaPreservation 0.0
    @default effectAlpha 1.0
*/

uniform vec3 flashColorA;
uniform vec3 flashColorB;
uniform float flashAmount;
uniform float alphaPreservation;
uniform float effectAlpha;

in vec2 vUv;
out vec4 FragColor;

const float TAU = 6.28318530718;

void main() {
    vec4 color = texture(uTexture, vUv);
    float flash = 0.5 + 0.5 * sin(uTime * TAU);
    vec3 flashColor = mix(flashColorA, flashColorB, flash);
    vec3 flashed = color.rgb * flashColor;
    color.rgb = mix(color.rgb, flashed, flashAmount);

    float finalAlpha;
    if (color.a <= 0.0) {
        finalAlpha = 0.0;
    } else {
        finalAlpha = mix(1.0, color.a, alphaPreservation);
    }

    vec3 finalColor = mix(texture(uTexture, vUv).rgb, color.rgb, clamp(effectAlpha, 0.0, 1.0));

    FragColor = vec4(finalColor, finalAlpha);
}