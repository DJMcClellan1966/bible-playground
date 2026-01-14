using AI_Bible_App.Core.Interfaces;
using System.IO.Compression;
using System.Text;

namespace AI_Bible_App.Infrastructure.Services
{
    public class SimpleContextCompressor : IContextCompressor
    {
        public string Compress(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            using var ms = new MemoryStream();
            using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true))
            {
                ds.Write(bytes, 0, bytes.Length);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decompress(string compressed)
        {
            var bytes = Convert.FromBase64String(compressed);
            using var inMs = new MemoryStream(bytes);
            using var ds = new DeflateStream(inMs, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            ds.CopyTo(outMs);
            return Encoding.UTF8.GetString(outMs.ToArray());
        }
    }
}
