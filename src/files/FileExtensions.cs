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
        string extension = Path.GetExtension(fileName).TrimStart('.');
        
        return stringFileExtensions.GetValueOrDefault(extension, null);
    }
}