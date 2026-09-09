#version 300 es

precision highp float;

uniform sampler2D uTexture;
uniform vec2 uResolution;
uniform float uTime;

// from https://www.shadertoy.com/view/ltjXRV

/*
    @default tileScale 20.0
    @default slashWidth 0.025
    @default radialSpeed 2.0
    @default rotationSpeed 1.0
    @default radialRotationAmount 3.0

    @default colorA [0.0, 0.4, 0.4]
    @default colorB [0.0, 0.7, 0.4]
    @default colorC [0.0, 0.4, 1.0]
    @default colorD [0.0, 0.0, 0.4]
*/

uniform float tileScale;
uniform float slashWidth;
uniform float radialSpeed;
uniform float rotationSpeed;
uniform float radialRotationAmount;

uniform vec3 colorA;
uniform vec3 colorB;
uniform vec3 colorC;
uniform vec3 colorD;

in vec2 vUv;
out vec4 FragColor;

const float PI = 3.14159265359;

float random(vec2 st) {
    return fract(sin(dot(st, vec2(12.9898, 78.233))) * 43758.5453123);
}

float barra(vec2 st, float width) {
    float pct = smoothstep(st.x - width, st.x, st.y);
    pct *= 1.0 - smoothstep(st.x, st.x + width, st.y);
    return pct;
}

float contrabarra(vec2 st, float width) {
    float pct = smoothstep(st.x - width, st.x, 1.0 - st.y);
    pct *= 1.0 - smoothstep(st.x, st.x + width, 1.0 - st.y);
    return pct;
}

void main() {
    vec2 uv = vUv;
    float aspect = uResolution.x / uResolution.y;

    vec2 screenUv = uv - 0.5;
    screenUv.x *= aspect;

    float r = length(screenUv) * 2.0;
    float a = atan(screenUv.y, screenUv.x);

    vec2 tiled = screenUv + vec2(aspect * 0.5, 0.5);
    tiled *= tileScale;

    vec2 ipos = floor(tiled);
    vec2 st = fract(tiled);

    vec2 localCenterSt = st - 0.5;
    float localR = length(localCenterSt) * 2.0;
    float localA = atan(localCenterSt.y, localCenterSt.x);

    float rotating = step(PI / 4.0, abs(cos(localA + uTime * rotationSpeed + r * radialRotationAmount)));
    float randX = random(ipos + rotating) + rotating;

    vec3 color;

    if (randX > 0.75) {
        color = colorA;
    } else if (randX > 0.5) {
        color = colorB;
    } else if (randX > 0.25) {
        color = colorC;
    } else {
        color = colorD;
    }

    color.r = 1.0 - abs(sin(r * radialSpeed + uTime * radialSpeed));

    if (randX > 0.5) {
        color *= vec3(barra(st, slashWidth));
    } else {
        color *= vec3(contrabarra(st, slashWidth));
    }

    vec4 source = texture(uTexture, uv);
    FragColor = vec4(color, source.a);
}