type JSONValue =
    | string
    | number
    | boolean
    | null
    | JSONValue[]
    | { [key: string]: JSONValue };

export type ShaderProperties = Record<string, JSONValue>;

export interface ShaderRenderArgs {
    imageBase64: string;
    fragmentSource: string;
    parameters: {
        time: number;
        shaderProperties : ShaderProperties;
    };
}


// ----------------------------------------
// Vertex shader - No special effects
// ----------------------------------------

const identityVertexShader : string = (
    `#version 300 es

        in vec2 aPosition;
        in vec2 aUv;

        out vec2 vUv;

        void main()
        {
            vUv = aUv;

            gl_Position = vec4(
                aPosition,
                0.0,
                1.0
            );
        }
    `
);



export async function renderShader(args: ShaderRenderArgs): Promise<number[]> {
    const { imageBase64, fragmentSource, parameters } = args;
    const image = new Image();

    image.src = "data:image/png;base64," + imageBase64;

    await new Promise<void>((resolve, reject) => {
        image.onload = () => resolve();
        image.onerror = () => reject(new Error("Failed to load input image."));
    });

    const canvas = document.createElement("canvas");

    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;

    const gl = canvas.getContext("webgl2", {
        premultipliedAlpha: false,
        preserveDrawingBuffer: true
    });

    if (!gl) {
        throw new Error("WebGL 2 is not available.");
    }

    // ----------------------------------------
    // Compile shader helper function
    // ----------------------------------------

    function compileShader(type: number, source: string, name: string): WebGLShader {
        if (!source) {
            throw new Error(`${name} shader source is undefined.`);
        }

        const shader = gl!.createShader(type);

        if (!shader) {
            throw new Error(`Failed to create ${name} shader.`);
        }

        gl!.shaderSource(shader, source);
        gl!.compileShader(shader);

        const success = gl!.getShaderParameter(shader, gl!.COMPILE_STATUS);

        if (!success) {
            const log = gl!.getShaderInfoLog(shader);

            throw new Error(
                `${name} shader compilation failed:\n` +
                `${log}\n\n` +
                `Source:\n${source}`
            );
        }

        return shader;
    }

    const vertexShader = compileShader(gl.VERTEX_SHADER, identityVertexShader, "Vertex");
    const fragmentShader = compileShader(gl.FRAGMENT_SHADER, fragmentSource, "Fragment");

    // ----------------------------------------
    // Shader program
    // ----------------------------------------

    const program = gl.createProgram();

    if (!program) {
        throw new Error("Failed to create shader program.");
    }

    gl.attachShader(program, vertexShader);
    gl.attachShader(program, fragmentShader);
    gl.linkProgram(program);

    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        throw new Error(
            "Shader program linking failed:\n" +
            gl.getProgramInfoLog(program)
        );
    }

    gl.useProgram(program);

    // ----------------------------------------
    // Fullscreen quad
    // ----------------------------------------

    const vertices = new Float32Array([
        -1, -1,
         1, -1,
         1,  1,
        -1, -1,
         1,  1,
        -1,  1
    ]);

    const uvs = new Float32Array([
        0, 0,
        1, 0,
        1, 1,
        0, 0,
        1, 1,
        0, 1
    ]);

    const positionBuffer = gl.createBuffer();

    if (!positionBuffer) {
        throw new Error("Failed to create position buffer.");
    }

    gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

    const positionLocation = gl.getAttribLocation(program, "aPosition");

    gl.enableVertexAttribArray(positionLocation);
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);

    const uvBuffer = gl.createBuffer();

    if (!uvBuffer) {
        throw new Error("Failed to create UV buffer.");
    }

    gl.bindBuffer(gl.ARRAY_BUFFER, uvBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, uvs, gl.STATIC_DRAW);

    const uvLocation = gl.getAttribLocation(program, "aUv");

    gl.enableVertexAttribArray(uvLocation);
    gl.vertexAttribPointer(uvLocation, 2, gl.FLOAT, false, 0, 0);

    // ----------------------------------------
    // Input texture
    // ----------------------------------------

    const texture = gl.createTexture();

    if (!texture) {
        throw new Error("Failed to create texture.");
    }

    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);

    gl.texImage2D(
        gl.TEXTURE_2D,
        0,
        gl.RGBA,
        gl.RGBA,
        gl.UNSIGNED_BYTE,
        image
    );

    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, texture);

    // ----------------------------------------
    // Texture uniform
    // ----------------------------------------

    const textureLocation = gl.getUniformLocation(program, "uTexture");

    if (textureLocation) {
        gl.uniform1i(textureLocation, 0);
    }

    // ----------------------------------------
    // Resolution uniform
    // ----------------------------------------

    const resolutionLocation = gl.getUniformLocation(program, "uResolution");

    if (resolutionLocation) {
        gl.uniform2f(resolutionLocation, canvas.width, canvas.height);
    }

    // ----------------------------------------
    // Time uniform
    // ----------------------------------------

    const timeLocation = gl.getUniformLocation(program, "uTime");
    
    if (timeLocation !== null) {
        gl.uniform1f(timeLocation, parameters.time);
    }
    if (gl.getError() != gl.NO_ERROR) {
        console.log("GL error:", gl.getError());
    }

    // ----------------------------------------
    // Custom shader property uniforms
    // ----------------------------------------
    
    const shaderProperties = {...parseShaderDefaults(fragmentSource), ...parameters.shaderProperties};
    
    if (shaderProperties) {
        const uniformCount = gl.getProgramParameter(
            program,
            gl.ACTIVE_UNIFORMS
        );

        const uniforms = new Map<string, WebGLActiveInfo>();

        for (let i = 0; i < uniformCount; i++) {
            const info = gl.getActiveUniform(program, i);

            if (info !== null) {
                uniforms.set(info.name, info);
            }
        }

        for (const [property, value] of Object.entries(shaderProperties)) {
         
            try {
                const info = uniforms.get(property);

                if (!info) {
                    console.warn(`Shader property "${property}" does not exist in shader.`);
                    continue;
                }

                const propertyLocation = gl.getUniformLocation(program, property);

                if (propertyLocation === null) {
                    console.warn(`Could not get location for shader property "${property}".`);
                    continue;
                }
                setShaderUniform(
                    gl,
                    propertyLocation,
                    info.type,
                    value
                );
                
                if (gl.getError() != gl.NO_ERROR) {
                    console.log("GL error:", gl.getError());
                }
            } catch (e) {
                console.log(`Error applying property ${property} with value ${value} to shader.`);
            }
        }
    }

    // ----------------------------------------
    // Render
    // ----------------------------------------

    gl.viewport(0, 0, canvas.width, canvas.height);
    gl.clearColor(0, 0, 0, 0);
    gl.clear(gl.COLOR_BUFFER_BIT);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
    gl.finish();

    // ----------------------------------------
    // Return PNG
    // ----------------------------------------

    const dataUrl = canvas.toDataURL("image/png");
    const base64 = dataUrl.split(",")[1];
    const binary = atob(base64);
    const bytes = new Array<number>(binary.length);

    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }

    return bytes;
}

/**
 * Parses default shader properties declared in the shader source.
 *
 * Declarations can appear inside a line or block comment.
 * Expected format:
 *   @default [PropertyName] [PropertyValue]
 *  
 * [PropertyName] [PropertyValue] must be entirely one line.
 * @param source The source code of the shader.
 * @returns The parsed default properties as ShaderProperties.
 */
function parseShaderDefaults(source: string): ShaderProperties {
    const defaults: ShaderProperties = {};

    const regex = /@default\s+(\w+)\s+([^\r\n]+)/g;

    for (const match of source.matchAll(regex)) {
        const property = match[1];
        const value = match[2].trim();

        try {
            defaults[property] = JSON.parse(value) as JSONValue;
        } catch (e) {
            throw new Error(
                `Invalid default value for shader property "${property}": ${value}`
            );
        }
    }

    return defaults;
}

function setShaderUniform(
    gl: WebGL2RenderingContext,
    location: WebGLUniformLocation,
    type: number,
    value: JSONValue
): void {
    switch (type) {
        // ----------------------------------------
        // Floating point
        // ----------------------------------------

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

        // ----------------------------------------
        // Integers
        // ----------------------------------------

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

        // ----------------------------------------
        // Unsigned integers
        // ----------------------------------------

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

        // ----------------------------------------
        // Booleans
        // ----------------------------------------

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

            gl.uniform2iv(
                location,
                value.map(v => v ? 1 : 0)
            );
            break;

        case gl.BOOL_VEC3:
            if (!isBooleanArray(value, 3)) {
                throw new Error("Expected boolean[3] for bvec3");
            }

            gl.uniform3iv(
                location,
                value.map(v => v ? 1 : 0)
            );
            break;

        case gl.BOOL_VEC4:
            if (!isBooleanArray(value, 4)) {
                throw new Error("Expected boolean[4] for bvec4");
            }

            gl.uniform4iv(
                location,
                value.map(v => v ? 1 : 0)
            );
            break;

        // ----------------------------------------
        // Matrices
        // ----------------------------------------

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

        // ----------------------------------------
        // Samplers
        // ----------------------------------------

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


function isNumberArray(
    value: JSONValue,
    length: number
): value is number[] {
    return (
        Array.isArray(value) &&
        value.length === length &&
        value.every(v => typeof v === "number")
    );
}

function isBooleanArray(
    value: JSONValue,
    length: number
): value is boolean[] {
    return (
        Array.isArray(value) &&
        value.length === length &&
        value.every(v => typeof v === "boolean")
    );
}
