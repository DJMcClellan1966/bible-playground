namespace AI_Bible_App.Core.Interfaces
{
    public interface IResponseValidator
    {
        /// <summary>
        /// Returns true if response passes basic validation (no policy violations, not empty).
        /// </summary>
        bool Validate(string response, out string? reason);
    }
}
