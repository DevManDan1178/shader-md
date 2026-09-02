using Markdig;
using ShaderMarkdown.Rendering;
using ShaderMarkdown.FilePaths;
using ShaderMarkdown.Config;

string inputPath = "markdown/TestSuite.md";
string outputPath = "output/TestSuite2.gif";

/*
ShaderInfo? backgroundShaderInfo = ShaderInfo.FromShaderFileName(shaderRootDirectory, "driftingSquares.frag", new ShaderParameters(0.0f, 0.01f, new()));

ShaderInfo? finalizeShaderInfo = null; //ShaderInfo.FromShaderFileName(shaderRootDirectory,"halfBright.frag", new());
*/
var parameters = new ShaderizeDocumentParameters() {
    Paths = new ShaderizeDocumentParameters.IOPaths {
        Input = inputPath,
        Output = outputPath,
    },
    DocRenderSettings = new ShaderizeDocumentParameters.RenderSettings  {
        DocSize = new ShaderizeDocumentParameters.RenderSettings.DocumentSize {
            Width = 800,
            Height = 100
        }
    },
    FPS = 10,
};
string shaderConfigPath = Path.Combine(AppContext.BaseDirectory, "shaderConfig.yaml");
if (!File.Exists(shaderConfigPath)) {
    throw new FileNotFoundException($"Shader config not found: {shaderConfigPath}.");
}

ShaderConfig shaderConfig;
shaderConfig = ShaderConfig.ReadFromYAML(File.ReadAllText(shaderConfigPath));

 
var stopwatch = System.Diagnostics.Stopwatch.StartNew();

Console.WriteLine("Preparing to shaderize.");

await ShaderizeDocument(
    parameters,
    shaderConfig
);

stopwatch.Stop();

Console.WriteLine($"Shaderized {inputPath} to {outputPath} " + $"in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");


static async Task ShaderizeDocument(ShaderizeDocumentParameters parameters, ShaderConfig shaderConfig) {
    string? markdown = File.ReadAllText(
        parameters.Paths.Input
    );

    var pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    string? html = Markdown.ToHtml(markdown, pipeline);

    var shaderProcessor = new ShaderProcessor();

    var renderer = new HtmlShaderRenderer(shaderProcessor);

    await renderer.RenderAsync(
        html: html,
        shaderConfig,
        outputPath: parameters.Paths.Output,
        width: parameters.DocRenderSettings.DocSize.Width,
        height: parameters.DocRenderSettings.DocSize.Height,
        fps: parameters.FPS,
        duration: parameters.Duration,
        scale: parameters.Scale  
    );
}

