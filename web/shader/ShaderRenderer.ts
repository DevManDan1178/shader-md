
import { 
    ShaderProperties, 
    RawFrameResult, 
    ShaderRenderArgs,
    ShaderRenderBatchArgs,
    parseShaderDefaults,
    identityVertexShader,
    setShaderUniform
} from ".";

class ShaderRenderer {
    private canvas: OffscreenCanvas;
    private readonly gl: WebGL2RenderingContext;
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

    /**
     * Serializes render calls on this renderer so concurrent frames
     * (different images, same shader) don't interleave texture upload,
     * draw, and readback against each other.
     */
    private queue: Promise<unknown> = Promise.resolve();

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
        this.texture = texture;
        this.defaultShaderProperties = defaultShaderProperties;

        this.textureLocation = gl.getUniformLocation(program, "uTexture");
        this.resolutionLocation = gl.getUniformLocation(program, "uResolution");
        this.timeLocation = gl.getUniformLocation(program, "uTime");
        this.cacheUniforms();
    }

    // Renderer is created from the shader only — no image involved yet.
    static async create(fragmentSource: string): Promise<ShaderRenderer> {
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

        const renderer = new ShaderRenderer(
            canvas, gl, program, positionBuffer, uvBuffer, texture, defaultShaderProperties
        );

        gl.useProgram(program);

        if (renderer.textureLocation !== null) {
            gl.uniform1i(renderer.textureLocation, 0);
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


    /**
     * Uploads a new source image, resizing the canvas/viewport/pixel buffer and resolution uniform if the image dimensions changed.
     * @param imageBase64 image
     */
    private async uploadImage(imageBase64: string): Promise<void> {
        const blob = await (await fetch("data:image/png;base64," + imageBase64)).blob();
        const bitmap = await createImageBitmap(blob);

        const gl = this.gl;

        if (this.canvas.width !== bitmap.width || this.canvas.height !== bitmap.height) {
            this.canvas.width = bitmap.width;
            this.canvas.height = bitmap.height;

            if (this.resolutionLocation !== null) {
                gl.useProgram(this.program);
                gl.uniform2f(this.resolutionLocation, bitmap.width, bitmap.height);
            }
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

        const output = new Uint8Array(this.canvas.width * this.canvas.height * 4);
        gl.readPixels(0, 0, this.canvas.width, this.canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, output);

        return output;
    }

    // Uploads the given image, then renders one frame. 
    // Queued so concurrent calls on this renderer run one at a time, since they share a single texture/canvas/pixel buffer.
    renderFrame(imageBase64: string, time: number, shaderProperties: ShaderProperties): Promise<RawFrameResult> {
        const task = this.queue.then(async () => {
            await this.uploadImage(imageBase64);
            const pixels = this.drawAndReadback(time, shaderProperties);
            return { width: this.canvas.width, height: this.canvas.height, pixels };
        });

        // Keep the queue alive even if this task fails.
        this.queue = task.catch(() => {});

        return task;
    }
}

const rendererCache = new Map<string, ShaderRenderer | Promise<ShaderRenderer>>();

async function getOrCreateRenderer(shaderPath: string, fragmentSource: string): Promise<ShaderRenderer> {
    const cached = rendererCache.get(shaderPath);
    if (cached) {
        return cached;
    }

    const creationPromise = ShaderRenderer.create(fragmentSource);
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

export async function renderShaderRaw(args: ShaderRenderArgs): Promise<RawFrameResult> {
    const { shaderPath, imageBase64, fragmentSource, parameters } = args;

    const renderer = await getOrCreateRenderer(shaderPath, fragmentSource);

    return renderer.renderFrame(imageBase64, parameters.time, parameters.shaderProperties);
}

/**
 * Renders many images through the same shader concurrently in one call.
 * Each frame's upload+draw+readback is serialized internally per renderer,
 * but frames for different shaderPaths run fully in parallel.
 * @param args ShaderRenderBatchArgs 
 * @returns a promise for the result
 */
export async function renderShaderBatchRaw(args: ShaderRenderBatchArgs): Promise<RawFrameResult[]> {
    const { shaderPath, fragmentSource, frames } = args;

    const renderer = await getOrCreateRenderer(shaderPath, fragmentSource);

    return Promise.all(
        frames.map(frame => renderer.renderFrame(frame.imageBase64, frame.time, frame.shaderProperties))
    );
}
