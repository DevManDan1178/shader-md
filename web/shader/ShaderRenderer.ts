import { 
    ShaderProperties, 
    RawFrameResult, 
    ShaderRenderArgs,
    ShaderRenderBatchArgs,
    parseShaderDefaults,
    setShaderUniform,
    timeUniform,
    resolutionUniform,
    textureUniform,
    ImageSliceInfo,
    getVertexShaderSource,
    areImageSliceInfoEqual,
    ShaderChunkRenderArgs,
} from ".";

function compileVertexShader(gl: WebGL2RenderingContext, source: string): WebGLShader {
    if (!source) {
        throw new Error("Vertex shader source is undefined.");
    }

    const shader = gl.createShader(gl.VERTEX_SHADER);
    if (!shader) {
        throw new Error("Failed to create Vertex shader.");
    }

    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(`Vertex shader compilation failed:\n${log}\n\nSource:\n${source}`);
    }

    return shader;
}

function compileFragmentShader(gl: WebGL2RenderingContext, source: string): WebGLShader {
    if (!source) {
        throw new Error("Fragment shader source is undefined.");
    }

    const shader = gl.createShader(gl.FRAGMENT_SHADER);
    if (!shader) {
        throw new Error("Failed to create Fragment shader.");
    }

    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(`Fragment shader compilation failed:\n${log}\n\nSource:\n${source}`);
    }

    return shader;
}

/**
 * A shader renderer specialized for a single, fixed shader program.
 * The fragment shader is compiled once at creation and never changes.
 * The vertex shader also starts fixed (identityVertexShader) but can be
 * swapped later via setVertexShader() without recreating the program,
 * texture, buffers, or GL context.
 */
class ShaderRenderer {
    private canvas: OffscreenCanvas;
    private readonly gl: WebGL2RenderingContext;
    private readonly program: WebGLProgram;

    private readonly texture: WebGLTexture;
    private readonly positionBuffer: WebGLBuffer;
    private readonly uvBuffer: WebGLBuffer;

    private textureLocation: WebGLUniformLocation | null;
    private resolutionLocation: WebGLUniformLocation | null;
    private timeLocation: WebGLUniformLocation | null;

    private vertexImageSliceInfo: ImageSliceInfo | null;
    private vertexShader: WebGLShader;
    private readonly fragmentShader: WebGLShader;

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
        texture: WebGLTexture,
        positionBuffer: WebGLBuffer,
        uvBuffer: WebGLBuffer,
        vertexImageSliceInfo: ImageSliceInfo | null,
        vertexShader: WebGLShader,
        fragmentShader: WebGLShader,
        defaultShaderProperties: ShaderProperties
    ) {
        this.canvas = canvas;
        this.gl = gl;
        this.program = program;
        this.texture = texture;
        this.positionBuffer = positionBuffer;
        this.uvBuffer = uvBuffer;
        this.vertexShader = vertexShader;
        this.fragmentShader = fragmentShader;
        this.defaultShaderProperties = defaultShaderProperties;
        this.vertexImageSliceInfo = vertexImageSliceInfo;
        this.textureLocation = gl.getUniformLocation(program, textureUniform);
        this.resolutionLocation = gl.getUniformLocation(program, resolutionUniform);
        this.timeLocation = gl.getUniformLocation(program, timeUniform);
        this.cacheUniforms();
    }

    // Renderer is created from the fragment shader (+ optional vertex shader override).
    // No image is involved yet.
    static async create(fragmentSource: string, imageSliceInfo : ImageSliceInfo | null = null): Promise<ShaderRenderer> {
        const canvas = new OffscreenCanvas(1, 1);

        const gl = canvas.getContext("webgl2", {
            premultipliedAlpha: false,
            preserveDrawingBuffer: false
        }) as WebGL2RenderingContext | null;

        if (!gl) {
            throw new Error("WebGL 2 is not available.");
        }

        const defaultShaderProperties = parseShaderDefaults(fragmentSource);
        const vertexSource = getVertexShaderSource(imageSliceInfo)
        const vertexShader = compileVertexShader(gl, vertexSource);
        const fragmentShader = compileFragmentShader(gl, fragmentSource);

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
        /* 
            Shaders stay attached to the program, so deleteShader here only *flags* them for deletion 
            they remain valid handles for detachShader in setVertexShader() until actually detached.
        */
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
            canvas, gl, program, texture, positionBuffer, uvBuffer,
            imageSliceInfo, vertexShader, fragmentShader, defaultShaderProperties
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
     * Swaps the vertex shader on this renderer's existing program and
     * relinks, without touching the GL context, texture, canvas, or
     * vertex buffers. Compiles the new shader BEFORE tearing down the
     * old one, so a bad source leaves the renderer in its prior working
     * state rather than half torn-down.
     *
     * Re-fetches every attribute and uniform location afterward, since
     * relinking invalidates all of them (not just the ones affected by
     * the vertex shader).
     */
    setVertexShader(newVertexSource: string): void {
        const gl = this.gl;

        const newVertexShader = compileVertexShader(gl, newVertexSource);

        gl.detachShader(this.program, this.vertexShader);
        gl.deleteShader(this.vertexShader);
        gl.attachShader(this.program, newVertexShader);
        gl.linkProgram(this.program);

        if (!gl.getProgramParameter(this.program, gl.LINK_STATUS)) {
            const log = gl.getProgramInfoLog(this.program);
            /* 
                Relink failed - the program is now in a broken link state.
                Put the old vertex shader back and relink to restore a working program.
            */
            gl.detachShader(this.program, newVertexShader);
            gl.deleteShader(newVertexShader);
            gl.attachShader(this.program, this.vertexShader);
            gl.linkProgram(this.program);
            throw new Error(`Vertex shader relink failed, reverted to previous vertex shader:\n${log}`);
        }

        this.vertexShader = newVertexShader;
        gl.useProgram(this.program);

        const positionLocation = gl.getAttribLocation(this.program, "aPosition");
        if (positionLocation < 0) {
            throw new Error('New vertex shader does not contain "aPosition".');
        }
        gl.bindBuffer(gl.ARRAY_BUFFER, this.positionBuffer);
        gl.enableVertexAttribArray(positionLocation);
        gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);

        const uvLocation = gl.getAttribLocation(this.program, "aUv");
        if (uvLocation < 0) {
            throw new Error('New vertex shader does not contain "aUv".');
        }
        gl.bindBuffer(gl.ARRAY_BUFFER, this.uvBuffer);
        gl.enableVertexAttribArray(uvLocation);
        gl.vertexAttribPointer(uvLocation, 2, gl.FLOAT, false, 0, 0);

        this.textureLocation = gl.getUniformLocation(this.program, textureUniform);
        this.resolutionLocation = gl.getUniformLocation(this.program, resolutionUniform);
        this.timeLocation = gl.getUniformLocation(this.program, timeUniform);
        this.shaderUniforms.clear();
        this.cacheUniforms();

        if (this.textureLocation !== null) {
            gl.uniform1i(this.textureLocation, 0);
        }

        /* 
            Resolution uniform location is new - re-push the current size if the canvas has already been sized by a prior uploadImage() call.
        */
        if (this.resolutionLocation !== null && this.canvas.width > 0 && this.canvas.height > 0) {
            gl.uniform2f(this.resolutionLocation, this.canvas.width, this.canvas.height);
        }
    }

    /**
     * Uploads a new source image, resizing the canvas/viewport/pixel buffer and resolution uniform if the image dimensions changed.
     * @param imageBase64 image
     */
    private async uploadImage(imageBase64: string, imageSliceInfo : ImageSliceInfo | null = null): Promise<void> {
        if (!areImageSliceInfoEqual(this.vertexImageSliceInfo, imageSliceInfo)) {
            this.setVertexShader(getVertexShaderSource(imageSliceInfo));
        } 
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
            if (property == timeUniform || property == textureUniform || property == resolutionUniform) {
                continue;
            }
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

    renderFrameChunk(imageChunkBase64: string, time: number, shaderProperties: ShaderProperties, imageSliceInfo : ImageSliceInfo) {
        const task = this.queue.then(async() => {
            await this.uploadImage(imageChunkBase64, imageSliceInfo);
            const pixels = this.drawAndReadback(time, shaderProperties);
            return { width: this.canvas.width, height: this.canvas.height, pixels}
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

/**
 * Swaps the vertex shader for an already-created renderer, keyed by the
 * same shaderPath used to create/render it. Throws if no renderer has
 * been created yet for that path.
 */
export async function setShaderVertexShader(shaderPath: string, vertexSource: string): Promise<void> {
    const cached = rendererCache.get(shaderPath);
    if (!cached) {
        throw new Error(`No renderer cached for shader path "${shaderPath}". Create it first via renderShaderRaw/renderShaderBatchRaw.`);
    }

    const renderer = await cached;
    renderer.setVertexShader(vertexSource);
}


export async function renderShaderRaw(args: ShaderRenderArgs): Promise<RawFrameResult> {
    const { shaderPath, imageBase64, fragmentSource, parameters } = args;
    const renderer = await getOrCreateRenderer(shaderPath, fragmentSource);

    return renderer.renderFrame(imageBase64, parameters.time, parameters.shaderProperties);
}

export async function renderShaderChunkRaw(args : ShaderChunkRenderArgs) : Promise<RawFrameResult> {
    const { shaderPath, imageBase64, fragmentSource, parameters, imageSliceInfo } = args;
    const renderer = await getOrCreateRenderer(shaderPath, fragmentSource);

    return renderer.renderFrameChunk(imageBase64, parameters.time, parameters.shaderProperties, imageSliceInfo);
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