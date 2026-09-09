#version 300 es

precision highp float;

/*
    @default curve 0.025

    @default distortionFrequency 20.0
    @default distortionAmount 0.00025

    @default scanlineFrequency 1.0
    @default scanlineBase 0.96
    @default scanlineAmount 0.04

    @default flickerBase 0.985
    @default flickerAmount 0.015
    @default flickerFrequency 3.0

    @default vignetteStrength 0.12
*/

uniform sampler2D uTexture;
uniform vec2 uResolution;
uniform float uTime;

uniform float curve;
uniform float distortionFrequency;
uniform float distortionAmount;
uniform float scanlineFrequency;
uniform float scanlineBase;
uniform float scanlineAmount;
uniform float flickerBase;
uniform float flickerAmount;
uniform float flickerFrequency;
uniform float vignetteStrength;

in vec2 vUv;
out vec4 FragColor;

const float PI = 3.14159265359;

void main() {
    float t = uTime * 2.0 * PI;
    vec2 uv = vUv;

    if (curve != 0.0) {
        vec2 p = uv * 2.0 - 1.0;
        p.x *= 1.0 + curve * p.y * p.y;
        p.y *= 1.0 + curve * p.x * p.x;
        uv = p * 0.5 + 0.5;
    }

    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
        FragColor = vec4(0.0);
        return;
    }

    float distortion = sin(uv.y * distortionFrequency + t) * distortionAmount;
    uv.x += distortion;

    vec4 source = texture(uTexture, uv);
    float alpha = source.a;

    float r = texture(uTexture, uv).r;
    float g = texture(uTexture, uv).g;
    float b = texture(uTexture, uv).b;
    vec3 color = vec3(r, g, b);

    float scanline = sin(uv.y * uResolution.y * PI * scanlineFrequency + t);
    color *= scanlineBase + scanline * scanlineAmount;

    float flicker = flickerBase + flickerAmount * sin(t * flickerFrequency);
    color *= flicker;

    vec2 vignetteUv = uv * 2.0 - 1.0;
    float vignette = 1.0 - dot(vignetteUv, vignetteUv) * vignetteStrength;
    color *= vignette;

    FragColor = vec4(color, alpha);
}