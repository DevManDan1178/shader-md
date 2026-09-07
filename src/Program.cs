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
            throw new FileNotFoundException($"File not found: \"{options.Input}\".");
        }
        if (!options.ShaderConfig.Exists) {
            throw new FileNotFoundException($"Shader configuration document not found: \"{options.ShaderConfig}\".");
        }
        
        bool shaderizingDirectory = options.Input.Attributes.HasFlag(FileAttributes.Directory);
        
        if (!shaderizingDirectory) {
            if (FileExtension.GetAnimatedFileExtension(options.Output) == null) {
                throw new FormatException($"Output file extension is unsupported: \"{Path.GetExtension(options.Output)}\".");
            }
            if (!FileExtension.IsSupportedDocumentExtension(options.Input.FullName)) {
                throw new FormatException($"Document file extension is unsupported: \"{options.Input.FullName}\".");
            }
        }
        
        ShaderizeDocumentParameters parameters = new ShaderizeDocumentParameters() {
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

        IOPaths paths = new () {
            Input = options.Input.FullName,
            Output = options.Output,
            OutputExtension = (AnimatedFileExtension) options.outputExtension!,
        };

        ShaderConfig shaderConfig = ShaderConfig.ReadFromYAML(
            File.ReadAllText(options.ShaderConfig.FullName)
        );

        if (string.IsNullOrWhiteSpace(shaderConfig.ShadersRootDirectory)) {
            throw new ArgumentNullException($"Empty shader root directory path in shader config at \"{options.ShaderConfig.FullName}\".");
        } else if (!Directory.Exists(shaderConfig.ShadersRootDirectory)) {
            throw new FileNotFoundException($"Shader root directory not found at path \"{shaderConfig.ShadersRootDirectory}\".");
        }
        
        
        Stopwatch stopwatch = Stopwatch.StartNew();

        Console.WriteLine($"Preparing to shaderize \"{options.Input.FullName}\" to target \"{options.Output}\" with shader configurations at \"{options.ShaderConfig.FullName}\".");
        
        if (shaderizingDirectory) {
            await ShaderizeDocumentDirectory(
                parameters,
                shaderConfig,
                paths
            );
            Console.WriteLine($"Shaderized directory {options.Input.FullName} to {options.Output} " + $"in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
        } else {
            string? outputPath = await ShaderizeDocument(
                parameters,
                shaderConfig,
                paths
            );
            Console.WriteLine(outputPath != null 
                ? $"Shaderized {options.Input.FullName} to {outputPath} in {stopwatch.Elapsed.TotalSeconds:F2} seconds." 
                : $"Export failed of shaderized document from {outputPath}. Total process duration: {stopwatch.Elapsed.TotalSeconds:F2}."
            );
        }
        stopwatch.Stop();   
    }

    /// <summary>
    /// Applies the shader to every markdown file in the directory at the input path
    /// The resulting exported files will follow the same file structure as the original directory
    /// The exported directory be at the output path with the output name
    /// 
    /// Creates the directories of the output in advance
    /// </summary>
    /// <param name="parameters">Parameters for shaderizing the document</param>
    /// <param name="shaderConfig">Shader configuration parameters</param>
    /// <returns>Task for when it finishes</returns>
    static async Task ShaderizeDocumentDirectory(ShaderizeDocumentParameters parameters, ShaderConfig shaderConfig, IOPaths paths) {
        
        ParallelOptions parallelOptions = new ParallelOptions {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
        // Avoid multithreading on shaders for multithreading on files instead
        ShaderProcessor shaderProcessor = new ShaderProcessor(false);
        List<IOPaths> directoryFiles = new();

        void findDirectoryDocumentsRecursive(string[] subPaths) {
            string directoryPath = Path.Combine([
                paths.Input,
                ..subPaths
            ]);
            string[] childFiles = Directory.GetFiles(directoryPath);
            string[] childDirectories = Directory.GetDirectories(directoryPath);
 
            foreach (string childFilePath in childFiles) {
                if (FileExtension.IsSupportedDocumentExtension(childFilePath)) {
                    string outputPath = Path.Combine([
                        paths.Output,
                        ..subPaths,
                        Path.GetFileNameWithoutExtension(childFilePath)
                    ]);
                    Console.WriteLine(outputPath);
                    directoryFiles.Add(new() {
                        Output = outputPath,
                        Input = childFilePath,
                        OutputExtension = paths.OutputExtension,
                    });
                }
            }

            foreach (string subDirectory in childDirectories) {
                string subDirectoryName = Path.GetFileName(subDirectory);
                string outputPath = Path.Combine([
                    paths.Output,
                    ..subPaths,
                    subDirectoryName
                ]);
                Directory.CreateDirectory(outputPath);
                findDirectoryDocumentsRecursive([..subPaths, subDirectoryName]);
            }
        }
        findDirectoryDocumentsRecursive([]);
        
        await Parallel.ForEachAsync(directoryFiles, parallelOptions, async (fileIOPaths, _) => {
            string? outputPath = await ShaderizeDocument(parameters, shaderConfig, fileIOPaths, shaderProcessor);
            Console.WriteLine(outputPath != null 
                ? $"Shaderized {fileIOPaths.Input} to {outputPath}." 
                : $"Export failed of shaderized document from {outputPath}."
            );
        });

    }
    /// <summary>
    /// Applies the shader to the markdown file at the input path
    /// The resulting exported file will be at the output path
    /// </summary>
    /// <param name="parameters">Parameters for shaderizing the document</param>
    /// <param name="shaderConfig">Shader configuration parameters</param>
    /// <param name="paths">IOPaths object for the input and output paths</param>
    /// <param name="shaderProcessor">The shader processor to use [defaults to new ShaderProcessor()]</param>
    /// <returns>Task for when it finishes</returns>
    static async Task<string?> ShaderizeDocument(ShaderizeDocumentParameters parameters, ShaderConfig shaderConfig, IOPaths paths, ShaderProcessor? shaderProcessor = null) {
        string? markdown = File.ReadAllText(paths.Input);

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        string? html = Markdown.ToHtml(markdown, pipeline);

        var renderer = new HtmlShaderRenderer(shaderProcessor ?? new ShaderProcessor());

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

        return await AnimatedExporter.ExportAnimatedAsync(documentFrames, parameters.FPS, paths.Output, paths.OutputExtension);
    }

}


public class IOPaths {
    public required string Input { get; init; }
    public required string Output { get; init; }
    public required AnimatedFileExtension OutputExtension { get; init; } 
}