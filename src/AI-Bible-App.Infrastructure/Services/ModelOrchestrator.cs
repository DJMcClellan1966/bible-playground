using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Infrastructure.Services
{
    public class ModelOrchestrator : IModelOrchestrator
    {
        public string SelectModel(AIBackendRecommendation recommendation, double maxLatencyMs = 1000)
        {
            // Simple orchestration: prefer recommended model name if provided, else fallback to a default mapping
            if (!string.IsNullOrEmpty(recommendation.RecommendedModelName))
                return recommendation.RecommendedModelName;

            return recommendation.Primary switch
            {
                AIBackendType.LocalOllama => "local-ollama",
                AIBackendType.OnDevice => "on-device",
                AIBackendType.AzureOpenAI => "azure-openai",
                AIBackendType.Cloud => "cloud-model",
                _ => "default-model"
            };
        }
    }
}
