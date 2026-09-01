import { 
    ShaderProperties, 
    RawFrameResult, 
    JSONValue,
    parseShaderDefaults,
    identityVertexShader,
    setShaderUniform,
    ShaderRenderBatchArgs
} from ".";

export type StaticShaderRenderBatchArgs = ShaderRenderBatchArgs & {
    imageBase64 : string;
};

/**
 * A shader renderer specialized for a single static source image.
 * The image is uploaded exactly once at creation; every subsequent
 * render call only updates time/uniforms and redraws - no re-upload,
 * no image comparison, no per-frame decode cost.
 */
class StaticShaderRenderer {
    private readonly gl: WebGL2RenderingContext;
    private readonly canvas: OffscreenCanvas;
    private readonly program: WebGLProgram;

    private readonly texture: WebGLTexture;

    private readonly textureLocation: WebGLUniformLocation | null;
    private readonly resolutionLocation: WebGLUniformLocation | null;
    private readonly timeLocation: WebGLUniformLocation | null;

    private readonly shaderUniforms = new Map<string, {
        info: WebGLActiveInfo;
        location: WebGLUniformLocation;
    }>();

    private readonly defaultShaderProperties: ShaderProperties;
    private lastAppliedProperties: ShaderProperties = {};

    /** Serializes draw+readback calls so concurrent renderFrame calls don't interleave. */
    private queue: Promise<unknown> = Promise.resolve();

    private constructor(
        canvas: OffscreenCanvas,
        gl: WebGL2RenderingContext,
        program: WebGLProgram,
        texture: WebGLTexture,
        defaultShaderProperties: ShaderProperties
    ) {
        this.canvas = canvas;
        this.gl = gl;
        this.program = program;
        this.texture = texture;
        this.defaultShaderProperties = defaultShaderProperties;

        this.textureLocation = gl.getUniformLocation(program, "uTexture");
        this.resolutionLocation = gl.getUniformLocation(program, "uResolution");
        this.timeLocation = gl.getUniformLocation(program, "uTime");

        this.cacheUniforms();
    }

    /**
     * Creates a renderer bound to one specific image. The image is decoded
     * and uploaded to the GPU exactly once, here, and never again.
     */
    static async create(fragmentSource: string, imageBase64: string): Promise<StaticShaderRenderer> {
        const canvas = new OffscreenCanvas(1, 1);

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
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

        const renderer = new StaticShaderRenderer(
            canvas, gl, program, texture, defaultShaderProperties
        );

        gl.useProgram(program);

        if (renderer.textureLocation !== null) {
            gl.uniform1i(renderer.textureLocation, 0);
        }

        // Upload the one and only image, once, up front.
        await renderer.uploadImageOnce(imageBase64);

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

    private async uploadImageOnce(imageBase64: string): Promise<void> {
        const binary = atob(imageBase64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        const blob = new Blob([bytes], { type: "image/png" });
        const bitmap = await createImageBitmap(blob);

        const gl = this.gl;

        this.canvas.width = bitmap.width;
        this.canvas.height = bitmap.height;

        if (this.resolutionLocation !== null) {
            gl.useProgram(this.program);
            gl.uniform2f(this.resolutionLocation, bitmap.width, bitmap.height);
        }

        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, this.texture);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, bitmap);

        bitmap.close();
    }

    private drawAndReadback(time: number, shaderProperties: ShaderProperties): Uint8Array {
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
            if (propertyEquals(this.lastAppliedProperties[property], value)) continue;

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
        this.lastAppliedProperties = properties;

        gl.viewport(0, 0, this.canvas.width, this.canvas.height);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.drawArrays(gl.TRIANGLES, 0, 6);

        const output = new Uint8Array(this.canvas.width * this.canvas.height * 4);
        gl.readPixels(0, 0, this.canvas.width, this.canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, output);

        return output;
    }

    /**
     * Renders one frame of the bound image at the given time/properties.
     * No image parameter - this renderer only ever has the one image
     * it was created with.
     */
    renderFrame(time: number, shaderProperties: ShaderProperties): Promise<RawFrameResult> {
        const task = this.queue.then(async () => {
            const pixels = this.drawAndReadback(time, shaderProperties);
            return { width: this.canvas.width, height: this.canvas.height, pixels };
        });

        this.queue = task.catch(() => {});

        return task;
    }
}

const staticRendererCache = new Map<string, StaticShaderRenderer | Promise<StaticShaderRenderer>>();

function staticCacheKey(shaderPath: string, imageBase64: string): string {
    // Cheap, collision-safe-enough key: path + length + a couple of sampled
    // slices. Avoids hashing/scanning the whole multi-MB string on lookup.
    const len = imageBase64.length;
    const sample = imageBase64.slice(0, 16) + imageBase64.slice(Math.max(0, len - 16));
    return `${shaderPath}:${len}:${sample}`;
}

async function getOrCreateStaticRenderer(
    shaderPath: string,
    fragmentSource: string,
    imageBase64: string
): Promise<StaticShaderRenderer> {
    const key = staticCacheKey(shaderPath, imageBase64);

    const cached = staticRendererCache.get(key);
    if (cached) {
        return cached;
    }

    const creationPromise = StaticShaderRenderer.create(fragmentSource, imageBase64);
    staticRendererCache.set(key, creationPromise);

    try {
        const renderer = await creationPromise;
        staticRendererCache.set(key, renderer);
        return renderer;
    } catch (e) {
        staticRendererCache.delete(key);
        throw e;
    }
}

export function evictStaticShader(shaderPath: string, imageBase64: string): void {
    staticRendererCache.delete(staticCacheKey(shaderPath, imageBase64));
}

export function clearStaticShaderCache(): void {
    staticRendererCache.clear();
}

/**
 * Renders many frames of the SAME source image through the same shader.
 * The image is decoded and uploaded to the GPU exactly once for the whole
 * batch, regardless of how many frames are requested.
 */
export async function renderStaticShaderBatchRaw(args: StaticShaderRenderBatchArgs): Promise<RawFrameResult[]> {
    const { shaderPath, fragmentSource, imageBase64, frames } = args;

    if (frames.length === 0) {
        return [];
    }

    const renderer = await getOrCreateStaticRenderer(shaderPath, fragmentSource, imageBase64);

    return Promise.all(
        frames.map(frame => renderer.renderFrame(frame.time, frame.shaderProperties))
    );
}

function propertyEquals(a: JSONValue | undefined, b: JSONValue | undefined): boolean {
    if (a === b) return true;
    if (Array.isArray(a) && Array.isArray(b)) {
        return a.length === b.length && a.every((v, i) => v === b[i]);
    }
    return false;
}