using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Infrastructure.Services
{
    public class InMemoryRetrievalService : IRetrievalService
    {
        private readonly List<KnowledgeDocument> _store = new();

        public Task IndexAsync(IEnumerable<KnowledgeDocument> docs)
        {
            _store.AddRange(docs);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<KnowledgeDocument>> RetrieveAsync(string query, int maxResults = 5)
        {
            // Naive contains-based ranking for scaffolding
            var results = _store
                .Where(d => d.Content != null && d.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(maxResults);
            return Task.FromResult(results);
        }
    }
}
