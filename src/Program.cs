using Markdig;
using ShaderMarkdown.Rendering;

string inputPath = "markdown/TestSuite.md";
string outputPath = "output/TestSuite2.gif";


ShaderInfo? backgroundShaderInfo = ShaderInfo.FromShaderFileName("driftingSquares.frag", new ShaderParameters(0.0f, 0.01f, new ())); 
ShaderInfo? outerShaderInfo = ShaderInfo.FromShaderFileName("halfBright.frag", new());

var markdown = File.ReadAllText(inputPath);

var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();

var html = Markdown.ToHtml(markdown, pipeline);

var shaderProcessor = new ShaderProcessor();
var renderer = new HtmlShaderRenderer(shaderProcessor);

var stopwatch = System.Diagnostics.Stopwatch.StartNew();
await renderer.RenderAsync(
    html: html,
    outputPath: outputPath,
    width: 800,
    height: 100,
    fps: 10,
    duration: 1,
    scale: 4,
    backgroundColor: " #0d1117",
    backgroundShaderInfo: backgroundShaderInfo,
    outerShaderInfo: outerShaderInfo
);
stopwatch.Stop();


Console.WriteLine($"Shaderized {inputPath} to {outputPath} in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
