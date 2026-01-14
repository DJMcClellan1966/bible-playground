using System.Threading.Tasks;
using System.Linq;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Core.Models;
using Xunit;

namespace AI_Bible_App.Tests
{
    public class InMemoryUnconsciousEdgeTests
    {
        [Fact]
        public async Task PrepareContext_EmptyInput_ReturnsNull()
        {
            var svc = new InMemoryUnconsciousService();
            var result = await svc.PrepareContextAsync("s", "");
            Assert.Null(result);
        }

        [Fact]
        public async Task Consolidate_EmptyMessages_DoesNotThrow_And_NoEvent()
        {
            var longTerm = new InMemoryLongTermMemoryService();
            var svc = new InMemoryUnconsciousService(longTerm);
            var called = false;
            svc.ConsolidationCompleted += id => called = true;

            await svc.ConsolidateAsync("s", new ChatMessage[0]);
            Assert.False(called);
        }

        [Fact]
        public async Task LongTermMemory_Query_IsCaseInsensitive()
        {
            var longTerm = new InMemoryLongTermMemoryService();
            await longTerm.StoreAsync("k", "Hello World");
            var resultsLower = await longTerm.QueryAsync("hello");
            Assert.NotEmpty(resultsLower);
            var resultsUpper = await longTerm.QueryAsync("WORLD");
            Assert.NotEmpty(resultsUpper);
        }
    }
}
