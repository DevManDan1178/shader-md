namespace ShaderMarkdown.FilePaths {
    class Directories {
        public readonly static string DEFAULT_SHADER_DIRECTORY = Path.Combine(AppContext.BaseDirectory, "shaders");
    }
    class WebScriptPaths {
        const string WEB_SCRIPT_PATH_ROOT = "generated";
        private static string GetWebScriptPath(string webScriptRelativePath) {
            return Path.Combine(AppContext.BaseDirectory, WEB_SCRIPT_PATH_ROOT, webScriptRelativePath);
        }
        public readonly static string CREATE_DOCUMENT =  GetWebScriptPath("CreateDocument.js");
        public readonly static string RENDER_SHADER = GetWebScriptPath("RenderShader.js");
        public readonly static string CREATE_ELEMENT_BACKGROUND = GetWebScriptPath("CreateElementBackground.js");
        public readonly static string REPLACE_ELEMENT_WITH_IMAGE = GetWebScriptPath("ReplaceElementWithImage.js");
        public readonly static string CREATE_DOCUMENT_BACKGROUND = GetWebScriptPath("CreateDocumentBackground.js");
        
    }
}