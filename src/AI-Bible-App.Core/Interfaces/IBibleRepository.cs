using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Interface for accessing and searching Bible text
/// </summary>
public interface IBibleRepository
{
    /// <summary>
    /// Load all Bible verses from the data source
    /// </summary>
    Task<List<BibleVerse>> LoadAllVersesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get verses by book, chapter, and optional verse range
    /// </summary>
    Task<List<BibleVerse>> GetVersesAsync(
        string book, 
        int chapter, 
        int? startVerse = null, 
        int? endVerse = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for verses containing specific text
    /// </summary>
    Task<List<BibleVerse>> SearchVersesAsync(
        string searchText, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for RAG-based Bible verse retrieval using semantic search
/// </summary>
public interface IBibleRAGService
{
    /// <summary>
    /// Initialize the RAG service by loading and indexing Bible verses
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieve relevant Bible verses for a given query using semantic search.
    /// Falls back to keyword search if embeddings fail or return no results.
    /// </summary>
    /// <param name="query">The query to search for</param>
    /// <param name="limit">Maximum number of verses to return</param>
    /// <param name="minRelevanceScore">Minimum relevance score (0.0-1.0)</param>
    /// <param name="strictness">Search strictness: Strict (semantic only), Balanced (semantic + fallback), Relaxed (lower thresholds)</param>
    Task<List<BibleChunk>> RetrieveRelevantVersesAsync(
        string query, 
        int limit = 5,
        double minRelevanceScore = 0.7,
        SearchStrictness strictness = SearchStrictness.Balanced,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if the RAG service is initialized
    /// </summary>
    bool IsInitialized { get; }
    
    /// <summary>
    /// Get statistics about the last search operation
    /// </summary>
    SearchStatistics LastSearchStats { get; }
}

/// <summary>
/// Search strictness levels for RAG retrieval
/// </summary>
public enum SearchStrictness
{
    /// <summary>Only semantic/embedding search, no fallback</summary>
    Strict,
    /// <summary>Semantic search with keyword fallback (default)</summary>
    Balanced,
    /// <summary>Lower thresholds, more results, keyword boosting</summary>
    Relaxed
}

/// <summary>
/// Statistics about the last search operation
/// </summary>
public class SearchStatistics
{
    public string Query { get; set; } = "";
    public int TotalResults { get; set; }
    public int SemanticResults { get; set; }
    public int KeywordFallbackResults { get; set; }
    public double HighestScore { get; set; }
    public double LowestScore { get; set; }
    public TimeSpan SearchDuration { get; set; }
    public bool UsedFallback { get; set; }
    public SearchStrictness StrictnessUsed { get; set; }
}
