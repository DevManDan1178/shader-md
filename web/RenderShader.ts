export interface ShaderRenderArgs {
    imageBase64: string;
    fragmentSource: string;
    parameters: {
        time: number;
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
    // Compile shader helper
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
    // Program
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
    if (gl.getError() != 0) {
        console.log("GL error:", gl.getError());
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