using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Service for managing multi-character chat sessions
/// </summary>
public interface IMultiCharacterChatService
{
    /// <summary>
    /// Get responses from multiple characters in roundtable format
    /// </summary>
    Task<List<ChatMessage>> GetRoundtableResponsesAsync(
        List<BiblicalCharacter> characters,
        List<ChatMessage> conversationHistory,
        string userMessage,
        bool enableDevilsAdvocate = false,
        string advocateTone = "soft",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get responses from multiple characters for Wisdom Council (all respond to same question)
    /// </summary>
    Task<List<ChatMessage>> GetWisdomCouncilResponsesAsync(
        List<BiblicalCharacter> characters,
        string question,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get sequential prayer responses from multiple characters
    /// </summary>
    Task<List<ChatMessage>> GetPrayerChainResponsesAsync(
        List<BiblicalCharacter> characters,
        string prayerTopic,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream responses from multiple characters in roundtable format
    /// </summary>
    IAsyncEnumerable<(string CharacterId, string Token)> StreamRoundtableResponsesAsync(
        List<BiblicalCharacter> characters,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Start a dynamic discussion where characters talk to each other
    /// </summary>
    IAsyncEnumerable<DiscussionUpdate> StartDynamicDiscussionAsync(
        List<BiblicalCharacter> characters,
        List<ChatMessage> conversationHistory,
        string topic,
        DiscussionSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Continue discussion with user input
    /// </summary>
    Task<ChatMessage> AddUserInputToDiscussionAsync(
        string userInput,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Continue an ongoing discussion after user input
    /// </summary>
    IAsyncEnumerable<DiscussionUpdate> ContinueDiscussionAsync(
        string userInput,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Settings for dynamic discussions
/// </summary>
public class DiscussionSettings
{
    /// <summary>
    /// Maximum number of turns before asking for user input or concluding
    /// </summary>
    public int MaxTurnsBeforeCheck { get; set; } = 4;

    /// <summary>
    /// Maximum total turns in the discussion
    /// </summary>
    public int MaxTotalTurns { get; set; } = 12;

    /// <summary>
    /// Whether to seek consensus
    /// </summary>
    public bool SeekConsensus { get; set; } = true;

    /// <summary>
    /// Whether to prompt user for input during discussion
    /// </summary>
    public bool AllowUserInterjection { get; set; } = true;
}

/// <summary>
/// Update from an ongoing discussion
/// </summary>
public class DiscussionUpdate
{
    /// <summary>
    /// Type of update
    /// </summary>
    public DiscussionUpdateType Type { get; set; }

    /// <summary>
    /// The message if this is a character response
    /// </summary>
    public ChatMessage? Message { get; set; }

    /// <summary>
    /// Status message for UI
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Whether discussion is waiting for user input
    /// </summary>
    public bool WaitingForUserInput { get; set; }

    /// <summary>
    /// Suggested prompt for user input
    /// </summary>
    public string? UserInputPrompt { get; set; }

    /// <summary>
    /// Discussion outcome if concluded
    /// </summary>
    public DiscussionOutcome? Outcome { get; set; }
}

public enum DiscussionUpdateType
{
    CharacterSpeaking,
    CharacterResponse,
    StatusUpdate,
    RequestingUserInput,
    ConsensusReached,
    NoConsensus,
    DiscussionComplete
}

public enum DiscussionOutcome
{
    Consensus,
    PartialAgreement,
    AgreeToDisagree,
    UserConcluded,
    MaxTurnsReached
}
