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
        Console.WriteLine(options.Output);
        if (Path.Exists(options.Output)) {
            if (!options.OverwriteExistingFile) {
                throw new IOException($"File already exists at the output path and \"--oef\" (overwrite existing file) is not set.");
            }
            if (Directory.Exists(options.Output)) {
                Directory.Delete(options.Output, recursive: true);
            } else {
                File.Delete(options.Output);
            }
        }
        if (!options.ShaderConfig.Exists) {
            throw new FileNotFoundException($"Shader configuration document not found: \"{options.ShaderConfig}\".");
        }
        Console.WriteLine($"Input: {options.Input};Output: {options.Output};");
        bool shaderizingDirectory = options.Input.Attributes.HasFlag(FileAttributes.Directory);
        
        if (!shaderizingDirectory) {   
            if (!FileExtension.IsSupportedDocumentExtension(options.Input.FullName)) {
                throw new FormatException($"Document file extension is unsupported: \"{options.Input.FullName}\".");
            }
            if (options.VerticalSliceCount <= 1 && FileExtension.GetAnimatedFileExtension(options.Output) == null) {
                throw new FormatException($"Output file extension is unsupported: \"{Path.GetExtension(options.Output)}\".");
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
            ReverseLoopFromEnd = options.ReverseLoopFromEnd,
            BackgroundColor = options.BackgroundColor,
        };

        IOParameters paths = new () {
            Input = options.Input.FullName,
            Output = options.Output,
            OutputExtension = (AnimatedFileExtension) options.OutputExtension!,
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
                paths,
                options.VerticalSliceCount
            );
            Console.WriteLine($"Shaderized directory {options.Input.FullName} to {options.Output} " + $"in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
        } else {
            string? outputPath = await ShaderizeDocument(
                parameters,
                shaderConfig,
                paths,
                options.VerticalSliceCount
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
    static async Task ShaderizeDocumentDirectory(ShaderizeDocumentParameters parameters, ShaderConfig shaderConfig, IOParameters ioParams, int sliceCount) {
        
        ParallelOptions parallelOptions = new ParallelOptions {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
        
        List<IOParameters> directoryFiles = new();

        void findDirectoryDocumentsRecursive(string[] subPaths) {
            string directoryPath = Path.Combine([
                ioParams.Input,
                ..subPaths
            ]);
            string[] childFiles = Directory.GetFiles(directoryPath);
            string[] childDirectories = Directory.GetDirectories(directoryPath);
 
            foreach (string childFilePath in childFiles) {
                if (FileExtension.IsSupportedDocumentExtension(childFilePath)) {
                    string outputPath = Path.Combine([
                        ioParams.Output,
                        ..subPaths,
                        Path.GetFileNameWithoutExtension(childFilePath)
                    ]);
                    Console.WriteLine(outputPath);
                    directoryFiles.Add(new() {
                        Output = outputPath,
                        Input = childFilePath,
                        OutputExtension = ioParams.OutputExtension,
                    });
                }
            }

            foreach (string subDirectory in childDirectories) {
                string subDirectoryName = Path.GetFileName(subDirectory);
                string outputPath = Path.Combine([
                    ioParams.Output,
                    ..subPaths,
                    subDirectoryName
                ]);
                Directory.CreateDirectory(outputPath);
                findDirectoryDocumentsRecursive([..subPaths, subDirectoryName]);
            }
        }
        findDirectoryDocumentsRecursive([]);
        
        await Parallel.ForEachAsync(directoryFiles, parallelOptions, async (fileIOParams, _) => {
            // Avoid multithreading on shaders for multithreading on files instead
            string? outputPath = await ShaderizeDocument(parameters, shaderConfig, fileIOParams, sliceCount, false);
            Console.WriteLine(outputPath != null 
                ? $"Shaderized {fileIOParams.Input} to {outputPath}." 
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
    static async Task<string?> ShaderizeDocument(ShaderizeDocumentParameters parameters, ShaderConfig shaderConfig, IOParameters ioParameters, int sliceCount = 1, bool multithreadingEnabled = true) {
        string? markdown = File.ReadAllText(ioParameters.Input);

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        string? html = Markdown.ToHtml(markdown, pipeline);

        var renderer = new HtmlShaderRenderer(new ShaderProcessor(multithreadingEnabled));

        

        AnimatedFileExtension? fileExtension = FileExtension.GetAnimatedFileExtension(ioParameters.Output);
        string? animatedFileExtension = FileExtension.GetFileExtensionString(ioParameters.OutputExtension);
        // Invalid export file extension, abort (should normally never happen, since all file extensions should be covered in GetFileExtensionString)
        if (animatedFileExtension == null) {
            return null;
        }
        string outputPath = (fileExtension == ioParameters.OutputExtension || sliceCount > 1)
            ? ioParameters.Output // output path has correct file extension as is
            : (fileExtension == null
                ? $"{ioParameters.Output}.{animatedFileExtension}" // No file extension, so add it
                : Path.ChangeExtension(ioParameters.Output, animatedFileExtension) // Incorrect file extension, so replace it
            ); 

        byte[][] documentFrames = await renderer.GetShaderizedHTMLAsync(
            html: html,
            shaderConfig,
            width: parameters.DocRenderSettings.DocSize.Width,
            height: parameters.DocRenderSettings.DocSize.Height,
            fps: parameters.FPS,
            duration: parameters.Duration,
            scale: parameters.Scale,
            reverseLoopFromEnd: parameters.ReverseLoopFromEnd,
            backgroundColor: parameters.BackgroundColor
        );
        
        if (sliceCount < 2) {
            Console.WriteLine($"Exporting shaderized document.");

            await AnimatedExporter.ExportAnimatedAsync(
                documentFrames, 
                parameters.FPS, 
                outputPath, 
                ioParameters.OutputExtension
            );
        } else {
            Directory.CreateDirectory(outputPath);
            
            Console.WriteLine($"Slicing animated frames to {sliceCount} slices before export.");
            byte[][][] slicedFrames = ImageSlicer.SliceFramesVertically(documentFrames, sliceCount);
            
            int exportedCounter = 0;
            var exportTasks = Enumerable.Range(0, slicedFrames.Length).Select(async i => {
                string sliceOutputPath = Path.Combine(
                    outputPath, 
                    $"slice_{i + 1}.{animatedFileExtension}"
                );
                byte[][] slicedFrame = slicedFrames[i];
                int counter = Interlocked.Increment(ref exportedCounter);

                Console.WriteLine($"Exporting shaderized document slice: {counter}/{sliceCount}");

                await AnimatedExporter.ExportAnimatedAsync(
                    slicedFrame,    
                    parameters.FPS,
                    sliceOutputPath,
                    ioParameters.OutputExtension
                );
            });

            await Task.WhenAll(exportTasks);
        }
        return outputPath;
    }

}


public class IOParameters {
    /// <summary>
    /// Path to the input document
    /// </summary>
    public required string Input { get; init; }
    /// <summary>
    /// Path to the output
    /// </summary>
    public required string Output { get; init; }
    /// <summary>
    /// File extension of the output
    /// </summary>
    public required AnimatedFileExtension OutputExtension { get; init; } 
    /// <summary>
    /// How many vertical slices per output.
    /// Only slices if value > 1
    /// [defaults to 1]
    /// </summary>
    public int OutputVerticalSliceCount = 1;
}