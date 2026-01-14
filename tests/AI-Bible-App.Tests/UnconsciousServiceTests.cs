using System;
using System.Linq;
using System.Threading.Tasks;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Infrastructure.Services;
using Xunit;

namespace AI_Bible_App.Tests
{
    public class UnconsciousServiceTests
    {
        [Fact]
        public async Task PrepareContext_Returns_Prompt_For_Input()
        {
            var svc = new InMemoryUnconsciousService();
            var prompt = await svc.PrepareContextAsync("s1", "This is a long test input that should be chunked and produce a compact context prompt for unconscious processing.");
            Assert.False(string.IsNullOrWhiteSpace(prompt));
            Assert.Contains("UnconsciousContext", prompt);
        }

        [Fact]
        public async Task Consolidate_Persists_To_LongTerm_And_Raises_Event()
        {
            var longTerm = new InMemoryLongTermMemoryService();
            var svc = new InMemoryUnconsciousService(longTerm);

            var tcs = new TaskCompletionSource<string>();
            svc.ConsolidationCompleted += id => tcs.TrySetResult(id);

            await svc.ConsolidateAsync("s2", new[] { new AI_Bible_App.Core.Models.ChatMessage { Content = "Hello world" } });

            var signaled = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.Equal(tcs.Task, signaled);
            Assert.Equal("s2", tcs.Task.Result);

            var results = await longTerm.QueryAsync("Hello");
            Assert.NotEmpty(results);
        }
    }
}
