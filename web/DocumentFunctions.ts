type IdInfo = {
    Idx: number,
    Id: string,
    Depth: number,
}[];

type ElementShaderConfig = {
    Content: ShaderInfo;
    Background: ShaderInfo;
};

type ShaderInfo = {
    ShaderPath : string;
    ShaderParameters : Record<string, any>;
};


// ignoreParentShaders="..." should always ignore parent shaders unless the entered string is false (for convenience)
const parseIgnoreParentShaders = (value : string | null) => value != null && value.trim().toLowerCase() !== "false";

const SHADER_LAYER_ZINDEX = -1;
const SHADER_ID_PREFIX = "shader-";
const SHADER_OUTPUT_CLASSNAME = "shader-output";

const SHADER_KEY = "shader";
const SHADER_BG_KEY = "shader-bg";
const SHADER_PARAMETERS_KEY = "shader-params"
const SHADER_BG_PARAMETERS_KEY = "shader-bg-params";
const IGNORE_PARENT_SHADERS_KEY = "ignoreParentShaders";

const SHADER_FOREGROUND_ID = "shader-foreground-layer";

const HIDE_DESCENDANT_SHADERS_STYLE = `
    html.hide-descendant-shaders
    .${SHADER_OUTPUT_CLASSNAME}[data-shader-descendant="true"] {
        display: none !important;
    }
`
const SHADER_SOURCE_DATA_KEY = "shader-source";

const ShaderSelectors: Record<string, string> = {
    heading1: "h1",
    heading2: "h2",
    heading3: "h3",
    heading4: "h4",
    heading5: "h5",
    heading6: "h6",

    default: "p",

    blockquote: "blockquote",

    bold: "strong, b",
    italic: "em, i",
    bold_italic: "strong em, em strong",

    strikethrough: "del, s",

    inline_code: "code",
    code_block: "pre",

    link: "a",
    image: "img",

    unordered_list: "ul",
    ordered_list: "ol",

    horizontal_rule: "hr",

    table: "table",
    table_header: "th",
    table_cell: "td",

    task_list: "li",
    task_checkbox: 'input[type="checkbox"]',
};

/**
 * @brief Wraps raw HTML in a full document with the base stylesheet.
 * @param html Body content to embed.
 * @param defaultPageShadersParams The default shader parameters for the document, linking each html element type to default shaders and uniforms (or none)
 * @return Complete HTML document string.
 */
export function createShaderizedDocument(html: string, defaultPageShaders? : Record<string, ElementShaderConfig>): string {
    function getShaderStyle(shaderInfo: ShaderInfo, isBackground: boolean): string {
        const key = isBackground ? SHADER_BG_KEY : SHADER_KEY;
        const parameters = shaderInfo.ShaderParameters ?? {};

        let result = `${key}="${shaderInfo.ShaderPath}"`;
        if (Object.keys(parameters).length > 0) {
            result += ` ${isBackground 
                ? SHADER_BG_PARAMETERS_KEY 
                : SHADER_PARAMETERS_KEY
            }=\'${JSON.stringify(parameters)}\'`;
        }
        return result;
    }

    function applyDefaultShaderStyles(html: string): string {
        if (!defaultPageShaders) {
            console.log("no default shaders");
            return html;
        }

        for (const [shaderName, selector] of Object.entries(ShaderSelectors)) {
            const config = defaultPageShaders[shaderName];

            if (!config) {
                continue;
            }

            for (const individualSelector of selector.split(",")) {
                const tag = individualSelector.trim();

                if (tag.startsWith("input[") || tag.includes(" ")) {
                    continue;
                }

                html = html.replace(
                    new RegExp(`<${tag}(\\s[^>]*)?>`, "gi"),
                    (match, existingAttributes = "") => {
                        const hasShader = new RegExp(
                            `(?:^|\\s)${SHADER_KEY}\\s*=`,
                            "i"
                        ).test(existingAttributes);

                        const hasShaderBg = new RegExp(
                            `(?:^|\\s)${SHADER_BG_KEY}\\s*=`,
                            "i"
                        ).test(existingAttributes);

                        const attributesToAdd: string[] = [];

                        if (!hasShader && config.Content?.ShaderPath) {
                            attributesToAdd.push(
                                getShaderStyle(config.Content, false)
                            );
                        }

                        if (!hasShaderBg && config.Background?.ShaderPath) {
                            attributesToAdd.push(
                                getShaderStyle(config.Background, true)
                            );
                        }

                        if (attributesToAdd.length === 0) {
                            return match;
                        }

                        return `<${tag}${existingAttributes} ${attributesToAdd.join(" ")}>`;
                    }
                );
            }
        }
        return html;
    }

    
    return createHTMLPage(
        HIDE_DESCENDANT_SHADERS_STYLE, 
        applyDefaultShaderStyles(html)
    );
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
        id: string;
        dataUrl: string;
    }
): void {
    const rect = element.getBoundingClientRect();
    const computed = getComputedStyle(element);

    const image = document.createElement("img");

    image.id = args.id;
    image.src = args.dataUrl;
    image.alt = "";

    image.style.display = computed.display;
    image.style.verticalAlign = computed.verticalAlign;

    image.style.width = `${rect.width}px`;
    image.style.height = `${rect.height}px`;

    image.style.marginTop = computed.marginTop;
    image.style.marginRight = computed.marginRight;
    image.style.marginBottom = computed.marginBottom;
    image.style.marginLeft = computed.marginLeft;

    image.style.objectFit = "fill";
    image.style.boxSizing = "border-box";

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
 * @brief Assigns stable ids to shader elements if needed and builds their IdInfo.
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
export function createShaderLayerContainer(): void {
    if (document.getElementById(SHADER_FOREGROUND_ID)) {
        return;
    }

    const foregrounds = document.createElement("div");
    foregrounds.id = SHADER_FOREGROUND_ID;
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
    element: HTMLElement,
    args: {
        id: string;
        dataUrl: string;
        depth: number;
        background: boolean;
    }
): void {
    const rect = element.getBoundingClientRect();
    const image = document.createElement("img");

    image.id = args.id;
    image.src = args.dataUrl;
    image.dataset.shaderSource = element.id;

    // Mark this layer if its source is an ignoreParentShaders descendant.
    if (findFirstIgnoreParentShadersAncestor(element)) {
        image.dataset.shaderDescendant = "true";
    }

    image.style.display = "block";
    image.style.objectFit = "fill";
    image.style.pointerEvents = "none";
    
    if (args.background) {
        const computed = getComputedStyle(element);

        // Save the original values before modifying the element.
        if (!element.dataset.shaderPositionStored) {
            element.dataset.shaderPositionStored = "true";
            element.dataset.shaderPreviousPosition = element.style.position;
        }

        if (!element.dataset.shaderZIndexStored) {
            element.dataset.shaderZIndexStored = "true";
            element.dataset.shaderPreviousZIndex = element.style.zIndex;
        }

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

        // Put the shader in layer.
        image.style.zIndex = `${SHADER_LAYER_ZINDEX}`;

        element.prepend(image);
        return;
    }

    image.style.position = "absolute";
    image.style.left = `${rect.left + window.scrollX}px`;
    image.style.top = `${rect.top + window.scrollY}px`;
    image.style.width = `${rect.width}px`;
    image.style.height = `${rect.height}px`;
    image.style.zIndex = String(args.depth);

    const container = document.getElementById(SHADER_FOREGROUND_ID);

    if (!container) {
        throw new Error(
            "Shader foreground container has not been created."
        );
    }
    container.appendChild(image);
}


/**
 * @brief Returns ids of descendants opting out of parent shaders, to exclude from a shader screenshot.
 * An element opts out unless its ignoreParentShaders value is exactly "false" (case-insensitive).
 * @param element Element whose descendants are checked.
 * @return Ids of opted-out descendants.
 */
export function getDescendantsIgnoringParentShaders(element: HTMLElement): string[] {
    const result: string[] = [];

    const descendants = element.querySelectorAll(`[${IGNORE_PARENT_SHADERS_KEY}]`);

    for (const descendant of descendants) {
        if (!(descendant instanceof HTMLElement) || !descendant.id) {
            continue;
        }

        const ignoreParentShadersProperty = descendant.getAttribute(IGNORE_PARENT_SHADERS_KEY) ?? "";

        if (!parseIgnoreParentShaders(ignoreParentShadersProperty)) {
            continue;
        }

        result.push(descendant.id);
    }

    return result;
}

/**
 * @brief Waits for every image in the document to finish loading/decoding,
 * then two animation frames for layout to settle after that.
 * @return Promise resolving once the page has settled.
 */
export function waitForImagesSettled(): Promise<void> {
    const images = Array.from(document.images);
    const pending = images.filter((img) => !img.complete);

    const waitForImages = pending.length === 0
        ? Promise.resolve()
        : Promise.all(
            pending.map((img) => new Promise<void>((resolve) => {
                img.addEventListener("load", () => resolve(), { once: true });
                img.addEventListener("error", () => resolve(), { once: true });
            }))
        );

    return waitForImages.then(() => new Promise<void>((resolve) => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
    }));
}

/**
 * @brief Re-syncs every foreground shader overlay's position/size to its source element's current layout, 
 * correcting for any drift between capture time and final compositing. 
 * 
 * Background overlays are anchored inside their source element and move with it automatically. (so unnecessary)
 */
export function resyncShaderLayerPositions(): void {
    const layers = document.querySelectorAll<HTMLImageElement>(`[data-${SHADER_SOURCE_DATA_KEY}]`);

    for (const layer of layers) {
        if (layer.parentElement?.id !== SHADER_FOREGROUND_ID) {
            continue;
        }

        const sourceId = layer.dataset.shaderSource;
        if (!sourceId) {
            continue;
        }

        const source = document.getElementById(sourceId);
        if (!source) {
            continue;
        }

        const rect = source.getBoundingClientRect();

        layer.style.left = `${rect.left + window.scrollX}px`;
        layer.style.top = `${rect.top + window.scrollY}px`;
        layer.style.width = `${rect.width}px`;
        layer.style.height = `${rect.height}px`;
    }
}

/**
 * Sets the siblings of the elements visible or not using setElementVisible
 * @param element element
 * @param visible visibility of its siblings to set
 * @returns void
 */
export function setSiblingsVisible(element: HTMLElement, visible: boolean): void {
    const parent = element.parentElement;

    if (!parent) {
        return;
    }

    for (const child of parent.children) {
        if (child === element) {
            continue;
        }

        if (child instanceof HTMLElement) {
            setElementVisible(child, visible);
        }
    }
}


/**
 * Sets an element's inline visibility to hidden or restores its previous inline visibility (CSS) value.
 * When hiding an element, its current inline visibility value is stored once so that it can be restored when the element is made visible again.
 * @param element The element whose visibility to set.
 * @param visible Whether the element should be visible.
 * @returns void
*/
export function setElementVisible(element: HTMLElement, visible: boolean): void {
    if (!visible) {
        // Store original visibility (once)
        if (!element.dataset.shaderPreviousVisibilityStored) {
            element.dataset.shaderPreviousVisibilityStored = "true";
            element.dataset.shaderPreviousVisibility = element.style.visibility;
        }

        element.style.visibility = "hidden";
        return;
    }

    // Restoring original visibility 

    if (element.dataset.shaderPreviousVisibilityStored) {
        element.style.visibility = element.dataset.shaderPreviousVisibility ?? "";

        delete element.dataset.shaderPreviousVisibilityStored;
        delete element.dataset.shaderPreviousVisibility;
    } else {
        element.style.visibility = "";
    }
}

/**
 * Sets shader overlay layers and their source elements visible or hidden.
 *
 * @param ids Source element ids to update.
 * @param visible Whether the elements should be visible.
 * @return Number of unique elements/layers changed.
 */
export function setShaderLayersVisible(ids: string[], visible: boolean): number {
    const idSet = new Set(ids);
    const processed = new Set<HTMLElement>();

    const setVisibility = (element: HTMLElement): void => {
        if (!visible) {
            if (element.dataset.shaderPreviousVisibilityStored !== "true") {
                element.dataset.shaderPreviousVisibilityStored = "true";
                element.dataset.shaderPreviousVisibility = element.style.visibility;
            }

            element.style.visibility = "hidden";
            return;
        }

        if (element.dataset.shaderPreviousVisibilityStored === "true") {
            element.style.visibility = element.dataset.shaderPreviousVisibility ?? "";

            delete element.dataset.shaderPreviousVisibility;
            delete element.dataset.shaderPreviousVisibilityStored;
        }
    };

    const layers = document.querySelectorAll(
        `[data-${SHADER_SOURCE_DATA_KEY}]`
    ) as NodeListOf<HTMLElement>;

    for (const layer of layers) {
        const source = layer.dataset.shaderSource;

        if (!source || !idSet.has(source)) {
            continue;
        }

        if (processed.has(layer)) {
            continue;
        }

        processed.add(layer);
        setVisibility(layer);
    }

    for (const id of ids) {
        const element = document.getElementById(id);

        if (!element || processed.has(element)) {
            continue;
        }

        processed.add(element);
        setVisibility(element);
    }

    return processed.size;
}



/**
 * @brief Hides a shaded element behind its overlay, 
 * keeps ignoreParentShaders descendants visible instead of inheriting the hidden state.
 * @param element Element whose shader overlay now stands in for it.
 */
export function hideShaderedElement(element: HTMLElement): void {
    if (element.dataset.shaderOriginalVisibilityStored !== "true") {
        element.dataset.shaderOriginalVisibilityStored = "true";
        element.dataset.shaderOriginalVisibility = element.style.visibility;
    }

    element.style.visibility = "hidden";

    const ignoredDescendantIds = getDescendantsIgnoringParentShaders(element);

    for (const id of ignoredDescendantIds) {
        const descendant = document.getElementById(id);

        if (!descendant || descendant.dataset.shaderOriginalVisibilityStored === "true") {
            continue;
        }

        descendant.style.visibility = "visible";
    }
}


/**
 * Gets full html page string from the content html
 * Formatting similar to Github markdown
 * @param extraStyle extra style in <style> before the body
 * @param pageContent page content inside the <body>
 * @returns the full html document
 */
function createHTMLPage(extraStyle : string, pageContent : string) : string {
    return `
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">

            <style>
                html {
                    margin: 15px;
                    background: transparent;
                    overflow: hidden !important;
                }

                ${extraStyle}

                body {
                    margin: 0;
                    padding: 40px;
                    background: transparent;
                    color: #c9d1d9;
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI",
                                Helvetica, Arial, sans-serif;
                    font-size: 16px;
                    line-height: 1.6;
                    min-height: 100vh;
                    height: auto;
                }

                /* =========================
                Headings
                ========================= */

                h1,
                h2,
                h3,
                h4,
                h5,
                h6 {
                    color: #f0f6fc;
                    font-weight: 600;
                    line-height: 1.25;
                    margin-top: 24px;
                    margin-bottom: 16px;
                }

                h1 {
                    font-size: 2em;
                    padding-bottom: 0.3em;
                    border-bottom: 1px solid rgba(240, 246, 252, 0.15);
                }

                h2 {
                    font-size: 1.5em;
                    padding-bottom: 0.3em;
                    border-bottom: 1px solid rgba(240, 246, 252, 0.15);
                }

                h3 {
                    font-size: 1.25em;
                }

                h4 {
                    font-size: 1em;
                }

                h5 {
                    font-size: 0.875em;
                }

                h6 {
                    font-size: 0.85em;
                    color: #8b949e;
                }

                /* =========================
                Paragraphs
                ========================= */

                p {
                    margin-top: 0;
                    margin-bottom: 16px;
                }

                /* =========================
                Links
                ========================= */

                a {
                    color: #58a6ff;
                    text-decoration: none;
                }

                a:hover {
                    text-decoration: underline;
                }

                /* =========================
                Bold / Italic
                ========================= */

                strong {
                    color: #f0f6fc;
                    font-weight: 600;
                }

                em {
                    color: #e2e8ee;
                }

                /* =========================
                Lists
                ========================= */

                ul,
                ol {
                    margin-top: 0;
                    margin-bottom: 16px;
                    padding-left: 2em;
                }

                li {
                    margin-top: 0.25em;
                }

                li + li {
                    margin-top: 0.25em;
                }

                /* Nested lists */

                ul ul,
                ul ol,
                ol ul,
                ol ol {
                    margin-top: 0;
                    margin-bottom: 0;
                }

                /* =========================
                Inline Code
                ========================= */

                code {
                    font-family: ui-monospace, SFMono-Regular, SFMono-Regular,
                                Menlo, Monaco, Consolas, "Liberation Mono",
                                "Courier New", monospace;

                    font-size: 85%;
                    background: rgba(110, 118, 129, 0.18);
                    padding: 0.2em 0.4em;
                    border-radius: 6px;
                }

                /* =========================
                Code Blocks
                ========================= */

                pre {
                    margin-top: 0;
                    margin-bottom: 16px;
                    padding: 16px;

                    overflow: auto;

                    background: rgba(110, 118, 129, 0.12);
                    border-radius: 6px;
                    border: 1px solid rgba(240, 246, 252, 0.08);

                    line-height: 1.45;
                }

                pre code {
                    display: block;

                    padding: 0;
                    margin: 0;

                    background: transparent !important;
                    border: 0;

                    font-size: 85%;
                    line-height: 1.45;

                    white-space: pre;
                }

                /* =========================
                Blockquotes
                ========================= */

                blockquote {
                    margin: 0 0 16px 0;
                    padding: 0 1em;

                    color: #8b949e;

                    border-left: 0.25em solid #3b434b;
                }

                blockquote > :first-child {
                    margin-top: 0;
                }

                blockquote > :last-child {
                    margin-bottom: 0;
                }

                /* =========================
                Tables
                ========================= */

                table {
                    margin-top: 0;
                    margin-bottom: 16px;

                    border-spacing: 0;
                    border-collapse: collapse;

                    color: #c9d1d9;
                }

                th,
                td {
                    padding: 6px 13px;

                    border: 1px solid rgba(240, 246, 252, 0.15);

                    text-align: left;
                }

                th {
                    color: #f0f6fc;
                    font-weight: 600;
                    background: rgba(110, 118, 129, 0.12);
                }

                tr {
                    background: transparent;
                    border-top: 1px solid rgba(240, 246, 252, 0.15);
                }

                tr:nth-child(2n) {
                    background: rgba(110, 118, 129, 0.04);
                }

                /* =========================
                Horizontal Rule
                ========================= */

                hr {
                    height: 0.25em;
                    padding: 0;
                    margin: 24px 0;

                    background-color: rgba(240, 246, 252, 0.15);

                    border: 0;
                }

                /* =========================
                Images
                ========================= */

                img {
                    max-width: 100%;
                    box-sizing: content-box;
                }

                /* =========================
                Task Lists
                ========================= */

                input[type="checkbox"] {
                    margin-right: 0.5em;
                }

                /* =========================
                Keyboard
                ========================= */

                kbd {
                    display: inline-block;

                    padding: 3px 6px;

                    font-family: ui-monospace, SFMono-Regular, Menlo, Monaco,
                                Consolas, "Liberation Mono", "Courier New",
                                monospace;

                    font-size: 11px;
                    line-height: 10px;

                    color: #c9d1d9;

                    background: #161b22;

                    border: 1px solid #6e7681;
                    border-bottom-color: #6e7681;

                    border-radius: 6px;

                    box-shadow: inset 0 -1px 0 #6e7681;
                }

                /* =========================
                Definition / Details
                ========================= */

                details {
                    margin-bottom: 16px;
                }

                summary {
                    cursor: pointer;
                    color: #58a6ff;
                }

                /* =========================
                First / Last Elements
                ========================= */

                body > :first-child {
                    margin-top: 0 !important;
                }

                body > :last-child {
                    margin-bottom: 0 !important;
                }
            </style>
        </head>

        <body>
            ${pageContent}
        </body>
    </html>
    `
}


/**
 * @brief Finds the nearest ancestor (or self) opted into ignoreParentShaders,
 * skipping past any ancestor whose value is exactly "false" (case-insensitive)
 * to keep checking further up the tree.
 * @param element Element to start searching from.
 * @return The nearest opted-in ancestor/self, or null if none found.
 */
function findFirstIgnoreParentShadersAncestor(element: Element): Element | null {
    let current: Element | null = element;

    while (current) {
        if (current.hasAttribute(IGNORE_PARENT_SHADERS_KEY)) {
            if (parseIgnoreParentShaders(current.getAttribute(IGNORE_PARENT_SHADERS_KEY))) {
                return current;
            }
        }

        current = current.parentElement;
    }

    return null;
}