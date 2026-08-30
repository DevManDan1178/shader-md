type IdInfo = {
    Idx: number,
    Id: string,
    Depth: number,
}[];

const SHADER_ID_PREFIX = "shader-";
const SHADER_OUTPUT_CLASSNAME = "shader-output";

const SHADER_KEY = "shader";
const SHADER_BG_KEY = "shader-bg";
const IGNORE_PARENT_SHADERS_KEY = "ignoreParentShaders";

/**
 * @brief Wraps raw HTML in a full document with the base stylesheet.
 * @param html Body content to embed.
 * @return Complete HTML document string.
 */
export function createDocument(html: string): string {
    return `
        <!DOCTYPE html>
        <html>
            <head>
                <meta charset="UTF-8">

                <style>
                    html {
                        margin: 15px;
                        background: transparent;
                    }

                    html.hide-descendant-shaders
                    .${SHADER_OUTPUT_CLASSNAME}[data-shader-descendant="true"] {
                        display: none !important;
                    }

                    body {
                        margin: 0;
                        padding: 40px;
                        background: transparent;
                        color: #c9d1d9;
                        font-family: Arial, sans-serif;
                        min-height: 100vh;
                        height: auto;
                    }

                    h1 {
                        color: #ffffff;
                    }

                    h2 {
                        color: #ffffff;
                    }

                    code {
                        background: #161b22;
                        padding: 2px 5px;
                        border-radius: 4px;
                    }

                    pre {
                        background: #161b22;
                        padding: 16px;
                        border-radius: 8px;
                    }

                    img {
                        max-width: 100%;
                    }
                </style>
            </head>

            <body>
                ${html}
            </body>
        </html>
    `;
}

/**
 * @brief Inserts a full-page background image behind all page content.
 * @param dataUrl Image data URL to use.
 * @param id Id to assign the created image element.
 * @param width Image width in pixels.
 * @param height Image height in pixels.
 * @return Id of the created image element.
 */
export function createDocumentBackground(
    dataUrl: string,
    id: string,
    width: number,
    height: number
): string {
    const img = document.createElement("img");

    img.id = id;
    img.src = dataUrl;

    img.style.position = "absolute";
    img.style.left = "0";
    img.style.top = "0";

    img.style.width = `${width}px`;
    img.style.height = `${height}px`;

    img.style.display = "block";
    img.style.pointerEvents = "none";
    img.style.zIndex = "-1";

    document.documentElement.prepend(img);

    return img.id;
}

/**
 * @brief Replaces an element with an image sized and spaced to match its original layout.
 * @param element Element being replaced.
 * @param args Id and data URL for the replacement image.
 */
export function replaceElementWithImage(
    element: HTMLElement,
    args: {
        id: string,
        dataUrl: string
    }
): void {
    const rect = element.getBoundingClientRect();
    const computed = getComputedStyle(element);

    const image = document.createElement("img");

    image.id = args.id;
    image.src = args.dataUrl;

    image.style.width = rect.width + "px";
    image.style.height = rect.height + "px";

    image.style.display = computed.display;
    image.style.verticalAlign = computed.verticalAlign;

    image.style.marginTop = computed.marginTop;
    image.style.marginRight = computed.marginRight;
    image.style.marginBottom = computed.marginBottom;
    image.style.marginLeft = computed.marginLeft;

    image.style.objectFit = "fill";

    element.replaceWith(image);
}

/**
 * @brief Finds all elements carrying a shader, shader-bg, or ignoreParentShaders attribute.
 * @return Matching elements.
 */
function getShaderElements(): HTMLElement[] {
    return Array.from(
        document.querySelectorAll(`[${SHADER_KEY}], [${SHADER_BG_KEY}], [${IGNORE_PARENT_SHADERS_KEY}]`)
    ) as HTMLElement[];
}

/**
 * @brief Assigns stable ids to shader elements (if missing) and builds their IdInfo.
 * @param elements Elements to process, in the desired output order.
 * @return IdInfo entries matching the input order.
 */
function shaderElementsToIdInfo(elements: HTMLElement[]): IdInfo {
    return elements.map((element, idx) => {
        const existingId = element.id;

        const id = existingId && existingId.startsWith(SHADER_ID_PREFIX)
            ? existingId
            : `${SHADER_ID_PREFIX}${idx}`;

        element.id = id;

        return {
            Idx: idx,
            Id: id,
            Depth: getDepth(element),
        };
    });
}

/**
 * @brief Gets IdInfo for all shader elements, in document order.
 * @return IdInfo entries.
 */
export function getShaders(): IdInfo {
    const elements = getShaderElements();

    return shaderElementsToIdInfo(elements);
}

/**
 * @brief Gets IdInfo for all shader elements, ordered deepest-in-the-tree first.
 * @return IdInfo entries.
 */
export function getShadersDeepestFirst(): IdInfo {
    const elements = getShaderElements();

    elements.sort((a, b) => {
        return getDepth(b) - getDepth(a);
    });

    return shaderElementsToIdInfo(elements);
}

/**
 * @brief Counts how many ancestors an element has.
 * @param element Element to measure.
 * @return Number of ancestor elements.
 */
function getDepth(element: Element): number {
    let depth = 0;
    let current = element.parentElement;

    while (current) {
        ++depth;
        current = current.parentElement;
    }

    return depth;
}
/**
 * @brief Creates the foreground overlay container, if it doesn't already exist.
 */
export function createShaderLayerContainer() {
    if (document.getElementById("shader-foreground-layer")) {
        return;
    }

    const foregrounds = document.createElement("div");
    foregrounds.id = "shader-foreground-layer";
    foregrounds.style.position = "absolute";
    foregrounds.style.left = "0";
    foregrounds.style.top = "0";
    foregrounds.style.width = "100%";
    foregrounds.style.height = "100%";
    foregrounds.style.pointerEvents = "none";
    foregrounds.style.zIndex = "2000";

    document.body.appendChild(foregrounds);
}

/**
 * @brief Creates an overlay image positioned over an element and adds it to the shader layer container.
 * @param element Element the overlay is positioned over.
 * @param args Id, data URL, depth, and layer (background/foreground) for the overlay.
 */
export function createShaderLayer(
    element : HTMLElement, 
    args: {
        id: string,
        dataUrl: string,
        depth : number,
        background: string,
    }
) {
    const rect = element.getBoundingClientRect();
    const image = document.createElement("img");

    image.id = args.id;
    image.src = args.dataUrl;
    image.dataset.shaderSource = element.id;
    image.style.display = "block";
    image.style.objectFit = "fill";
    image.style.pointerEvents = "none";

    if (args.background) {
        const computed = getComputedStyle(element);

        if (computed.position === "static") {
            element.style.position = "relative";
        }

        if (computed.zIndex === "auto") {
            element.style.zIndex = "0";
        }

        image.style.position = "absolute";
        image.style.left = "0";
        image.style.top = "0";
        image.style.width = "100%";
        image.style.height = "100%";
        image.style.zIndex = "-1";

        element.prepend(image);
    } else {
        image.style.position = "absolute";
        image.style.left = (rect.left + window.scrollX) + "px";
        image.style.top = (rect.top + window.scrollY) + "px";
        image.style.width = rect.width + "px";
        image.style.height = rect.height + "px";
        image.style.zIndex = String(args.depth);

        const container = document.getElementById("shader-foreground-layer")!;
        container.appendChild(image);
    }
}

/**
 * @brief Returns ids of descendants marked ignoreParentShaders, to exclude from a shader screenshot.
 * @param element Element whose descendants are checked.
 * @return Ids of ignored descendants.
 */
export function getScreenshotIgnoredDescendants(element : HTMLElement) : string[] {
    const result = [];
    const descendants = element.querySelectorAll("[ignoreParentShaders]");

    for (const descendant of descendants) {
        if (descendant.id) {
            result.push(descendant.id);
        }
    }

    return result;
}

/**
 * @brief Hides shader overlay layers and their source elements, saving prior visibility for restore.
 * @param ids Source element ids to hide.
 * @return Number of elements/layers hidden.
 */
export function hideShaderLayers(ids : string[]) : number {
    let hiddenCount : number = 0;
    const idSet = new Set(ids);

    const layers = document.querySelectorAll(
        "[data-shader-source]"
    ) as NodeListOf<HTMLElement>;

    for (const layer of layers) { 
        const source = layer.dataset.shaderSource;
        if (!source || !idSet.has(source)) {
            continue;
        }

        if (!layer.dataset.shaderPreviousVisibilityStored) {
            layer.dataset.shaderPreviousVisibilityStored = "true";
            layer.dataset.shaderPreviousVisibility = layer.style.visibility;
        }

        layer.style.visibility = "hidden";
        hiddenCount++;
    }

    for (const id of ids) {
        const el = document.getElementById(id);
        if (!el) {
            continue;
        }

        if (!el.dataset.shaderPreviousVisibilityStored) {
            el.dataset.shaderPreviousVisibilityStored = "true";
            el.dataset.shaderPreviousVisibility = el.style.visibility;
        }

        el.style.visibility = "hidden";
        hiddenCount++;
    }

    return hiddenCount;
}

/**
 * @brief Restores visibility previously saved by hideShaderLayers.
 * @param ids Source element ids to restore.
 */
export function showShaderLayers(ids : string[]) {
    const idSet = new Set(ids);

    const layers = document.querySelectorAll(
        "[data-shader-source]"
    ) as NodeListOf<HTMLElement>;

    for (const layer of layers) {
        const source = layer.dataset.shaderSource;
        if (!source || !idSet.has(source)) {
            continue;
        }

        if (layer.dataset.shaderPreviousVisibilityStored !== "true") {
            continue;
        }

        layer.style.visibility = layer.dataset.shaderPreviousVisibility || "";
        delete layer.dataset.shaderPreviousVisibility;
        delete layer.dataset.shaderPreviousVisibilityStored;
    }

    for (const id of ids) {
        const el = document.getElementById(id);
        if (!el) {
            continue;
        }

        if (el.dataset.shaderPreviousVisibilityStored !== "true") {
            continue;
        }

        el.style.visibility = el.dataset.shaderPreviousVisibility || "";
        delete el.dataset.shaderPreviousVisibility;
        delete el.dataset.shaderPreviousVisibilityStored;
    }
}

/**
 * @brief Hides an element now that its shader overlay stands in for it; un-hides
 * descendants forced invisible by the visibility:hidden cascade (e.g. ignoreParentShaders).
 * @param element Element to hide.
 */
export function hideOriginalElement(element : HTMLElement) {
    element.dataset.shaderOriginalVisibility = element.style.visibility;
    element.style.visibility = "hidden";

    const descendants = element.querySelectorAll("*");
    for (const descendant of descendants) {
        const el = descendant as HTMLElement;
        if (el.style.visibility !== "hidden") {
            el.dataset.shaderForcedVisible = "true";
            el.style.visibility = "visible";
        }
    }
}