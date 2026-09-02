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

const SHADER_ID_PREFIX = "shader-";
const SHADER_OUTPUT_CLASSNAME = "shader-output";

const SHADER_KEY = "shader";
const SHADER_BG_KEY = "shader-bg";
const IGNORE_PARENT_SHADERS_KEY = "ignoreParentShaders";

const SHADER_FOREGROUND_ID = "shader-foreground-layer";

const HIDE_DESCENDANT_SHADERS_STYLE = `
    html.hide-descendant-shaders
    .${SHADER_OUTPUT_CLASSNAME}[data-shader-descendant="true"] {
        display: none !important;
    }
`

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

        return `${key}="${shaderInfo.ShaderPath}"` +
            (Object.keys(parameters).length > 0
                ? JSON.stringify(parameters)
                : "");
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
        console.log(html);
        return html;
    }

    
    return createHTMLPage(
        HIDE_DESCENDANT_SHADERS_STYLE, 
        applyDefaultShaderStyles(html)
    );
}

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
                    }

                    ${extraStyle}

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
                ${pageContent}
            </body>
        </html>
    `
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

        const container = document.getElementById(SHADER_FOREGROUND_ID)!;
        container.appendChild(image);
    }
}

/**
 * @brief Returns ids of descendants marked ignoreParentShaders, to exclude from a shader screenshot.
 * @param element Element whose descendants are checked.
 * @return Ids of ignored descendants.
 */
export function getDescendantsIgnoringParentShaders(element : HTMLElement) : string[] {
    const result = [];
    const descendants = element.querySelectorAll(`[${IGNORE_PARENT_SHADERS_KEY}]`);

    for (const descendant of descendants) {
        if (descendant.id) {
            result.push(descendant.id);
        }
    }

    return result;
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
        // Only remember the original value once.
        if (!element.dataset.shaderPreviousVisibilityStored) {
            element.dataset.shaderPreviousVisibilityStored = "true";
            element.dataset.shaderPreviousVisibility = element.style.visibility;
        }

        element.style.visibility = "hidden";
        return;
    }

    // Restore the original inline visibility.
    if (element.dataset.shaderPreviousVisibilityStored) {
        element.style.visibility = element.dataset.shaderPreviousVisibility ?? "";

        delete element.dataset.shaderPreviousVisibilityStored;
        delete element.dataset.shaderPreviousVisibility;
    } else {
        // If we didn't hide it ourselves, make it visible.
        element.style.visibility = "";
    }
}

/**
 * Sets shader overlay layers and their source elements visible or hidden.
 * [data-shader-source]
 * When hiding, the current inline visibility is saved so it can be restored when the elements are made visible again.
 * @param ids Source element ids to update.
 * @param visible Whether the elements should be visible.
 * @return Number of elements/layers hidden or shown.
 */
export function setShaderLayersVisible(ids: string[], visible: boolean): number {
    let count = 0;
    const idSet = new Set(ids);

    const setVisibility = (element: HTMLElement) => {
        if (!visible) {
            // Only save the original value once.
            if (element.dataset.shaderPreviousVisibilityStored !== "true") {
                element.dataset.shaderPreviousVisibilityStored = "true";
                element.dataset.shaderPreviousVisibility = element.style.visibility;
            }

            element.style.visibility = "hidden";
            return;
        }

        // Restore the original inline visibility.
        if (element.dataset.shaderPreviousVisibilityStored === "true") {
            element.style.visibility =
                element.dataset.shaderPreviousVisibility ?? "";

            delete element.dataset.shaderPreviousVisibility;
            delete element.dataset.shaderPreviousVisibilityStored;
        }
    };

    const layers = document.querySelectorAll(
        "[data-shader-source]"
    ) as NodeListOf<HTMLElement>;

    for (const layer of layers) {
        const source = layer.dataset.shaderSource;
        if (!source || !idSet.has(source)) {
            continue;
        }

        setVisibility(layer);
        count++;
    }

    for (const id of ids) {
        const el = document.getElementById(id);
        if (!el) {
            continue;
        }

        setVisibility(el);
        count++;
    }

    return count;
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