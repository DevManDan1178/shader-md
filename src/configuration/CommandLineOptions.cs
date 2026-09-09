using System.CommandLine;
using ShaderMarkdown.Files;

public class CommandLineOptions {
    const int DEFAULT_WIDTH = 1000;
    const int DEFAULT_HEIGHT = 0;
    const int DEFAULT_FPS = 5;
    const int DEFAULT_VERTICAL_SLICE_COUNT = 1;
    const float DEFAULT_SCALE = 1f;
    const float DEFAULT_DURATION = 1f;
    const string DEFAULT_BACKGROUND_COLOR = "#0d1117";
    const AnimatedFileExtension DEFAULT_OUTPUT_EXTENSION = AnimatedFileExtension.WEBP;
    const bool DEFAULT_REVERSE_LOOP_FROM_END = false;
    const bool DEFAULT_OVERWRITE_EXISTING_FILE = false;
    public required FileSystemInfo Input { get; init; }
    public required FileInfo ShaderConfig { get; init; }
    public required string Output { get; init; }

    public string BackgroundColor { get; init; } = DEFAULT_BACKGROUND_COLOR;
    public float Scale { get; init; } = DEFAULT_SCALE;
    public float Duration { get; init; } = DEFAULT_DURATION;
    public int Width { get; init; } = DEFAULT_WIDTH;
    public int Height { get; init; } = DEFAULT_HEIGHT;
    public int FPS { get; init; } = DEFAULT_FPS;
    public int VerticalSliceCount { get; init; } = DEFAULT_VERTICAL_SLICE_COUNT;
    public AnimatedFileExtension OutputExtension { get; init; } = DEFAULT_OUTPUT_EXTENSION;
    public bool ReverseLoopFromEnd { get; init; } = DEFAULT_REVERSE_LOOP_FROM_END;
    public bool OverwriteExistingFile { get; init; } = DEFAULT_OVERWRITE_EXISTING_FILE;

    public static CommandLineOptions? ParseCommandLineArgs(string[] args) {
        if (args.Contains("--help") || args.Contains("-h") || args.Contains("-?")) {
            Console.WriteLine(GetHelpDocumentation());
            return null;
        }
        var inputArgument = new Argument<FileSystemInfo>("input") {
            Description = "Path to the Markdown input document.",
        };

        var configOption = new Option<FileInfo>("--config") {
            Description = "Path to the shader configuration YAML file.",
            Required = true,
            Aliases = {"-c"},
        };

        var outputOption = new Option<string>("--output") {
            Description = "Path to the output document.",
            Required = true,
            Aliases = {"-o"},
        };

        var outputExtensionOption = new Option<AnimatedFileExtension>("--outputext") {
            Description ="File extension of the outputted shaderized documents.",
            CustomParser = result => {
                var fileName = result.Tokens.Single().Value;
                return FileExtension.GetAnimatedFileExtension(fileName) ?? 
                    throw new ArgumentException($"Unsupported animated file extension: {fileName}");
            },
            DefaultValueFactory = _ => DEFAULT_OUTPUT_EXTENSION
        };

        var backgroundColorOption = new Option<string>("--bgcolor") {
            Description = "Document background color in hex format (\"#xxxxxx\" or \"transparent\")",
            DefaultValueFactory = _ => DEFAULT_BACKGROUND_COLOR,
        };

        var widthOption = new Option<int>("--width") {
            Description = "Document width in pixels.",
            DefaultValueFactory = _ => DEFAULT_WIDTH
        };

        var heightOption = new Option<int>("--height") {
            Description = "Document height in pixels.",
            DefaultValueFactory = _ => DEFAULT_HEIGHT
        };

        var fpsOption = new Option<int>("--fps") {
            Description = "Frames per second.",
            DefaultValueFactory = _ => DEFAULT_FPS
        };

        var scaleOption = new Option<float>("--scale") {
            Description = "Render scale.",
            DefaultValueFactory = _ => DEFAULT_SCALE
        };

        var durationOption = new Option<float>("--duration") {
            Description = "Animation duration in seconds.",
            DefaultValueFactory = _ => DEFAULT_DURATION
        };

        var verticalSliceOption = new Option<int>("--vslices") {
            Description = "Amount of vertical slices to separate the output to",
            DefaultValueFactory = _ => DEFAULT_VERTICAL_SLICE_COUNT
        };

        var reverseOption = new Option<bool>("--reverseloop") {
            Description = "Reverse the animation after ending for seamless looping.",
            DefaultValueFactory = _ => DEFAULT_REVERSE_LOOP_FROM_END
        };

        var overwriteExistingOption = new Option<bool>("--oef") {
            Description = "Overwrites the existing file at the output location if it exists.",
            DefaultValueFactory = _ => DEFAULT_OVERWRITE_EXISTING_FILE,
        };

        var rootCommand = new RootCommand("Converts a Markdown document into a shaderized document.");

        rootCommand.Arguments.Add(inputArgument);
        rootCommand.Options.Add(configOption);
        rootCommand.Options.Add(outputOption);
        rootCommand.Options.Add(widthOption);
        rootCommand.Options.Add(heightOption);
        rootCommand.Options.Add(fpsOption);
        rootCommand.Options.Add(scaleOption);
        rootCommand.Options.Add(durationOption);
        rootCommand.Options.Add(reverseOption);
        rootCommand.Options.Add(outputExtensionOption);
        rootCommand.Options.Add(backgroundColorOption);
        rootCommand.Options.Add(verticalSliceOption);
        rootCommand.Options.Add(overwriteExistingOption);

        var parseResult = rootCommand.Parse(args);

        if (parseResult.Errors.Count > 0)  {
            throw new ArgumentException(string.Join(Environment.NewLine, parseResult.Errors.Select(e => e.Message)));
        }

        return new CommandLineOptions {
            Input = parseResult.GetValue(inputArgument)!,
            ShaderConfig = parseResult.GetValue(configOption)!,
            Output = parseResult.GetValue(outputOption)!,
            Width = parseResult.GetValue(widthOption),
            Height = parseResult.GetValue(heightOption),
            FPS = parseResult.GetValue(fpsOption),
            Scale = parseResult.GetValue(scaleOption),
            Duration = parseResult.GetValue(durationOption),
            ReverseLoopFromEnd = parseResult.GetValue(reverseOption),
            OutputExtension = parseResult.GetValue(outputExtensionOption),
            VerticalSliceCount = parseResult.GetValue(verticalSliceOption),
            OverwriteExistingFile = parseResult.GetValue(overwriteExistingOption),
            BackgroundColor = parseResult.GetValue(backgroundColorOption) ?? DEFAULT_BACKGROUND_COLOR,
        };
    }

    public static string GetHelpDocumentation()
    {
        return $"""
            Usage:
            
            shader-md <document path> --config <config path> --output <output path> [options]

            Options:
            -c, --config <file>     Path to the shader configuration YAML file. [required]
            -o, --output <file>     Path to the output document. [required]
                --width <value>     Document width in pixels. [default: {DEFAULT_WIDTH}]
                --height <value>    Document height in pixels. [default: {DEFAULT_HEIGHT}]
                --fps <value>       Frames per second. [default: {DEFAULT_FPS}]
                --scale <value>     Render scale. [default: {DEFAULT_SCALE}]
                --duration <value>  Animation duration in seconds. [default: {DEFAULT_DURATION}]
                --reverseloop       Reverse the animation between bounds for seamless looping.
                --bgcolor <value>   Background color of the document in hex format ({"\"#xxxxxx\" or \"transparent\""}) [default: {DEFAULT_BACKGROUND_COLOR}]
                --vslices <value>   The ammount of vertical slices to divide the output into. If over 1, documents export to a directory containing each slice. [default: {DEFAULT_VERTICAL_SLICE_COUNT}]
                --outputext <value> File extension of the outputted shaderized documents [default: {DEFAULT_OUTPUT_EXTENSION}]
                --oef                Overwrites the existing file at the output location if it exists. When disabled, a file at the output path halts the process. 

            -h, --help              Show help and usage information.
        """;
    }
}
