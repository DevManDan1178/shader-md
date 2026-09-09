namespace ShaderMarkdown.Files;
public enum AnimatedFileExtension {
    GIF, 
    WEBP, 
    APNG, 
//  AVIF, not supported yet
}

public static class FileExtension {
    private static Dictionary<string, AnimatedFileExtension?> stringFileExtensions = new () {
        ["gif"] = AnimatedFileExtension.GIF,
        ["webp"] = AnimatedFileExtension.WEBP,
        ["apng"] = AnimatedFileExtension.APNG,
    //  ["avif"] = AnimatedFileExtension.AVIF, not supported yet
    };

    public static AnimatedFileExtension? GetAnimatedFileExtension(string fileName) {
        string extension = fileName.Contains('.') 
        ? Path.GetExtension(fileName).TrimStart('.') 
        : fileName;
        
        return stringFileExtensions.GetValueOrDefault(extension, null);
    }

    public static string? GetFileExtensionString(AnimatedFileExtension fileExtension) {
        return stringFileExtensions.FirstOrDefault(ext => ext.Value == fileExtension).Key;
    }

    public static bool IsSupportedDocumentExtension(string fileName) {
        string extension = fileName.Contains('.') 
            ? Path.GetExtension(fileName).TrimStart('.')
            : fileName;

        return extension == "md" || extension == "html";
    }
}