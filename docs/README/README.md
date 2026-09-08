# Shader-md

Turn a Markdown file into a visually styled document by applying GPU shader effects to it. Headings, paragraphs, lists, code blocks, tables, images, and more can each get their own animated or stylized look.

---

## For Users

### What is this?

Normally, a Markdown file gets turned into plain HTML with basic styling: a heading is just bold, bigger text, and a code block is just a gray box. **Shader-md** does something different. It renders your Markdown document and then runs each part of it through a GLSL shader (the same kind of GPU effect used in games and visual art) to give it a distinct visual treatment.

### What can it do?

- Apply a different shader effect to **every type of Markdown element**, including:
  - Headings (`#` through `######`)
  - Paragraph text
  - Blockquotes (`>`)
  - **Bold**, *italic*, ***bold italic***, ~~strikethrough~~
  - `Inline code` and fenced code blocks
  - Links and images
  - Unordered and ordered lists, and list markers
  - Task lists (`- [ ]` / `- [x]`)
  - Horizontal rules
  - Tables, table headers, and table cells
- Give each of these elements **two** shaders if you want: one for its content, and a separate one for its background.
- Apply a **document wide background shader** and a **final shader pass** over the completed document, similar to a full screen post processing filter.
- Produce **animated output**, not just a static image, since shaders can react to time.

### How it works

1. You write a normal Markdown file.
2. You decide which shader effects should apply to which parts of the document.
    - A shader configuration file like `shaderConfig.yaml` can set default shaders for each markdown element
    - Individual elements can have customized shader behaviour based on html properties.
3. Shader-md renders the document in a browser.
4. Each piece of the rendered document is handed off to the GPU and shaderized.
5. Everything is composited back together, with an optional last shader pass over the whole thing.
6. You get back a styled, and potentially animated, version of your document.

### Default shader configuration

Shader-md ships with a `shaderConfig.yaml` file that allows the assignment of a default shader to every supported Markdown element, plus a default document background and a default "finalize" pass for the whole document. This allows for a more concise markdown document that will not require manually setting shaders for every element.

The shader configuration is organized into two main areas:

- **`document_shaders`**, which sets the background shader for the whole page and the finalize shader that runs once over the completed document.
- **`default_page_element_shaders`**, which lists every element type (`heading1` through `heading6`, `default` for paragraphs, `blockquote`, `bold`, `italic`, `bold_italic`, `strikethrough`, `inline_code`, `link`, `image`, `unordered_list`, `ordered_list`, `list_marker`, `task_list`, `task_checkbox`, `code_block`, `horizontal_rule`, `table`, `table_header`, and `table_cell`) and, for each one, a default `content` shader and `background` shader.

You are free to leave these defaults as they are and simply run Shader-md against your Markdown file, or edit the YAML to point any element at a different shader.


### Applying and changing shaders on elements

Because every element type has both a `content` shader and a `background` shader, you can mix and match freely:

- Set `shader_path` under any element's `content` or `background` entry to point it at a `.frag` file in the `shaders` folder.
- Adjust the `shader_parameters` block under a shader entry to change how that shader behaves (things like color, intensity, speed, and so on, depending on what the shader itself exposes as parameters).
- Leaving `shader_path` empty will treat the element normally without shaders.
- Leaving `shader_parameters` empty will cause the shaders to use its default parameters 

This means most customization work happens in `shaderConfig.yaml` rather than in code: you are choosing which shader plays which role, not writing new rendering logic.

### Setting customized shader behaviour for HTML elements

Shader-md's rendering step turns your Markdown into HTML before any shader is applied, where default shader elements are written to elements without any shader properties.

Default shaders from the shader configuration file (`shaderConfig.yaml`) can be overwritten with HTML properties:
- The shader applied to the element can be set with the `shader` property
    - For example, `shader="myCustomShader.frag"`
    - To avoid applying a shader to the element, the shader property can be set to `shader=""` (empty string)
- The shader applied to the element background can be set with the `shader-bg` property
    - For example, `shader-bg="myCustomShader.frag"`
    - To avoid applying a background shader to the element, the shader property can be set to `shader-bg=""` (empty string)
- The shader parameters of an element can be set with the `shader-params` and its background with the `shader-bg-params` properties, then giving the shader parameters as a json string.
    - For example, `shader-params='{"shaderProperty1": 1, "shaderProperty2": 2}'`

### Shader properties

#### Shader time
By default, every shader has a property `time` and `timescale`:
- The property `time` represents the starting time of the shader.
- The property `timescale` represents a scaling factor given to the time interpolation.

Shader time (given to the shader on each frame) is calculated using `time + timescale * current frame / FPS`.

#### Setting shader properties
Shader properties can be set in the shader configuration file and as HTML properties with `shader-params`.

As the format of the HTML property is in JSON, property names should be wrapped in `"` double quotation marks and followed up by a colon.

##### Formatting shader property values
For vector values (like colors), wrap the values with braces `[` `]`. (Ex: `[1, 1, 1]`)

##### Shader properties in HTML
Shader properties are set with a json string.

Example: setting a shader `color.frag` with a custom white `color` property, a starting time of 0.25, and a time scale of 0.5.
```
<h1 shader="color.frag" shader-params='{
    "time": 0.25,
    "timescale": 0.5,
    "color": [1,1,1]
}'> Cool Header Element </h1>
```

##### Shader properties with configuration file
Shader properties are set in the yaml format

Example: setting a shader `color.frag` for header elements with a custom white `color` property, a starting time of 0.25, and a time scale of 0.5.

`
heading1:
  content:
    shader_path:
      color.frag
    shader_parameters:
      time: 0.25
      timescale: 0.5
      color: [1,1,1]
  background:
    shader_path:
    shader_parameters:
`

### Extending Shader-md with new shaders

#### Writing new shaders
Currently, the supported shader format is `.frag`.

##### Uniforms
Some uniforms are always assigned to the shader, so the shaders must obey these naming conventions:
- Texture uniform `uniform sampler2D uTexture` is the texture of the element given to the shader
- Resolution uniform `uniform vec2 uResolution` is the resolution of the element given to the shader
- Time uniform `uniform float uTime` is the time given to every shader render (based on `time` and `timescale`)
- UV coordinates `in vec2 vUv` is the computed coordinate in the shader, normalized

*Shader parameters with these uniform names will be ignored.*

#### Default uniform values
Since uniforms cannot be initialized with a default value in `.frag`, they are read from the comments.

To set a uniform default value with a comment, write `@default` followed by `uniformName uniformValue` in a same line.

This can be done in a single line comment or a multiline comment as long as the uniform name and value are in the same line and separated by a space.

Ex:
```
/*
    @default color [1,1,1]
    @default 
    effectAlpha 0.5

    uniform vec3 color;
    uniform float effectAlpha;
*/
```
### Usage

```bash
shader-md <document path> --config <config path> --output <output path> [options]
```

**Required arguments**

| Flag | Description |
|---|---|
| `<document path>` | Path to the input Markdown file. |
| `-c, --config <file>` | Path to the shader configuration YAML file. |
| `-o, --output <file>` | Path to the output document. |

**Options**

| Flag | Description | Default |
|---|---|---|
| `--width <value>` | Document width in pixels. | `1000` |
| `--height <value>` | Document height in pixels. | `0` |
| `--fps <value>` | Frames per second. | `5` |
| `--scale <value>` | Render scale. | `1` |
| `--duration <value>` | Animation duration in seconds. | `1` |
| `--reverseloop` | Reverse the animation between bounds for seamless looping. | off |
| `--bgcolor <value>` | Background color in hex (`#xxxxxx`) or `transparent`. | `#0d1117` |
| `--vslices <value>` | Number of vertical slices to split the output into. Values above 1 export a directory of slices instead of a single file. | `1` |
| `--outputext <value>` | File extension for the output document(s). | `WEBP` |
| `--oef` | Overwrite the file at the output path if it already exists. Without this flag, an existing file halts the process. | off |
| `-h, --help` | Show help and usage information. | |

**Example**

```bash
shader-md notes.md --config shaderConfig.yaml --output notes.webp --width 1200 --fps 10 --duration 3 --reverseloop
```

This renders `notes.md` using the shader assignments in `shaderConfig.yaml`, at 1200px wide, 10 frames per second, over a 3 second animation that loops seamlessly forward and backward.

#### Vertical slices
Specifically for GitHub markdown implementation.

GitHub does not support embedding images with a size over 10MB, so the rendered result can be split into vertical slices and combined in a markdown.

Example with 10 slices:
```
<a href="https://pictureofahotdog.com">
<img src=".../slice_1.webp" width="10%"/><!-- 
--><img src=".../slice_2.webp" width="10%"/><!-- 
--><img src=".../slice_3.webp" width="10%"/><!-- 
--><img src=".../slice_4.webp" width="10%"/><!-- 
--><img src=".../slice_5.webp" width="10%"/><!-- 
--><img src=".../slice_6.webp" width="10%"/><!-- 
--><img src=".../slice_7.webp" width="10%"/><!-- 
--><img src=".../slice_8.webp" width="10%"/><!-- 
--><img src=".../slice_9.webp" width="10%"/><!-- 
--><img src=".../slice_10.webp" width="10%"/>
</a>
```

#### Integrating new shaders
Adding a new visual effect does not require touching any application code:

1. Write a new `.frag` shader and place it under a collective folder for shaders (`shaders`).
2. Reference set collective shader folder in the shader configuration folder (`shaderConfig.yaml`) as the `shaders_root_directory`.
    - The shader directory should be set as an **absolute path**.
2. Set the shader in the shader configuration folder for whichever element(s) it should be applied to.
    - Shaders should be set as a **relative path**.
3. Optionally exposing custom uniforms (parameters) in the shader itself, then setting their values through `shader_parameters` in the config.

Because the shader stage only needs a rasterized surface and a compiled GLSL program, it has no idea whether that surface came from a heading, a table cell, or an image. This keeps the process of adding new looks simple and self-contained.

### Who is this for?

This project is aimed at people who want to turn Markdown into something more expressive than a plain webpage or PDF, for example:

- Stylized or animated documentation
- Generative or art driven text documents
- Shader based presentations or title cards
- Experimenting with procedural typography

It's a technical and creative tool rather than a simple document converter. Getting the most out of it currently means being comfortable editing a YAML configuration file, and eventually writing or sourcing your own GLSL shaders if you want looks beyond the defaults.

---

## For Developers

### Architecture overview

Shader-md is a hybrid **.NET 8 (C#) + TypeScript** project:

```
Markdown file
      │
      ▼
 Markdig (Markdown parsing)
      │
      ▼
 Browser rendering via Microsoft.Playwright
      │
      ▼
 Per-element rasterization
      │
      ▼
 WebGL / GLSL shader pass(es), TypeScript renderer
      │
      ▼
 SixLabors.ImageSharp (compositing / frame processing)
      │
      ▼
 Final output (image / animation)
```

- **C# application**
    - Parses the Markdown (via `Markdig`), reads the shader configuration (via `YamlDotNet`), drives a headless browser (via `Microsoft.Playwright`) to render and lay out the document, and post-processes the resulting frames (via `SixLabors.ImageSharp`). It's exposed as a CLI (`System.CommandLine`).
- **TypeScript renderer**
    - runs inside the browser context that Playwright controls. It sets up WebGL, uploads rendered content as textures, compiles and runs the configured GLSL fragment shaders, and reads back the resulting pixels.
- **`shaderConfig.yaml`**
    - the declarative mapping between Markdown element types and the shaders applied to them.

### Project layout

```
Shader-md/
├── shaders/              GLSL fragment shaders (*.frag)
├── src/                  C# application source
├── web/                  TypeScript shader renderer + document logic
│   ├── shader/
│   │   ├── ShaderRenderer.ts         # animated / per-frame renderer
│   │   └── StaticShaderRenderer.ts   # optimized renderer for unchanging source images
│   └── DocumentFunctions.ts
├── shaderConfig.yaml      Element-to-shader mapping
├── Shader-md.csproj        .NET project file
├── package.json            TypeScript build config (esbuild)
└── tsconfig.json
```

### Build

The .NET build automatically triggers the TypeScript build first (wired via an MSBuild target in `shader-md.csproj`), so a normal build is:

```bash
npm install
dotnet build
```

Under the hood, `npm run build` bundles three TypeScript entry points with `esbuild` into IIFE globals, which are copied into the build/publish output as `Content`:

| Source | Output |
|---|---|
| `web/shader/ShaderRenderer.ts` | `generated/ShaderRenderer.js` |
| `web/shader/StaticShaderRenderer.ts` | `generated/StaticShaderRenderer.js` |
| `web/DocumentFunctions.ts` | `generated/DocumentFunctions.js` |

The `.frag` shader files and `shaderConfig.yaml` are likewise marked as `Content` and copied to the output directory, so the published app is self-contained aside from its Playwright/browser dependency.

### Key dependencies

| Package | Role |
|---|---|
| `Markdig` | Markdown parsing |
| `Microsoft.Playwright` | Headless browser automation / rendering |
| `SixLabors.ImageSharp` | Image/frame post-processing |
| `System.CommandLine` | CLI argument parsing |
| `YamlDotNet` | Reading `shaderConfig.yaml` |
| `esbuild` | Bundling the TypeScript renderer into browser-ready JS |

### Known architectural tradeoffs

- The pipeline is comparatively heavy: Markdown parsing, then HTML/DOM layout in a real browser, then GPU texture upload, then shader execution, then GPU-to-CPU pixel readback, then CPU-side image processing. This is significantly more expensive than a plain Markdown-to-HTML conversion.
- Depending on `Microsoft.Playwright` means the app needs a browser runtime available, adding deployment overhead compared to a purely native renderer.
- `StaticShaderRenderer.ts` exists specifically to reduce cost in cases where the source image does not change between frames, avoiding redundant GPU uploads during animated output.

---
*Note: some developer-facing details (exact CLI flags, precise WebGL call sequence) may evolve — check `src/` and `web/shader/` directly for the current implementation.*
