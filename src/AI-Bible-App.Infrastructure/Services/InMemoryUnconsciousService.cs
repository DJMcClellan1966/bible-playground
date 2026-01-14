using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Utils;
using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Infrastructure.Services
{
    public class InMemoryUnconsciousService : IUnconsciousService
    {
        private readonly ConcurrentDictionary<string, string> _sessionPrompts = new();
        private readonly ILongTermMemoryService? _longTerm;

        public event Action<string>? ConsolidationCompleted;

        public InMemoryUnconsciousService(ILongTermMemoryService? longTerm = null)
        {
            _longTerm = longTerm;
        }

        public Task<string?> PrepareContextAsync(string sessionId, string userInput, CancellationToken cancellationToken = default)
        {
            try
            {
                var chunks = ContextChunker.ChunkByWords(userInput ?? string.Empty, 80).ToList();
                var compact = chunks.Count > 0 ? string.Join(" \n", chunks.Take(3)) : null;
                if (!string.IsNullOrWhiteSpace(compact))
                {
                    var prompt = $"[UnconsciousContext] {compact}";
                    _sessionPrompts[sessionId] = prompt;
                    return Task.FromResult<string?>(prompt);
                }

                return Task.FromResult<string?>(null);
            }
            catch
            {
                return Task.FromResult<string?>(null);
            }
        }

        public async Task ConsolidateAsync(string sessionId, IEnumerable<ChatMessage> recentMessages, CancellationToken cancellationToken = default)
        {
            try
            {
                var text = string.Join(" ", recentMessages?.Select(m => m.Content).Where(c => !string.IsNullOrWhiteSpace(c)) ?? Array.Empty<string>());
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var compact = string.Join(" ", ContextChunker.ChunkByWords(text, 100).Take(2));
                    var consolidated = $"[Consolidated] {compact}";
                    _sessionPrompts[sessionId] = consolidated;

                    if (_longTerm != null)
                    {
                        // store best-effort to long-term memory
                        await _longTerm.StoreAsync(sessionId, consolidated);
                    }

                    ConsolidationCompleted?.Invoke(sessionId);
                }
            }
            catch
            {
                // best-effort
            }
        }
    }
}
