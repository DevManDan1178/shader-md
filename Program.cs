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
    html,
    "output/README.png",
    width: 1200,
    height: 800,
    scale: 2
);

Console.WriteLine("Rendered to output/");
