using AI_Bible_App.Core.Interfaces;

namespace AI_Bible_App.Infrastructure.Services
{
    public class ResponseValidator : IResponseValidator
    {
        public bool Validate(string response, out string? reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(response))
            {
                reason = "Empty response";
                return false;
            }
            // Very simple heuristic: disallow long sequences of repeating characters
            if (response.Length > 1000 && response.Distinct().Count() < 5)
            {
                reason = "Low entropy response";
                return false;
            }
            return true;
        }
    }
}
