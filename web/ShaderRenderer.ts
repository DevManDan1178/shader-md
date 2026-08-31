type JSONValue =
    | string
    | number
    | boolean
    | null
    | JSONValue[]
    | { [key: string]: JSONValue };

export type ShaderProperties = Record<string, JSONValue>;

export interface ShaderFrameParams {
    time: number;
    shaderProperties: ShaderProperties;
}

export interface ShaderRenderArgs {
    shaderPath: string;
    imageBase64: string;
    fragmentSource: string;
    parameters: ShaderFrameParams;
}

export interface ShaderRenderBatchArgs {
    shaderPath: string;
    imageBase64: string;
    fragmentSource: string;
    frames: ShaderFrameParams[];
}

const identityVertexShader: string = `#version 300 es

in vec2 aPosition;
in vec2 aUv;

out vec2 vUv;

void main() {
    vUv = aUv;
    gl_Position = vec4(aPosition, 0.0, 1.0);
}
`;

class ShaderRenderer {
    private readonly canvas: OffscreenCanvas;
    private readonly gl: WebGL2RenderingContext;
    private readonly program: WebGLProgram;

    private readonly positionBuffer: WebGLBuffer;
    private readonly uvBuffer: WebGLBuffer;
    private readonly texture: WebGLTexture;

    private readonly textureLocation: WebGLUniformLocation | null;
    private readonly resolutionLocation: WebGLUniformLocation | null;
    private readonly timeLocation: WebGLUniformLocation | null;

    private readonly shaderUniforms = new Map<string, {
        info: WebGLActiveInfo;
        location: WebGLUniformLocation;
    }>();

    private readonly defaultShaderProperties: ShaderProperties;

    // Pixel readback buffer, sized once per renderer and reused across frames.
    private readonly pixelBuffer: Uint8Array;

    private constructor(
        canvas: OffscreenCanvas,
        gl: WebGL2RenderingContext,
        program: WebGLProgram,
        positionBuffer: WebGLBuffer,
        uvBuffer: WebGLBuffer,
        texture: WebGLTexture,
        defaultShaderProperties: ShaderProperties
    ) {
        this.canvas = canvas;
        this.gl = gl;
        this.program = program;
        this.positionBuffer = positionBuffer;
        this.uvBuffer = uvBuffer;
        this.texture = texture;
        this.defaultShaderProperties = defaultShaderProperties;

        this.textureLocation = gl.getUniformLocation(program, "uTexture");
        this.resolutionLocation = gl.getUniformLocation(program, "uResolution");
        this.timeLocation = gl.getUniformLocation(program, "uTime");

        this.pixelBuffer = new Uint8Array(canvas.width * canvas.height * 4);

        this.cacheUniforms();
    }

    static async create(imageBase64: string, fragmentSource: string): Promise<ShaderRenderer> {
        const blob = await (await fetch("data:image/png;base64," + imageBase64)).blob();
        const bitmap = await createImageBitmap(blob);

        const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);

        const gl = canvas.getContext("webgl2", {
            premultipliedAlpha: false,
            preserveDrawingBuffer: false
        }) as WebGL2RenderingContext | null;

        if (!gl) {
            throw new Error("WebGL 2 is not available.");
        }

        const defaultShaderProperties = parseShaderDefaults(fragmentSource);

        function compileShader(type: number, source: string, name: string): WebGLShader {
            if (!source) {
                throw new Error(`${name} shader source is undefined.`);
            }
            if (!gl) {
                throw new Error("No GL initialized.");
            }

            const shader = gl.createShader(type);
            if (!shader) {
                throw new Error(`Failed to create ${name} shader.`);
            }

            gl.shaderSource(shader, source);
            gl.compileShader(shader);

            if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
                const log = gl.getShaderInfoLog(shader);
                throw new Error(`${name} shader compilation failed:\n${log}\n\nSource:\n${source}`);
            }

            return shader;
        }

        const vertexShader = compileShader(gl.VERTEX_SHADER, identityVertexShader, "Vertex");
        const fragmentShader = compileShader(gl.FRAGMENT_SHADER, fragmentSource, "Fragment");

        const program = gl.createProgram();
        if (!program) {
            throw new Error("Failed to create shader program.");
        }

        gl.attachShader(program, vertexShader);
        gl.attachShader(program, fragmentShader);
        gl.linkProgram(program);

        if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
            throw new Error("Shader program linking failed:\n" + gl.getProgramInfoLog(program));
        }

        gl.useProgram(program);
        gl.deleteShader(vertexShader);
        gl.deleteShader(fragmentShader);

        const vertices = new Float32Array([
            -1, -1, 1, -1, 1, 1,
            -1, -1, 1, 1, -1, 1
        ]);

        const uvs = new Float32Array([
            0, 0, 1, 0, 1, 1,
            0, 0, 1, 1, 0, 1
        ]);

        const positionBuffer = gl.createBuffer();
        if (!positionBuffer) {
            throw new Error("Failed to create position buffer.");
        }

        gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

        const positionLocation = gl.getAttribLocation(program, "aPosition");
        if (positionLocation < 0) {
            throw new Error('Shader does not contain "aPosition".');
        }

        gl.enableVertexAttribArray(positionLocation);
        gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);

        const uvBuffer = gl.createBuffer();
        if (!uvBuffer) {
            throw new Error("Failed to create UV buffer.");
        }

        gl.bindBuffer(gl.ARRAY_BUFFER, uvBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, uvs, gl.STATIC_DRAW);

        const uvLocation = gl.getAttribLocation(program, "aUv");
        if (uvLocation < 0) {
            throw new Error('Shader does not contain "aUv".');
        }

        gl.enableVertexAttribArray(uvLocation);
        gl.vertexAttribPointer(uvLocation, 2, gl.FLOAT, false, 0, 0);

        const texture = gl.createTexture();
        if (!texture) {
            throw new Error("Failed to create texture.");
        }

        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, bitmap);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

        bitmap.close();

        const renderer = new ShaderRenderer(
            canvas, gl, program, positionBuffer, uvBuffer, texture, defaultShaderProperties
        );

        gl.useProgram(program);

        if (renderer.textureLocation !== null) {
            gl.uniform1i(renderer.textureLocation, 0);
        }

        if (renderer.resolutionLocation !== null) {
            gl.uniform2f(renderer.resolutionLocation, canvas.width, canvas.height);
        }

        return renderer;
    }

    private cacheUniforms(): void {
        const gl = this.gl;
        const uniformCount = gl.getProgramParameter(this.program, gl.ACTIVE_UNIFORMS);

        for (let i = 0; i < uniformCount; i++) {
            const info = gl.getActiveUniform(this.program, i);
            if (!info) {
                continue;
            }

            const location = gl.getUniformLocation(this.program, info.name);
            if (location === null) {
                continue;
            }

            this.shaderUniforms.set(info.name, { info, location });
        }
    }

    // Renders one frame and returns raw RGBA pixels (no PNG encoding, no base64).
    renderRaw(time: number, shaderProperties: ShaderProperties): Uint8Array {
        const gl = this.gl;

        gl.useProgram(this.program);

        if (this.timeLocation !== null) {
            gl.uniform1f(this.timeLocation, time);
        }

        const properties: ShaderProperties = {
            ...this.defaultShaderProperties,
            ...shaderProperties
        };

        for (const [property, value] of Object.entries(properties)) {
            try {
                const uniform = this.shaderUniforms.get(property);
                if (!uniform) {
                    console.warn(`Shader property "${property}" does not exist in shader.`);
                    continue;
                }

                setShaderUniform(gl, uniform.location, uniform.info.type, value);
            } catch (e) {
                console.log(`Error applying property ${property} with value ${value} to shader.`, e);
            }
        }

        gl.viewport(0, 0, this.canvas.width, this.canvas.height);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.drawArrays(gl.TRIANGLES, 0, 6);

        gl.readPixels(0, 0, this.canvas.width, this.canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, this.pixelBuffer);

        return this.pixelBuffer;
    }

    get width(): number {
        return this.canvas.width;
    }

    get height(): number {
        return this.canvas.height;
    }
}

const rendererCache = new Map<string, ShaderRenderer | Promise<ShaderRenderer>>();

async function getOrCreateRenderer(shaderPath: string, imageBase64: string, fragmentSource: string): Promise<ShaderRenderer> {
    const cached = rendererCache.get(shaderPath);
    if (cached) {
        return cached;
    }

    const creationPromise = ShaderRenderer.create(imageBase64, fragmentSource);
    rendererCache.set(shaderPath, creationPromise);

    try {
        const renderer = await creationPromise;
        rendererCache.set(shaderPath, renderer);
        return renderer;
    } catch (e) {
        rendererCache.delete(shaderPath);
        throw e;
    }
}

export function evictShader(shaderPath: string): void {
    rendererCache.delete(shaderPath);
}

export function clearShaderCache(): void {
    rendererCache.clear();
}

// Raw-pixel result: flat RGBA bytes plus dimensions, so the caller can
// reconstruct/encode an image however it wants without another format
// conversion happening on this side.
export interface RawFrameResult {
    width: number;
    height: number;
    pixels: Uint8Array;
}

export async function renderShaderRaw(args: ShaderRenderArgs): Promise<RawFrameResult> {
    const { shaderPath, imageBase64, fragmentSource, parameters } = args;

    const renderer = await getOrCreateRenderer(shaderPath, imageBase64, fragmentSource);
    const pixels = renderer.renderRaw(parameters.time, parameters.shaderProperties);

    return { width: renderer.width, height: renderer.height, pixels };
}

// Batched variant: one EvaluateAsync round trip renders many frames of the
// same shader, amortizing the fixed cross-process call overhead.
export async function renderShaderBatchRaw(args: ShaderRenderBatchArgs): Promise<RawFrameResult[]> {
    const { shaderPath, imageBase64, fragmentSource, frames } = args;

    const renderer = await getOrCreateRenderer(shaderPath, imageBase64, fragmentSource);

    const results: RawFrameResult[] = new Array(frames.length);

    for (let i = 0; i < frames.length; i++) {
        const frame = frames[i];
        // Copy out of the shared pixel buffer since renderRaw reuses it.
        const pixels = renderer.renderRaw(frame.time, frame.shaderProperties).slice();
        results[i] = { width: renderer.width, height: renderer.height, pixels };
    }

    return results;
}

function parseShaderDefaults(source: string): ShaderProperties {
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

function setShaderUniform(gl: WebGL2RenderingContext, location: WebGLUniformLocation, type: number, value: JSONValue): void {
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