namespace ShaderMarkdown.FilePaths {
    class Directories {
        public readonly static string DEFAULT_SHADER_DIRECTORY = Path.Combine(AppContext.BaseDirectory, "shaders");
    }
    class WebScriptPaths {
        const string WEB_SCRIPT_PATH_ROOT = "generated";
        private static string GetWebScriptPath(string webScriptRelativePath) {
            return Path.Combine(AppContext.BaseDirectory, WEB_SCRIPT_PATH_ROOT, webScriptRelativePath);
        }
        public readonly static string DOCUMENT_FUNCTIONS = GetWebScriptPath("DocumentFunctions.js");
        public readonly static string SHADER_RENDERER = GetWebScriptPath("ShaderRenderer.js");
    }
}