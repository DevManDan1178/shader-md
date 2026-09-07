using ShaderMarkdown.Files;

namespace ShaderMarkdown.Config;

public class ShaderizeDocumentParameters {
    public class RenderSettings {
        public class DocumentSize {
            public int Width { get; init; } = 800;
            public int Height { get; init; } = 0;
        }

        public DocumentSize DocSize { get; init; } = new();
    }

    public RenderSettings DocRenderSettings { get; init; } = new();

    public float Duration { get; init; } = 1.0f;
    public float Scale { get; init; } = 1.0f;
    public int FPS { get; init; } = 6;
    public bool ReverseLoopFromEnd { get; init; } = false;
}
