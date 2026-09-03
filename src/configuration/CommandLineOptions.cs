using System.CommandLine;

public class CommandLineOptions {
    const int DEFAULT_WIDTH = 800;
    const int DEFAULT_HEIGHT = 0;
    const int DEFAULT_FPS = 10;
    const float DEFAULT_SCALE = 2f;
    const float DEFAULT_DURATION = 1f;
    public required FileInfo Input { get; init; }
    public required FileInfo Config { get; init; }
    public required FileInfo Output { get; init; }

    public int Width { get; init; } = 800;
    public int Height { get; init; } = 100;
    public int FPS { get; init; } = 10;

    public float Scale { get; init; } = 2.0f;
    public float Duration { get; init; } = 2.0f;

    public bool ReverseLoopFromEnd { get; init; }

    public static CommandLineOptions? ParseCommandLineArgs(string[] args) {
        if (args.Contains("--help") || args.Contains("-h") || args.Contains("-?")) {
            Console.WriteLine(GetHelpDocumentation());
            return null;
        }
        var inputArgument = new Argument<FileInfo>("input") {
            Description = "Path to the Markdown input document.",
        };

        var configOption = new Option<FileInfo>("--config") {
            Description = "Path to the shader configuration YAML file.",
            Required = true,
        };
        configOption.Aliases.Add("-c");

        var outputOption = new Option<FileInfo?>("--output") {
            Description = "Path to the output document.",
            Required = true,
        };
        outputOption.Aliases.Add("-o");

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
            DefaultValueFactory = _ => DEFAULT_FPS
        };

        var durationOption = new Option<float>("--duration") {
            Description = "Animation duration in seconds.",
            DefaultValueFactory = _ => DEFAULT_DURATION
        };

        var reverseOption = new Option<bool>("--reverseloop") {
            Description = "Reverse the animation after ending for seamless looping."
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

        var parseResult = rootCommand.Parse(args);

        if (parseResult.Errors.Count > 0)  {
            throw new ArgumentException(string.Join(Environment.NewLine, parseResult.Errors.Select(e => e.Message)));
        }

        return new CommandLineOptions {
            Input = parseResult.GetValue(inputArgument)!,
            Config = parseResult.GetValue(configOption)!,
            Output = parseResult.GetValue(outputOption)!,
            Width = parseResult.GetValue(widthOption),
            Height = parseResult.GetValue(heightOption),
            FPS = parseResult.GetValue(fpsOption),
            Scale = parseResult.GetValue(scaleOption),
            Duration = parseResult.GetValue(durationOption),
            ReverseLoopFromEnd = parseResult.GetValue(reverseOption)
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
            -h, --help              Show help and usage information.
        """;
    }
}
