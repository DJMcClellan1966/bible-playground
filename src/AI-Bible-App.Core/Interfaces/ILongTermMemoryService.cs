using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces
{
    public interface ILongTermMemoryService
    {
        Task StoreAsync(string key, string content);
        Task<IEnumerable<MemoryRecord>> QueryAsync(string query, int max = 5);
    }
}
