using System.Text.Json;

namespace ShaderMarkdown.Rendering;

public class ShaderInfo {
    public string ShaderPath { get; init; } = "";
    public ShaderParameters ShaderParameters { get; set; } = new();


    private ShaderInfo(string shaderPath) {
        ShaderPath = shaderPath;
    }
    public ShaderInfo(string shaderPath, ShaderParameters shaderParameters) : this(shaderPath) {
        ShaderParameters = shaderParameters;
    }

    public ShaderInfo WithShaderParameters(ShaderParameters shaderParameters) {
        return new (ShaderPath, shaderParameters);
    }

    public static ShaderInfo FromShaderFileName(string shadersRootDirectory, string shaderName) {
        return new (Path.Combine(shadersRootDirectory, $"{shaderName}"));
    }

    public static ShaderInfo FromShaderFileName(string shadersRootDirectory, string shaderName, ShaderParameters shaderParameters) {
        return new (Path.Combine(shadersRootDirectory, $"{shaderName}"), shaderParameters);
    }
}

public class SerializableShaderInfo {
    public string? ShaderPath { get; init; }
    public Dictionary<string, JsonElement>? ShaderParameters { get; init; }

    public ShaderInfo ToShaderInfo(string shadersRootDirectory) {
        return new ShaderInfo(
            ShaderPath?.Trim().Length > 0
                ? Path.Combine(shadersRootDirectory, $"{ShaderPath}")
                : "", 
            Rendering.ShaderParameters.ParseShaderParameters(ShaderParameters)
        );
    }

    public bool IsValid() {
        return !string.IsNullOrWhiteSpace(ShaderPath);
    }
};