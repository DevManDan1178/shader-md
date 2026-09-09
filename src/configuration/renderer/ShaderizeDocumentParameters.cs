using ShaderMarkdown.Files;

namespace ShaderMarkdown.Config;

public class ShaderizeDocumentParameters {
    public class RenderSettings {
        public class DocumentSize {
            public required int Width { get; init; }
            public required int Height { get; init; }
        }

        public required DocumentSize DocSize { get; init; }
    }

    public required RenderSettings DocRenderSettings { get; init; }
    public required string BackgroundColor { get; init; }
    public required float Duration { get; init; } 
    public required float Scale { get; init; } 
    public required int FPS { get; init; } 
    public required bool ReverseLoopFromEnd { get; init; }
}
