export function createDocumentBackground(
    dataUrl: string,
    id: string,
    width: number,
    height: number
) : string {
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

    // Background layer.
    img.style.zIndex = "-1";

    document.documentElement.prepend(img);

    return img.id;
}
