using System.Net;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder.Core;

public static class TesseractLanguageDownloader
{
    private static readonly HttpClient HttpClient = new();
    private const string RepositoryBaseUrl = "https://raw.githubusercontent.com/NoMercy-Entertainment/NoMercyTesseract/master/tessdata";
    
    static TesseractLanguageDownloader()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);
    }

    /// <summary>
    /// Ensures that the Tesseract language file for the specified language exists.
    /// Downloads it from the NoMercyTesseract repository if it doesn't exist.
    /// </summary>
    /// <param name="languageCode">The ISO 639-3 language code (e.g., "eng", "fra", "deu")</param>
    /// <returns>True if the language file exists or was successfully downloaded, false otherwise</returns>
    public static async Task<bool> EnsureLanguageFileExists(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            Logger.Encoder("Language code is null or empty", LogEventLevel.Warning);
            return false;
        }

        // Create tessdata directory if it doesn't exist
        if (!Directory.Exists(AppFiles.TesseractModelsFolder))
        {
            Directory.CreateDirectory(AppFiles.TesseractModelsFolder);
            Logger.Encoder($"Created Tesseract models directory: {AppFiles.TesseractModelsFolder}");
        }

        string languageFileName = $"{languageCode}.traineddata";
        string localFilePath = Path.Combine(AppFiles.TesseractModelsFolder, languageFileName);

        // Check if the language file already exists
        if (File.Exists(localFilePath))
        {
            Logger.Encoder($"Tesseract language file already exists: {languageFileName}", LogEventLevel.Debug);
            return true;
        }

        // Download the language file from the repository
        Logger.Encoder($"Downloading Tesseract language file: {languageFileName}");
        
        try
        {
            string downloadUrl = $"{RepositoryBaseUrl}/{languageFileName}";
            
            using HttpResponseMessage response = await HttpClient.GetAsync(downloadUrl);
            
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Logger.Encoder($"Language file not found in repository: {languageFileName}", LogEventLevel.Warning);
                return false;
            }
            
            response.EnsureSuccessStatusCode();
            
            byte[] fileData = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(localFilePath, fileData);
            
            Logger.Encoder($"Successfully downloaded Tesseract language file: {languageFileName}");
            return true;
        }
        catch (HttpRequestException ex)
        {
            Logger.Encoder($"Failed to download Tesseract language file {languageFileName}: {ex.Message}", LogEventLevel.Error);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Encoder($"Unexpected error downloading Tesseract language file {languageFileName}: {ex.Message}", LogEventLevel.Error);
            return false;
        }
    }

    /// <summary>
    /// Downloads multiple language files in parallel
    /// </summary>
    /// <param name="languageCodes">Collection of ISO 639-3 language codes</param>
    /// <returns>Dictionary indicating success/failure for each language</returns>
    public static async Task<Dictionary<string, bool>> EnsureLanguageFilesExist(IEnumerable<string> languageCodes)
    {
        var tasks = languageCodes.Select(async lang => new
        {
            Language = lang,
            Success = await EnsureLanguageFileExists(lang)
        });

        var results = await Task.WhenAll(tasks);
        
        return results.ToDictionary(r => r.Language, r => r.Success);
    }

    /// <summary>
    /// Gets a list of all available language files in the local tessdata directory
    /// </summary>
    /// <returns>List of language codes for available language files</returns>
    public static List<string> GetAvailableLanguages()
    {
        if (!Directory.Exists(AppFiles.TesseractModelsFolder))
            return new();

        return Directory.GetFiles(AppFiles.TesseractModelsFolder, "*.traineddata")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList()!;
    }
}
