namespace ShaderMarkdown.Config;

public class ShaderizeDocumentParameters {
    public class IOPaths {
        public string Input { get; init; } = "";
        public string Output { get; init; } = "";
    }

    public class RenderSettings
    {
        public class DocumentSize
        {
            public int Width { get; init; } = 800;
            public int Height { get; init; } = 100;
        }

        public DocumentSize DocSize { get; init; } = new();
    }

    public IOPaths Paths { get; init; } = new();

    public RenderSettings DocRenderSettings { get; init; } = new();

    public float Duration = 1.0f;
    public float Scale = 1.0f;
    public int FPS = 6;
}
