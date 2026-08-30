export function createElementBackground(
    element: HTMLElement,
    id: string
) {
    const computed = getComputedStyle(element);

    if (computed.position === "static") {
        element.style.position = "relative";
    }

    element.style.zIndex =
        computed.zIndex === "auto"
            ? "0"
            : computed.zIndex;

    const image = document.createElement("img");

    image.id = id;

    image.style.position = "absolute";
    image.style.inset = "0";
    image.style.width = "100%";
    image.style.height = "100%";
    image.style.objectFit = "fill";
    image.style.pointerEvents = "none";
    image.style.zIndex = "-1";

    element.prepend(image);
}
