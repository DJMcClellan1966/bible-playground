using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces
{
    public interface IRetrievalService
    {
        Task<IEnumerable<KnowledgeDocument>> RetrieveAsync(string query, int maxResults = 5);
        Task IndexAsync(IEnumerable<KnowledgeDocument> docs);
    }
}
