namespace ShaderMarkdown.Rendering;

public class ShaderInfo {
    public string ShaderPath { get; init; }
    public ShaderParameters ShaderParameters { get; set; } = new();

    public static string ShaderDirectory = FilePaths.Directories.DEFAULT_SHADER_DIRECTORY;
    
    private ShaderInfo(string shaderPath) {
        ShaderPath = shaderPath;
    }
    private ShaderInfo(string shaderPath, ShaderParameters shaderParameters) : this(shaderPath) {
        ShaderParameters = shaderParameters;
    }

    public ShaderInfo WithShaderParameters(ShaderParameters shaderParameters) {
        return new (ShaderPath, shaderParameters);
    }

    public static ShaderInfo FromShaderFileName(string shaderName) {
        return new (Path.Combine(ShaderDirectory, $"{shaderName}"));
    }

    public static ShaderInfo FromShaderFileName(string shaderName, ShaderParameters shaderParameters) {
        return new (Path.Combine(ShaderDirectory, $"{shaderName}"), shaderParameters);
    }
}