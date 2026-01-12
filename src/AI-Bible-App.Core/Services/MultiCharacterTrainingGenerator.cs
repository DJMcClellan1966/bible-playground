using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Services;

/// <summary>
/// Generates training data for multi-character conversations (roundtables)
/// Avoids echo chamber by using real user questions to anchor discussions
/// </summary>
public interface IMultiCharacterTrainingGenerator
{
    Task<List<TrainingConversation>> GenerateRoundtableConversationsAsync(
        List<BiblicalCharacter> characters,
        int count,
        CancellationToken cancellationToken = default);
}

public class MultiCharacterTrainingGenerator : IMultiCharacterTrainingGenerator
{
    private readonly IAIService _aiService;
    private readonly IUserQuestionCollector _questionCollector;

    // Real user questions that would benefit from multiple perspectives
    private static readonly string[] RoundtableQuestions = new[]
    {
        // Complex theological questions
        "Is suffering punishment from God or just part of life? I'm confused.",
        "How do we balance grace and truth when dealing with sin?",
        "What does it really mean to have faith? I feel like everyone defines it differently.",
        "How do you reconcile God's sovereignty with human free will?",
        
        // Practical dilemmas needing multiple viewpoints
        "Should I confront someone who hurt me or just forgive and move on?",
        "How do you know when to wait on God versus when to take action?",
        "Is it wrong to set boundaries with toxic family members?",
        "Can you be angry and still be faithful? I'm struggling with both.",
        
        // Leadership and community
        "How do you lead when people disagree with your decisions?",
        "What's the difference between being humble and being a doormat?",
        "How do you handle conflict in community without causing division?",
        "When should a leader step down? How do you know?",
        
        // Different life experiences provide unique answers
        "How do you handle success without pride? Everyone has advice but what actually works?",
        "What helped you most when going through grief? I need multiple perspectives.",
        "How do different people hear from God? Maybe I'm missing something.",
        "What does faithfulness look like in different seasons of life?",
        
        // Questions of calling and purpose
        "How did you each discover your calling? Was it obvious or gradual?",
        "Can you serve God in 'ordinary' work or do you need to be in ministry?",
        "What if your calling costs you everything? How do you know it's worth it?",
        "How do you know if you're being faithful or just stubborn?",
        
        // Relationships and forgiveness
        "How do you forgive someone who keeps hurting you? Different situations, different answers?",
        "What's the difference between forgiveness and reconciliation?",
        "How do you love difficult people? Everyone says 'just love them' but how?",
        "Can relationships be restored after deep betrayal? What does that look like?",
        
        // Faith in hardship
        "Where was God when each of you suffered? I need to hear different stories.",
        "How do you praise God when life is falling apart? That feels fake to me.",
        "What keeps you faithful when prayers aren't answered? I'm losing hope.",
        "Is it okay to be angry at God? Different people seem to have different views on this.",
        
        // Wisdom and decision-making
        "How do you make wise decisions when the 'right' choice isn't clear?",
        "What's the role of emotions in decision-making? Should we trust feelings?",
        "How do you discern between fear and wisdom when making choices?",
        "When do you seek counsel and when do you trust your own judgment?",
        
        // Spiritual warfare and temptation
        "How does each of you fight temptation? What strategies actually work?",
        "Is every struggle spiritual warfare or are some just consequences?",
        "How do you break patterns of sin that keep repeating?",
        "What's the difference between testing and temptation?",
        
        // Identity and worth
        "How did you find your identity in God instead of accomplishments?",
        "What does it mean to be 'chosen' by God? That concept confuses me.",
        "How do you handle feeling ordinary when God seems to use others more?",
        "Can you explain worth versus value versus purpose? I'm mixing them up.",
        
        // Justice and mercy
        "How do you balance justice and mercy in real situations?",
        "When should you stand up for yourself versus turning the other cheek?",
        "Is it wrong to want justice for wrongs done to you?",
        "How do you fight injustice without becoming bitter?",
        
        // Prayer and intimacy with God
        "How do each of you pray? Maybe I'm doing it wrong.",
        "What does intimacy with God actually feel like? I don't think I have it.",
        "How do you hear God's voice? Everyone describes it differently.",
        "Is worship just singing or something more? I'm confused about this.",
        
        // Failure and redemption
        "How did you recover from major failures? What worked for each of you?",
        "Can God still use you after you've blown it? I feel disqualified.",
        "How do you rebuild trust after failing people?",
        "What's the difference between consequences and punishment from God?",
        
        // Marriage and singleness
        "What advice would each of you give about marriage based on your experience?",
        "How do you stay content when your relationship status isn't what you wanted?",
        "Is singleness really a gift or just something we say? Be honest.",
        "How do you love your spouse when you don't feel like it?",
        
        // Parenting and legacy
        "What's the most important thing to pass on to the next generation?",
        "How do you parent with grace without being permissive?",
        "What do you do when your children reject your values?",
        "How do you leave a godly legacy in a broken world?"
    };

    public MultiCharacterTrainingGenerator(
        IAIService aiService,
        IUserQuestionCollector questionCollector)
    {
        _aiService = aiService;
        _questionCollector = questionCollector;
    }

    public async Task<List<TrainingConversation>> GenerateRoundtableConversationsAsync(
        List<BiblicalCharacter> characters,
        int count,
        CancellationToken cancellationToken = default)
    {
        var conversations = new List<TrainingConversation>();
        
        // Get real user questions if available
        var userQuestions = await _questionCollector.GetUniqueQuestionsAsync(minOccurrences: 3);
        
        // Combine real user questions with curated roundtable questions
        var allQuestions = userQuestions.Concat(RoundtableQuestions).ToList();
        var random = new Random();

        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            var question = allQuestions[random.Next(allQuestions.Count)];
            var conversation = await GenerateRoundtableDiscussionAsync(
                characters,
                question,
                cancellationToken);

            if (conversation != null)
            {
                conversations.Add(conversation);
            }

            // Rate limiting
            await Task.Delay(500, cancellationToken); // Longer delay for multi-character
        }

        return conversations;
    }

    private async Task<TrainingConversation?> GenerateRoundtableDiscussionAsync(
        List<BiblicalCharacter> characters,
        string userQuestion,
        CancellationToken cancellationToken)
    {
        try
        {
            var messages = new List<TrainingMessage>
            {
                new TrainingMessage
                {
                    Role = "user",
                    Content = userQuestion
                }
            };

            // Each character responds in turn (2-3 rounds)
            var conversationHistory = new List<ChatMessage>();
            
            for (int round = 0; round < 2; round++)
            {
                foreach (var character in characters)
                {
                    // Get character's response with awareness of previous speakers
                    var prompt = round == 0 
                        ? userQuestion 
                        : $"{userQuestion}\n\n[Build on what others have shared, don't just repeat.]";

                    var response = await _aiService.GetChatResponseAsync(
                        character,
                        conversationHistory,
                        prompt,
                        cancellationToken);

                    // Add to history
                    var characterMessage = new ChatMessage
                    {
                        Role = "assistant",
                        Content = $"{character.Name}: {response}",
                        CharacterId = character.Id
                    };
                    conversationHistory.Add(characterMessage);

                    // Add to training messages
                    messages.Add(new TrainingMessage
                    {
                        Role = "assistant",
                        Content = $"**{character.Name}**: {response}"
                    });

                    await Task.Delay(100, cancellationToken);
                }
            }

            return new TrainingConversation
            {
                CharacterId = "roundtable",
                CharacterName = string.Join(", ", characters.Select(c => c.Name)),
                Topic = "multi-character discussion",
                Source = ConversationSource.SyntheticGenerated,
                Messages = messages,
                Tags = new List<string> { "roundtable", "multiple-perspectives" },
                QualityScore = 0.5
            };
        }
        catch (Exception)
        {
            return null;
        }
    }
}
