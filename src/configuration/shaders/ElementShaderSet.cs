using System.Text.Json;
using ShaderMarkdown.Rendering;

namespace ShaderMarkdown.Config;
public class SerializablePageShaders : Dictionary<string, ElementShaderSet> {}
public class ElementShaderSet {
    public SerializableShaderInfo? Content { get; set; }
    public SerializableShaderInfo? Background { get; set; }
}