namespace ShaderMarkdown.Files;

public static class WebScriptPaths {
    const string WEB_SCRIPTS_PATH_ROOT = "generated";
    private static string GetWebScriptPath(string webScriptFileName, params string[] webScriptSubdirectories) {
        return Path.Combine([
            AppContext.BaseDirectory, 
            WEB_SCRIPTS_PATH_ROOT, 
            ..webScriptSubdirectories, 
            webScriptFileName
        ]);
    }
    public readonly static string DOCUMENT_FUNCTIONS = GetWebScriptPath("DocumentFunctions.js");
    public readonly static string SHADER_RENDERER = GetWebScriptPath("ShaderRenderer.js");
    public readonly static string STATIC_SHADER_RENDERER = GetWebScriptPath("StaticShaderRenderer.js");
}
