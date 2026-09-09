#version 300 es

precision highp float;

/*
    @default textureFidelity 0.75
    @default alphaPreservation 1.0
    @default brightness 2.0
    @default totalAlpha 1.0
    @default effectAlpha 0.6
    @default noiseScale 2.0

    @default color1 [1.0, 0.0, 0.0]
    @default color2 [0.0, 1.0, 0.0]
    @default color3 [0.0, 0.5, 1.0]

    @default randomX 12.9898
    @default randomY 78.233
    @default randomMultiplier 43758.5453

    @default noiseTime1X 0.7
    @default noiseTime1Y 0.5
    @default noiseTime2X 1.3
    @default noiseTime2Y 0.9
    @default noiseTime3X 1.1
    @default noiseTime3Y 1.7

    @default sineCycles 6.2831
    @default sineTimeScale 1.0
    @default weightPhase1 0.0
    @default weightPhase2 2.0
    @default weightPhase3 4.0

    @default brightnessDifferenceThreshold 0.5
    @default textureFidelityAmount 0.25
    @default weightEpsilon 0.0001
*/

uniform sampler2D uTexture;
uniform vec2 uResolution;
uniform float uTime;

uniform float textureFidelity;
uniform float alphaPreservation;
uniform float brightness;
uniform float totalAlpha;
uniform float effectAlpha;
uniform float noiseScale;

uniform vec3 color1;
uniform vec3 color2;
uniform vec3 color3;

uniform float randomX;
uniform float randomY;
uniform float randomMultiplier;

uniform float noiseTime1X;
uniform float noiseTime1Y;
uniform float noiseTime2X;
uniform float noiseTime2Y;
uniform float noiseTime3X;
uniform float noiseTime3Y;

uniform float sineCycles;
uniform float sineTimeScale;
uniform float weightPhase1;
uniform float weightPhase2;
uniform float weightPhase3;

uniform float brightnessDifferenceThreshold;
uniform float textureFidelityAmount;
uniform float weightEpsilon;

in vec2 vUv;
out vec4 FragColor;

const float PI = 3.14159265359;

float rand(vec2 co) {
    return fract(sin(dot(co, vec2(randomX, randomY))) * randomMultiplier);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);

    float a = rand(i);
    float b = rand(i + vec2(1.0, 0.0));
    float c = rand(i + vec2(0.0, 1.0));
    float d = rand(i + vec2(1.0, 1.0));

    vec2 u = f * f * (3.0 - 2.0 * f);

    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void main() {
    vec2 uv = vUv;
    float t = uTime * 2.0 * PI;

    vec4 original = texture(uTexture, uv);
    vec3 origRgb = original.rgb;
    float origAlpha = original.a;

    vec2 noiseUv = uv * noiseScale;

    float n1 = noise(noiseUv + vec2(sin(t * noiseTime1X), cos(t * noiseTime1Y)));
    float n2 = noise(noiseUv + vec2(cos(t * noiseTime2X), sin(t * noiseTime2Y)));
    float n3 = noise(noiseUv + vec2(sin(t * noiseTime3X), cos(t * noiseTime3Y)));

    float w1 = abs(sin(n1 * sineCycles + t * sineTimeScale + weightPhase1));
    float w2 = abs(sin(n2 * sineCycles + t * sineTimeScale + weightPhase2));
    float w3 = abs(sin(n3 * sineCycles + t * sineTimeScale + weightPhase3));

    float total = max(w1 + w2 + w3, weightEpsilon);

    w1 /= total;
    w2 /= total;
    w3 /= total;

    vec3 mixedColor = w1 * color1 + w2 * color2 + w3 * color3;
    mixedColor *= brightness;

    float origAvg = (origRgb.r + origRgb.g + origRgb.b) / 3.0;
    float newAvg = (mixedColor.r + mixedColor.g + mixedColor.b) / 3.0;

    if (abs(origAvg - newAvg) > brightnessDifferenceThreshold && newAvg > 0.0) {
        mixedColor = mix(mixedColor, origRgb, textureFidelity * textureFidelityAmount);
    }

    vec3 finalColor = mix(origRgb, mixedColor, effectAlpha);
    float finalAlpha = mix(effectAlpha, origAlpha, alphaPreservation);
    finalAlpha *= totalAlpha;

    FragColor = vec4(finalColor, finalAlpha);
}