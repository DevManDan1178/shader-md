export function replaceElementWithImage(
    element : HTMLElement, 
    args : {
        id : string, 
        dataUrl: string
    }
) {
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