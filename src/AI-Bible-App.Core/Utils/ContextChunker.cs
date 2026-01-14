using System.Collections.Generic;
using System.Text;

namespace AI_Bible_App.Core.Utils
{
    public static class ContextChunker
    {
        // Very small, deterministic chunker that splits text into chunks of roughly `maxWords` words.
        // This is a lightweight approximation for token-based chunking suitable for local/dev use.
        public static IEnumerable<string> ChunkByWords(string text, int maxWords = 200)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            var count = 0;

            foreach (var w in words)
            {
                sb.Append(w);
                sb.Append(' ');
                count++;
                if (count >= maxWords)
                {
                    yield return sb.ToString().Trim();
                    sb.Clear();
                    count = 0;
                }
            }

            if (sb.Length > 0)
                yield return sb.ToString().Trim();
        }
    }
}
