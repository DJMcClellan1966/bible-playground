using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces
{
    public interface IModelOrchestrator
    {
        /// <summary>
        /// Selects the model name to use for a request based on capabilities and recommendation.
        /// </summary>
        string SelectModel(AIBackendRecommendation recommendation, double maxLatencyMs = 1000);
    }
}
