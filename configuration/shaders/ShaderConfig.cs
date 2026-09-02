using ShaderMarkdown.FilePaths;
using ShaderMarkdown.Rendering;
using YamlDotNet.Serialization.NamingConventions;

namespace ShaderMarkdown.Config;

using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

public sealed class JsonElementYamlTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) {
        return type == typeof(JsonElement);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) {
        return ReadNode(parser);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) {
        throw new NotSupportedException();
    }

    private static JsonElement ReadNode(IParser parser) {
        if (parser.TryConsume<Scalar>(out var scalar)) {
            if (scalar.Value == null) {
                return JsonDocument.Parse("null").RootElement.Clone();
            }

            if (bool.TryParse(scalar.Value, out var boolValue)) {
                return JsonSerializer.SerializeToElement(boolValue);
            }

            if (double.TryParse(
                    scalar.Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var numberValue)
                ) {
                return JsonSerializer.SerializeToElement(numberValue);
            }

            return JsonSerializer.SerializeToElement(scalar.Value);
        }

        if (parser.TryConsume<SequenceStart>(out _)) {
            var values = new List<JsonElement>();

            while (!parser.TryConsume<SequenceEnd>(out _)) {
                values.Add(ReadNode(parser));
            }

            return JsonSerializer.SerializeToElement(values);
        }

        if (parser.TryConsume<MappingStart>(out _)) {
            var values = new Dictionary<string, JsonElement>();

            while (!parser.TryConsume<MappingEnd>(out _)) {
                var key = parser.Consume<Scalar>();
                values[key.Value] = ReadNode(parser);
            }

            return JsonSerializer.SerializeToElement(values);
        }

        throw new YamlException("Unsupported YAML node while converting to JsonElement.");
    }
}

public class ShaderConfig {
    public class DocumentShadersInfo {
        public SerializableShaderInfo? Background { get; set; }
        public SerializableShaderInfo? Finalize { get; set; }
    }
    public DocumentShadersInfo DocumentShaders { get; set; } = new();

    public Dictionary<string, ElementShaderSet> DefaultPageElementShaders { get; set; } = new();

    public string ShadersRootDirectory { get; init; } = Directories.DEFAULT_SHADER_DIRECTORY;

    public static ShaderConfig ReadFromYAML(string yamlString) {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new JsonElementYamlTypeConverter())
            .Build();

        return deserializer.Deserialize<ShaderConfig>(yamlString);
    }
}