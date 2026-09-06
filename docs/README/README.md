# shader-md

Turn a Markdown file into a visually styled document by applying GPU shader effects to it — headings, paragraphs, lists, code blocks, tables, images, and more can each get their own animated or stylized look.

---

## For Users

### What is this?

Normally, a Markdown file gets turned into plain HTML with basic styling — a heading is just bold, bigger text; a code block is just a gray box. **shader-md** does something different: it renders your Markdown document and then runs each part of it through a GLSL shader (the same kind of GPU effect used in games and visual art) to give it a distinct visual treatment.

Think of it like this: instead of your document just *looking* like text on a page, individual pieces of it — a title, a quote, an image, a code snippet — can each be given their own visual effect (glow, distortion, animation, color shifting, texture, etc.), and the whole finished document can then get one more effect applied over the top of everything, like a filter over a photo.

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
- Apply a **document-wide background shader** and a **final shader pass** over the completed document (similar to a full-screen post-processing filter).
- Produce **animated output**, not just a static image, since shaders can react to time.

### How it works, in plain terms

1. You write a normal Markdown file.
2. You (or a config file) decide which shader effects should apply to which parts of the document.
3. shader-md renders your document in a real browser so the layout, fonts, and formatting look correct.
4. Each piece of the rendered document is handed off to the GPU, where the assigned shader transforms how it looks.
5. Everything is composited back together, optionally with one last shader pass over the whole thing.
6. You get back a styled — and potentially animated — version of your document.

### Who is this for?

This project is aimed at people who want to turn Markdown into something more expressive than a plain webpage or PDF — for example:

- Stylized or animated documentation
- Generative/art-driven text documents
- Shader-based presentations or title cards
- Experimenting with procedural typography

It's a technical/creative tool rather than a simple document converter — getting the most out of it currently means being comfortable writing or sourcing your own GLSL shaders and editing a YAML configuration file.

---

## For Developers

### Architecture overview

shader-md is a hybrid **.NET 8 (C#) + TypeScript** project:

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
 WebGL / GLSL shader pass(es) — TypeScript renderer
      │
      ▼
 SixLabors.ImageSharp (compositing / frame processing)
      │
      ▼
 Final output (image / animation)
```

- **C# application** — the main orchestrator. It parses the Markdown (via `Markdig`), reads the shader configuration (via `YamlDotNet`), drives a headless browser (via `Microsoft.Playwright`) to render and lay out the document, and post-processes the resulting frames (via `SixLabors.ImageSharp`). It's exposed as a CLI (`System.CommandLine`).
- **TypeScript renderer** — runs inside the browser context that Playwright controls. It sets up WebGL, uploads rendered content as textures, compiles and runs the configured GLSL fragment shaders, and reads back the resulting pixels.
- **GLSL shaders** (`shaders/`) — `.frag` fragment shaders referenced from the YAML config.
- **`shaderConfig.yaml`** — the declarative mapping between Markdown element types and the shaders applied to them.

### Project layout

```
shader-md/
├── shaders/              GLSL fragment shaders (*.frag)
├── src/                  C# application source
├── web/                  TypeScript shader renderer + document logic
│   ├── shader/
│   │   ├── ShaderRenderer.ts         # animated / per-frame renderer
│   │   └── StaticShaderRenderer.ts   # optimized renderer for unchanging source images
│   └── DocumentFunctions.ts
├── shaderConfig.yaml      Element-to-shader mapping
├── shader-md.csproj        .NET project file
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

### Configuring shaders (`shaderConfig.yaml`)

The config file defines, at minimum:

- `shaders_root_directory` — base path that all shader paths in the file are relative to.
- `document_shaders.background` / `document_shaders.finalize` — a shader applied behind the whole document, and one applied as a final full-document pass, respectively.
- `default_page_element_shaders.<element>.content` / `.background` — per-element-type shader assignment, where `<element>` is one of the supported Markdown constructs (`heading1`–`heading6`, `default` for paragraphs, `blockquote`, `bold`, `italic`, `bold_italic`, `strikethrough`, `inline_code`, `link`, `image`, `unordered_list`, `ordered_list`, `list_marker`, `task_list`, `task_checkbox`, `code_block`, `horizontal_rule`, `table`, `table_header`, `table_cell`).

Each of these accepts a `shader_path` (relative to `shaders_root_directory`) and a `shader_parameters` block for uniform values passed to that shader.

### Extending shader-md

Because the shader stage is generic — it only needs a rasterized surface and a compiled GLSL program — adding a new visual effect typically means:

1. Writing a new `.frag` shader in `shaders/`.
2. Referencing it from `shaderConfig.yaml` for the element(s) you want it applied to.
3. Optionally exposing custom uniforms in the shader and setting their values via `shader_parameters`.

No changes to the C# orchestration code should be required for a purely new visual effect.

### Known architectural tradeoffs

- The pipeline is comparatively heavy: Markdown parsing → HTML/DOM layout in a real browser → GPU texture upload → shader execution → GPU→CPU pixel readback → CPU-side image processing. This is significantly more expensive than a plain Markdown-to-HTML conversion.
- Depending on `Microsoft.Playwright` means the app needs a browser runtime available, adding deployment overhead compared to a purely native renderer.
- `StaticShaderRenderer.ts` exists specifically to reduce cost in cases where the source image doesn't change between frames, avoiding redundant GPU uploads during animated output.

---

*Note: some developer-facing details (exact CLI flags, precise WebGL call sequence) may evolve — check `src/` and `web/shader/` directly for the current implementation.*