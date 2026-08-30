using Markdig;
using ShaderMarkdown.Rendering;

var markdown = File.ReadAllText("markdown/README.md");

var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();

var html = Markdown.ToHtml(markdown, pipeline);

var shaderProcessor = new ShaderProcessor();

var renderer = new HtmlRenderer(shaderProcessor);

await renderer.RenderAsync(
    html: html,
    outputPath: "output/README.gif",
    width: 800,
    height: 100,
    fps: 15,
    duration: 1,
    scale: 2,
    backgroundColor: " #0d1117",
    backgroundShader: "test",
    outerShader: "rainbow"
);

Console.WriteLine("Rendered to output/");
