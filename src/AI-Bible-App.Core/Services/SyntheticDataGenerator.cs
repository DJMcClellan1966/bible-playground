using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Services;

/// <summary>
/// Generates synthetic training conversations for model improvement
/// </summary>
public interface ISyntheticDataGenerator
{
    Task<List<TrainingConversation>> GenerateSyntheticConversationsAsync(
        BiblicalCharacter character, 
        int count, 
        CancellationToken cancellationToken = default);
}

public class SyntheticDataGenerator : ISyntheticDataGenerator
{
    private readonly IAIService _aiService;
    private readonly ICharacterRepository _characterRepository;

    // Common topics users ask about
    private static readonly string[] Topics = new[]
    {
        "dealing with fear and anxiety",
        "overcoming guilt and shame",
        "handling betrayal by friends",
        "finding purpose in suffering",
        "leadership challenges",
        "making difficult decisions",
        "family conflicts",
        "faith during hardship",
        "temptation and sin",
        "forgiveness and reconciliation",
        "grief and loss",
        "courage in the face of opposition",
        "patience and waiting on God",
        "jealousy and comparison",
        "pride and humility",
        "trusting God's timing",
        "dealing with failure",
        "parenting struggles",
        "marriage difficulties",
        "workplace ethics",
        "financial worries",
        "doubt and questioning",
        "loneliness and isolation",
        "anger management",
        "seeking wisdom"
    };

    public SyntheticDataGenerator(IAIService aiService, ICharacterRepository characterRepository)
    {
        _aiService = aiService;
        _characterRepository = characterRepository;
    }

    public async Task<List<TrainingConversation>> GenerateSyntheticConversationsAsync(
        BiblicalCharacter character,
        int count,
        CancellationToken cancellationToken = default)
    {
        var conversations = new List<TrainingConversation>();
        var random = new Random();

        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            var topic = Topics[random.Next(Topics.Length)];
            var conversation = await GenerateSingleConversationAsync(character, topic, cancellationToken);
            
            if (conversation != null)
            {
                conversations.Add(conversation);
            }

            // Avoid rate limiting - add small delay
            if (i < count - 1)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        return conversations;
    }

    private async Task<TrainingConversation?> GenerateSingleConversationAsync(
        BiblicalCharacter character,
        string topic,
        CancellationToken cancellationToken)
    {
        try
        {
            // Generate a realistic user question about the topic
            var questionPrompt = $@"Generate a realistic, heartfelt question that someone might ask about: {topic}
The question should be:
- Conversational and natural
- Show genuine struggle or concern
- Be 1-2 sentences
- NOT use overly formal or religious language

Just return the question, nothing else.";

            var userQuestion = await _aiService.GeneratePrayerAsync(questionPrompt, cancellationToken);
            userQuestion = userQuestion.Trim().Trim('"');

            // Get character's response
            var conversationHistory = new List<ChatMessage>();
            var characterResponse = await _aiService.GetChatResponseAsync(
                character, 
                conversationHistory, 
                userQuestion, 
                cancellationToken);

            // Create training conversation
            var trainingConversation = new TrainingConversation
            {
                CharacterId = character.Id,
                CharacterName = character.Name,
                Topic = topic,
                Source = ConversationSource.SyntheticGenerated,
                Messages = new List<TrainingMessage>
                {
                    new TrainingMessage
                    {
                        Role = "user",
                        Content = userQuestion
                    },
                    new TrainingMessage
                    {
                        Role = "assistant",
                        Content = characterResponse
                    }
                },
                Tags = new List<string> { topic },
                QualityScore = 0.5 // Default - would need evaluation
            };

            return trainingConversation;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
