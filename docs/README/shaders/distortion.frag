#version 300 es

precision highp float;

uniform sampler2D uTexture;
uniform vec2 uResolution;
uniform float uTime;

/*
    @default distortionFrequency 30.0
    @default distortionAmount 0.0035
    @default distortionSpeed 1.0
    @default distortionStrengthBase 0.15
    @default distortionStrengthAmount 0.15
    @default distortionStrengthSpeed 1.0

    // 0.0 = original colors
    @default chromaticAmount 0.0
    @default chromaticVariation 0.0
    @default chromaticSpeed 2.0

    // 1.0 + 0.0 = original brightness
    @default scanlineFrequency 0.5
    @default scanlineBase 1.0
    @default scanlineAmount 0.25

    // 1.0 + 0.0 = original brightness
    @default flickerBase 1.0
    @default flickerAmount 0.1
    @default flickerFrequency 1.0

    // 0.0 = no vignette
    @default vignetteStrength 0.1
    @default vignetteRadius 0.5
    @default vignetteSoftness 0.5
*/

uniform float distortionFrequency;
uniform float distortionAmount;
uniform float distortionSpeed;
uniform float distortionStrengthBase;
uniform float distortionStrengthAmount;
uniform float distortionStrengthSpeed;

uniform float chromaticAmount;
uniform float chromaticVariation;
uniform float chromaticSpeed;

uniform float scanlineFrequency;
uniform float scanlineBase;
uniform float scanlineAmount;

uniform float flickerBase;
uniform float flickerAmount;
uniform float flickerFrequency;

uniform float vignetteStrength;
uniform float vignetteRadius;
uniform float vignetteSoftness;

in vec2 vUv;
out vec4 FragColor;

void main() {
    vec2 uv = vUv;

    float wave = sin(uv.y * distortionFrequency + uTime * distortionSpeed) * distortionAmount;
    float distortionStrength = distortionStrengthBase + distortionStrengthAmount * sin(uTime * distortionStrengthSpeed);
    uv.x += wave * distortionStrength;

    float chromatic = chromaticAmount + chromaticVariation * sin(uTime * chromaticSpeed);

    float r = texture(uTexture, uv + vec2(chromatic, 0.0)).r;
    float g = texture(uTexture, uv).g;
    float b = texture(uTexture, uv - vec2(chromatic, 0.0)).b;
    vec4 color = texture(uTexture, uv);

    float scanline = scanlineBase + scanlineAmount * sin(uv.y * uResolution.y * scanlineFrequency);
    float flicker = flickerBase + flickerAmount * sin(uTime * flickerFrequency);

    vec2 centeredUv = uv - 0.5;
    float distanceFromCenter = length(centeredUv);
    float vignette = smoothstep(vignetteRadius, vignetteRadius + vignetteSoftness, distanceFromCenter);
    float vignetteFactor = 1.0 - vignette * vignetteStrength;

    FragColor = vec4(
        r * scanline * flicker * vignetteFactor,
        g * scanline * flicker * vignetteFactor,
        b * scanline * flicker * vignetteFactor,
        color.a
    );
}