#version 300 es

precision highp float;

/*
    @default textureFidelity 0.75
    @default brightness 1.0

    @default color1 [1.0, 0.5, 0.5]
    @default color2 [0.5, 1.0, 0.5]
    @default color3 [0.5, 0.5, 1.5]
    @default color4 [0.5, 1, 1]
    @default effectAlpha 1.0

    @default noiseScale 3.0
    @default octaves 5
    @default lacunarity 1.25
    @default gain 0.5

    @default speed 3.0
    @default offsetRad 0.75
    @default warpAmp 0.2
    @default warpSpeed 1.0

    @default timeOffsetAmount 0.1
    @default orbitPhase1 1.7
    @default orbitPhase2 3.9
    @default orbitPhase3 5.5

    @default warpPhaseOffset 1.57079632679
    @default warpAxisOffset 7.3

    @default brightnessDifferenceThreshold 0.5
    @default textureFidelityAmount 0.25
    @default effectAmount 0.85
    @default weightEpsilon 0.00001
*/

uniform sampler2D uTexture;
uniform vec2 uResolution;
uniform float uTime;

uniform float textureFidelity;
uniform float brightness;

uniform vec3 color1;
uniform vec3 color2;
uniform vec3 color3;
uniform vec3 color4;

uniform float noiseScale;
uniform int octaves;
uniform float lacunarity;
uniform float gain;

uniform float speed;
uniform float offsetRad;
uniform float warpAmp;
uniform float warpSpeed;

uniform float timeOffsetAmount;
uniform float orbitPhase1;
uniform float orbitPhase2;
uniform float orbitPhase3;

uniform float warpPhaseOffset;
uniform float warpAxisOffset;

uniform float brightnessDifferenceThreshold;
uniform float textureFidelityAmount;
uniform float effectAmount;
uniform float effectAlpha;
uniform float weightEpsilon;

in vec2 vUv;
out vec4 FragColor;

const float PI = 3.14159265359;
const vec2 primeIshVec = vec2(7.13, 13.73);

float fadef(float t) {
    return t * t * (3.0 - 2.0 * t);
}

float noise2D(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);

    float a = fract(sin(dot(i, primeIshVec)));
    float b = fract(sin(dot(i + vec2(1.0, 0.0), primeIshVec)));
    float c = fract(sin(dot(i + vec2(0.0, 1.0), primeIshVec)));
    float d = fract(sin(dot(i + vec2(1.0, 1.0), primeIshVec)));

    vec2 u = vec2(fadef(f.x), fadef(f.y));

    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
    float sum = 0.0;
    float amp = 1.0;

    for (int i = 0; i < octaves; i++) {
        sum += amp * noise2D(p);
        p *= lacunarity;
        amp *= gain;
    }

    return sum;
}

void main() {
    vec2 uv = vUv;
    vec4 original = texture(uTexture, uv);
    vec3 origRgb = original.rgb;
    float origA = original.a;

    if (origA <= 0.0) {
        FragColor = vec4(0.0);
        return;
    }

    float t = uTime * 2.0 * PI;

    vec2 timeOffset = vec2(cos(t) - 1.0, sin(t)) * timeOffsetAmount;
    vec2 noiseUv = uv * noiseScale + timeOffset;

    vec2 off[4];

    off[0] = offsetRad * vec2(cos(t * speed), sin(t * speed));
    off[1] = offsetRad * vec2(cos(t * speed + orbitPhase1), sin(t * speed + orbitPhase1));
    off[2] = offsetRad * vec2(cos(t * speed + orbitPhase2), sin(t * speed + orbitPhase2));
    off[3] = offsetRad * vec2(cos(t * speed + orbitPhase3), sin(t * speed + orbitPhase3));

    float wt = t * warpSpeed;

    vec2 warpOffsetX = vec2(cos(wt) - 1.0, sin(wt));
    vec2 warpOffsetY = vec2(cos(wt + warpPhaseOffset) - 1.0, sin(wt + warpPhaseOffset));

    vec2 warp = vec2(
        fbm(noiseUv + warpOffsetX),
        fbm(noiseUv + warpOffsetY)
    ) * warpAmp;

    float n[4];

    for (int i = 0; i < 4; i++) {
        n[i] = fbm(noiseUv + off[i] + warp + vec2(float(i) * warpAxisOffset));
    }

    float sumW = n[0] + n[1] + n[2] + n[3] + weightEpsilon;

    vec3 mixedColor = (
        color1 * n[0] +
        color2 * n[1] +
        color3 * n[2] +
        color4 * n[3]
    ) / sumW;

    mixedColor *= brightness;

    float origAvg = (origRgb.r + origRgb.g + origRgb.b) / 3.0;
    float newAvg = (mixedColor.r + mixedColor.g + mixedColor.b) / 3.0;

    if (abs(origAvg - newAvg) > brightnessDifferenceThreshold && newAvg > 0.0) {
        mixedColor = mix(mixedColor, origRgb, textureFidelity * textureFidelityAmount);
    }

    vec3 effectColor = mix(origRgb, mixedColor, effectAmount);
    vec3 finalColor = mix(origRgb, effectColor, clamp(effectAlpha, 0.0, 1.0));

    FragColor = vec4(finalColor, origA);
}
