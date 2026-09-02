using System.Text.Json;

namespace ShaderMarkdown.Rendering;

public class ShaderParameters {
    const string TIME_PARAMETER_KEY = "time";
    const string TIMESCALE_PARAMETER_KEY = "timescale";
    const float DEFAULT_TIME_PARAMETER = 0f;
    const float DEFAULT_TIMESCALE_PARAMETER = 1.0f;
    public float Time { get; init; } = DEFAULT_TIME_PARAMETER;
    public float TimeScale { get; init;} = DEFAULT_TIMESCALE_PARAMETER; 
    public Dictionary<string, JsonElement> ShaderProperties { get; init; } = new();


    public ShaderParameters() {}
    public ShaderParameters(float time, float timeScale, Dictionary<string, JsonElement> shaderProperties) {
        Time = time;
        TimeScale = timeScale;
        ShaderProperties = shaderProperties;
    }
    
    public static ShaderParameters ParseShaderParameters(Dictionary<string, JsonElement>? parameters) {
        if (parameters == null) {
            return new ShaderParameters();
        }
        
        var timeValue = parameters.GetValueOrDefault(TIME_PARAMETER_KEY);
        parameters.Remove(TIME_PARAMETER_KEY);

        var timeScaleValue = parameters.GetValueOrDefault(TIMESCALE_PARAMETER_KEY);
        parameters.Remove(TIMESCALE_PARAMETER_KEY);

        float time = timeValue.ValueKind == JsonValueKind.Number
            ? (float) timeValue.GetDouble()
            : DEFAULT_TIME_PARAMETER;

        float timeScale = timeScaleValue.ValueKind == JsonValueKind.Number
            ? (float) timeScaleValue.GetDouble()
            : DEFAULT_TIMESCALE_PARAMETER;

        return new ShaderParameters(time, timeScale, parameters){};
    }
    public static ShaderParameters ParseShaderParameters(string? json) {
        if (string.IsNullOrEmpty(json)) {
            return new ShaderParameters();
        }

        return ParseShaderParameters(
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
        );
    }
}