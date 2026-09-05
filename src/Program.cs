using Markdig;
using ShaderMarkdown.Rendering;
using ShaderMarkdown.Config;
using System.Diagnostics;
using ShaderMarkdown.Exporting;
using ShaderMarkdown.Files;

partial class Program {
    static async Task Main(string[] args) {
        CommandLineOptions? options = CommandLineOptions.ParseCommandLineArgs(args);
        if (options == null) {
            return;
        }
        if (!options.Input.Exists) {
            throw new FileNotFoundException($"Document not found: \"{options.Input}\".");
        }

        if (!options.ShaderConfig.Exists) {
            throw new FileNotFoundException($"Shader config not found: \"{options.ShaderConfig}\".");
        }
        if (FileExtension.GetAnimatedFileExtension(options.Output.FullName) == null) {
            throw new FormatException($"Output file extension is unsupported: \"{Path.GetExtension(options.Output.Name)}\"");
        }

        Console.WriteLine($"Input: {options.Input.FullName}, Output: {options.Output.FullName}, Shader Config: {options.ShaderConfig.FullName}");

        ShaderizeDocumentParameters parameters = new ShaderizeDocumentParameters() {
            Paths = new ShaderizeDocumentParameters.IOPaths {
                Input = options.Input.FullName,
                Output = options.Output.FullName,
            },
            DocRenderSettings = new ShaderizeDocumentParameters.RenderSettings  {
                DocSize = new ShaderizeDocumentParameters.RenderSettings.DocumentSize {
                    Width = options.Width,
                    Height = options.Height,
                }
            },
            FPS = options.FPS,
            Scale = options.Scale,
            Duration = options.Duration,
            ReverseLoopFromEnd = options.ReverseLoopFromEnd
        };

        Console.WriteLine("Reading shader configurations");
        ShaderConfig shaderConfig = ShaderConfig.ReadFromYAML(
            File.ReadAllText(options.ShaderConfig.FullName)
        );
        if (string.IsNullOrWhiteSpace(shaderConfig.ShadersRootDirectory)) {
            throw new ArgumentNullException($"Empty shader root directory path in shader config at \"{options.ShaderConfig.FullName}\".");
        } else if (!Directory.Exists(shaderConfig.ShadersRootDirectory)) {
            throw new FileNotFoundException($"Shader root directory not found at path \"{shaderConfig.ShadersRootDirectory}\".");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        Console.WriteLine("Preparing to shaderize.");
        
        await ShaderizeDocument(
            parameters,
            shaderConfig
        );
        
        stopwatch.Stop();
        Console.WriteLine($"Shaderized {options.Input.FullName} to {options.Output.FullName} " + $"in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
    }

    static async Task ShaderizeDocument(ShaderizeDocumentParameters parameters, ShaderConfig shaderConfig) {
        string? markdown = File.ReadAllText(parameters.Paths.Input);

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        string? html = Markdown.ToHtml(markdown, pipeline);

        var shaderProcessor = new ShaderProcessor();

        var renderer = new HtmlShaderRenderer(shaderProcessor);

        byte[][] documentFrames = await renderer.GetShaderizedHTMLAsync(
            html: html,
            shaderConfig,
            width: parameters.DocRenderSettings.DocSize.Width,
            height: parameters.DocRenderSettings.DocSize.Height,
            fps: parameters.FPS,
            duration: parameters.Duration,
            scale: parameters.Scale,
            reverseLoopFromEnd: parameters.ReverseLoopFromEnd
        );

        await AnimatedExporter.ExportAnimatedAsync(documentFrames, parameters.FPS, parameters.Paths.Output);
    }

}
