export type JSONValue =
    | string
    | number
    | boolean
    | null
    | JSONValue[]
    | { [key: string]: JSONValue };

export type ShaderProperties = Record<string, JSONValue>;

export interface ShaderFrameParams {
    imageBase64: string;
    time: number;
    shaderProperties: ShaderProperties;
}

export interface ShaderRenderArgs {
    shaderPath: string;
    imageBase64: string;
    fragmentSource: string;
    parameters: {
        time: number;
        shaderProperties: ShaderProperties;
    };
}

export interface ShaderRenderBatchArgs {
    shaderPath: string;
    fragmentSource: string;
    frames: ShaderFrameParams[];
}

export interface RawFrameResult {
    width: number;
    height: number;
    pixels: Uint8Array;
}

export const identityVertexShader: string = `#version 300 es

in vec2 aPosition;
in vec2 aUv;

out vec2 vUv;

void main() {
    vUv = aUv;
    gl_Position = vec4(aPosition, 0.0, 1.0);
}
`;

export function parseShaderDefaults(source: string): ShaderProperties {
    const defaults: ShaderProperties = {};
    const regex = /@default\s+(\w+)\s+([^\r\n]+)/g;

    for (const match of source.matchAll(regex)) {
        const property = match[1];
        const value = match[2].trim();

        try {
            defaults[property] = JSON.parse(value) as JSONValue;
        } catch (e) {
            throw new Error(`Invalid default value for shader property "${property}": ${value}`);
        }
    }

    return defaults;
}

export function setShaderUniform(gl: WebGL2RenderingContext, location: WebGLUniformLocation, type: number, value: JSONValue): void {
    switch (type) {
        case gl.FLOAT:
            if (typeof value !== "number") {
                throw new Error(`Expected number, got ${typeof value}`);
            }
            gl.uniform1f(location, value);
            break;

        case gl.FLOAT_VEC2:
            if (!isNumberArray(value, 2)) {
                throw new Error("Expected number[2] for vec2");
            }
            gl.uniform2fv(location, value);
            break;

        case gl.FLOAT_VEC3:
            if (!isNumberArray(value, 3)) {
                throw new Error("Expected number[3] for vec3");
            }
            gl.uniform3fv(location, value);
            break;

        case gl.FLOAT_VEC4:
            if (!isNumberArray(value, 4)) {
                throw new Error("Expected number[4] for vec4");
            }
            gl.uniform4fv(location, value);
            break;

        case gl.INT:
            if (typeof value !== "number") {
                throw new Error(`Expected number, got ${typeof value}`);
            }
            gl.uniform1i(location, value);
            break;

        case gl.INT_VEC2:
            if (!isNumberArray(value, 2)) {
                throw new Error("Expected number[2] for ivec2");
            }
            gl.uniform2iv(location, value);
            break;

        case gl.INT_VEC3:
            if (!isNumberArray(value, 3)) {
                throw new Error("Expected number[3] for ivec3");
            }
            gl.uniform3iv(location, value);
            break;

        case gl.INT_VEC4:
            if (!isNumberArray(value, 4)) {
                throw new Error("Expected number[4] for ivec4");
            }
            gl.uniform4iv(location, value);
            break;

        case gl.UNSIGNED_INT:
            if (typeof value !== "number") {
                throw new Error(`Expected number, got ${typeof value}`);
            }
            gl.uniform1ui(location, value);
            break;

        case gl.UNSIGNED_INT_VEC2:
            if (!isNumberArray(value, 2)) {
                throw new Error("Expected number[2] for uvec2");
            }
            gl.uniform2uiv(location, value);
            break;

        case gl.UNSIGNED_INT_VEC3:
            if (!isNumberArray(value, 3)) {
                throw new Error("Expected number[3] for uvec3");
            }
            gl.uniform3uiv(location, value);
            break;

        case gl.UNSIGNED_INT_VEC4:
            if (!isNumberArray(value, 4)) {
                throw new Error("Expected number[4] for uvec4");
            }
            gl.uniform4uiv(location, value);
            break;

        case gl.BOOL:
            if (typeof value !== "boolean") {
                throw new Error(`Expected boolean, got ${typeof value}`);
            }
            gl.uniform1i(location, value ? 1 : 0);
            break;

        case gl.BOOL_VEC2:
            if (!isBooleanArray(value, 2)) {
                throw new Error("Expected boolean[2] for bvec2");
            }
            gl.uniform2iv(location, value.map(v => v ? 1 : 0));
            break;

        case gl.BOOL_VEC3:
            if (!isBooleanArray(value, 3)) {
                throw new Error("Expected boolean[3] for bvec3");
            }
            gl.uniform3iv(location, value.map(v => v ? 1 : 0));
            break;

        case gl.BOOL_VEC4:
            if (!isBooleanArray(value, 4)) {
                throw new Error("Expected boolean[4] for bvec4");
            }
            gl.uniform4iv(location, value.map(v => v ? 1 : 0));
            break;

        case gl.FLOAT_MAT2:
            if (!isNumberArray(value, 4)) {
                throw new Error("Expected number[4] for mat2");
            }
            gl.uniformMatrix2fv(location, false, value);
            break;

        case gl.FLOAT_MAT3:
            if (!isNumberArray(value, 9)) {
                throw new Error("Expected number[9] for mat3");
            }
            gl.uniformMatrix3fv(location, false, value);
            break;

        case gl.FLOAT_MAT4:
            if (!isNumberArray(value, 16)) {
                throw new Error("Expected number[16] for mat4");
            }
            gl.uniformMatrix4fv(location, false, value);
            break;

        case gl.SAMPLER_2D:
        case gl.SAMPLER_CUBE:
        case gl.SAMPLER_2D_SHADOW:
        case gl.SAMPLER_CUBE_SHADOW:
        case gl.INT_SAMPLER_2D:
        case gl.INT_SAMPLER_CUBE:
        case gl.UNSIGNED_INT_SAMPLER_2D:
        case gl.UNSIGNED_INT_SAMPLER_CUBE:
            if (typeof value !== "number") {
                throw new Error(`Expected texture unit number`);
            }
            gl.uniform1i(location, value);
            break;

        default:
            throw new Error(`Unsupported uniform type: ${type}`);
    }
}

function isNumberArray(value: JSONValue, length: number): value is number[] {
    return Array.isArray(value) && value.length === length && value.every(v => typeof v === "number");
}

function isBooleanArray(value: JSONValue, length: number): value is boolean[] {
    return Array.isArray(value) && value.length === length && value.every(v => typeof v === "boolean");
}