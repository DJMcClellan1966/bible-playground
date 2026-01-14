using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI_Bible_App.Core.Interfaces
{
    public interface IUnconsciousService
    {
        /// <summary>
        /// Prepare compact context for the given session and user input.
        /// Best-effort: may return null if nothing available or on error.
        /// </summary>
        Task<string?> PrepareContextAsync(string sessionId, string userInput, CancellationToken cancellationToken = default);

        /// <summary>
        /// Consolidate recent messages into short-term memory / records.
        /// Best-effort background operation.
        /// </summary>
        Task ConsolidateAsync(string sessionId, IEnumerable<AI_Bible_App.Core.Models.ChatMessage> recentMessages, CancellationToken cancellationToken = default);
    }
}
