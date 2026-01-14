namespace AI_Bible_App.Core.Interfaces
{
    public interface IContextCompressor
    {
        string Compress(string input);
        string Decompress(string compressed);
    }
}
