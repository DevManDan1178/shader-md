using Markdig;
using ShaderMarkdown.Rendering;

var markdown = File.ReadAllText("markdown/TestSuite.md");

var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();

var html = Markdown.ToHtml(markdown, pipeline);

var shaderProcessor = new ShaderProcessor();

var renderer = new HtmlShaderRenderer(shaderProcessor);

ShaderInfo backgroundShaderInfo = ShaderInfo.FromShaderFileName("test.frag"); 
string outputPath = "output/TestSuite2.gif";

await renderer.RenderAsync(
    html: html,
    outputPath: outputPath,
    width: 800,
    height: 100,
    fps: 3,
    duration: 1,
    scale: 4,
    backgroundColor: " #0d1117",
    backgroundShaderInfo: backgroundShaderInfo,
    outerShaderInfo: null 
);

Console.WriteLine($"Rendered to {outputPath}");
