using System.Text.Json;

namespace ShaderMarkdown.Rendering;

public class ShaderParameters {
    public float Time { get; set; } = 0f;
    public bool InterpolateTime { get; init; } = true; 
    public Dictionary<string, JsonElement> ShaderProperties { get; init; } = new();

    public ShaderParameters() {}
    public ShaderParameters(float time) {
        Time = time;
        InterpolateTime = false;
    }
    


    public static ShaderParameters ParseShaderParameters(string? json) {
        if (string.IsNullOrEmpty(json)) {
            return new ShaderParameters();
        }

        Dictionary<string, JsonElement>? parameters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (parameters == null) {
            return new ShaderParameters();
        }
        
        var time = parameters.GetValueOrDefault("time");
        parameters.Remove("time");


        return time.ValueKind == JsonValueKind.Number
            ?   new ((float) time.GetDouble()){
                    ShaderProperties = parameters
                }
            :   new () {
                ShaderProperties = parameters
                };
       
    }
}