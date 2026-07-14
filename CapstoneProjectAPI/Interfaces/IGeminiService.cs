namespace CapstoneProjectAPI.Interfaces
{
    /// <summary>
    /// Abstracts the Gemini API call so DocumentService is not coupled to a concrete HTTP implementation.
    /// </summary>
    public interface IGeminiService
    {
        /// <summary>
        /// Generates a concise AI summary for the provided file content.
        /// Returns <c>null</c> if the call fails or the response is empty — callers must handle this gracefully.
        /// </summary>
        /// <param name="fileStream">The file stream to summarise.</param>
        /// <param name="mimeType">MIME type of the file (e.g. application/pdf, image/jpeg).</param>
        /// <param name="fileName">Original file name, used for logging.</param>
        Task<string?> GenerateSummaryAsync(Stream fileStream, string mimeType, string fileName);
    }
}
