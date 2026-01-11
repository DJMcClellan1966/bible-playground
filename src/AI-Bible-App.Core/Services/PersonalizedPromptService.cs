using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Core.Services;

/// <summary>
/// Service that enhances a character's system prompt with personalized user context.
/// This creates dynamic, relationship-aware prompts without modifying the AI interface.
/// </summary>
public class PersonalizedPromptService
{
    private readonly ICharacterMemoryService _memoryService;
    private readonly ILogger<PersonalizedPromptService> _logger;

    public PersonalizedPromptService(
        ICharacterMemoryService memoryService,
        ILogger<PersonalizedPromptService> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a personalized copy of a character with enhanced system prompt
    /// </summary>
    public async Task<BiblicalCharacter> GetPersonalizedCharacterAsync(
        BiblicalCharacter baseCharacter, 
        string userId)
    {
        try
        {
            var contextSummary = await _memoryService.GetContextForPromptAsync(userId, baseCharacter.Id);
            
            if (string.IsNullOrEmpty(contextSummary) || contextSummary.Contains("first conversation"))
            {
                _logger.LogDebug("No user context found for user {UserId}, character {CharacterId}", userId, baseCharacter.Id);
                return baseCharacter;
            }

            // Create a personalized copy of the character
            var personalizedCharacter = new BiblicalCharacter
            {
                Id = baseCharacter.Id,
                Name = baseCharacter.Name,
                Title = baseCharacter.Title,
                Description = baseCharacter.Description,
                Era = baseCharacter.Era,
                BiblicalReferences = baseCharacter.BiblicalReferences,
                IconFileName = baseCharacter.IconFileName,
                Voice = baseCharacter.Voice,
                PrimaryTone = baseCharacter.PrimaryTone,
                Relationships = baseCharacter.Relationships,
                PrayerStyle = baseCharacter.PrayerStyle,
                Attributes = baseCharacter.Attributes,
                SystemPrompt = EnhanceSystemPrompt(baseCharacter.SystemPrompt, contextSummary)
            };

            _logger.LogDebug("Created personalized character prompt for user {UserId}, character {CharacterId}", userId, baseCharacter.Id);
            return personalizedCharacter;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error personalizing character prompt for user {UserId}, character {CharacterId}", userId, baseCharacter.Id);
            return baseCharacter; // Fall back to base character on error
        }
    }

    /// <summary>
    /// Records an interaction and extracts insights for future conversations
    /// </summary>
    public async Task RecordInteractionAsync(
        string userId,
        string characterId,
        string userMessage,
        string characterResponse)
    {
        try
        {
            await _memoryService.RecordInteractionAsync(userId, characterId, userMessage, characterResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording interaction for user {UserId}, character {CharacterId}", userId, characterId);
            // Don't throw - this is a non-critical background operation
        }
    }

    /// <summary>
    /// Performs deeper analysis on a conversation to extract more insights
    /// </summary>
    public async Task<ConversationInsights?> AnalyzeConversationAsync(
        string userId,
        string characterId,
        List<ChatMessage> conversation)
    {
        try
        {
            return await _memoryService.ExtractAndStoreInsightsAsync(userId, characterId, conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing conversation for user {UserId}, character {CharacterId}", userId, characterId);
            return null;
        }
    }

    private string EnhanceSystemPrompt(string basePrompt, string userContext)
    {
        // Insert user context after the character identity but before instructions
        var enhancedPrompt = $@"{basePrompt}

=== YOUR RELATIONSHIP WITH THIS PERSON ===
{userContext}

=== IMPORTANT RELATIONSHIP GUIDANCE ===
- Reference what you know about this person naturally in conversation
- Build on previous conversations rather than starting fresh each time
- Remember their struggles and ask how things are going
- If they mentioned a difficult situation before, follow up with genuine care
- Adapt your communication style to what works best for them
- When suggesting scripture, consider what has resonated with them before
- Be genuinely relational - you KNOW this person";

        return enhancedPrompt;
    }
}
