using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Advanced multi-character debate engine with semantic validation,
/// dynamic role assignment, and intelligent response generation.
/// Now integrates with CharacterIntelligenceService for evolving character personalities.
/// </summary>
public class MultiCharacterChatService : IMultiCharacterChatService
{
    private readonly IAIService _aiService;
    private readonly ICharacterRepository _characterRepository;
    private readonly CharacterIntelligenceService? _intelligenceService;
    
    // Track the current question (updated when user asks something new)
    private string _currentQuestion = "";
    private string _originalTopic = "";
    private int _questionNumber = 0;
    
    // Memory tokens to ensure unique responses - tracks what each character has already said
    private Dictionary<string, List<string>> _characterMemory = new();
    
    // Advanced debate tracking
    private Dictionary<string, List<string>> _extractedClaims = new(); // Key claims made by each character
    private Dictionary<string, string> _characterStances = new(); // Current position on the topic
    private List<string> _debateHighlights = new(); // Key moments worth referencing
    private int _turnsSinceLastUserInput = 0;
    
    // Debate roles for dynamic assignment
    private enum DebateRole
    {
        Initiator,      // First to speak - sets the tone
        Challenger,     // Pushes back on previous claims
        Supporter,      // Builds on and strengthens a point
        Questioner,     // Asks probing questions
        Synthesizer,    // Finds common ground or deeper truth
        Devil_Advocate, // Takes contrary position for depth
        Witness,        // Shares personal experience as evidence
        Prophet         // Brings scriptural authority/warning
    }

    public MultiCharacterChatService(
        IAIService aiService,
        ICharacterRepository characterRepository,
        CharacterIntelligenceService? intelligenceService = null)
    {
        _aiService = aiService;
        _characterRepository = characterRepository;
        _intelligenceService = intelligenceService;
    }

    public async Task<List<ChatMessage>> GetRoundtableResponsesAsync(
        List<BiblicalCharacter> characters,
        List<ChatMessage> conversationHistory,
        string userMessage,
        bool enableDevilsAdvocate = false,
        string advocateTone = "soft",
        CancellationToken cancellationToken = default)
    {
        var responses = new List<ChatMessage>();

        // Add user message to history
        var userMsg = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = "user",
            Content = userMessage,
            Timestamp = DateTime.UtcNow
        };
        responses.Add(userMsg);

        // Get response from each character in turn
        var updatedHistory = new List<ChatMessage>(conversationHistory) { userMsg };
        
        // If devil's advocate is enabled but no character is explicitly marked, choose the first available
        string? forcedDevilId = null;
        if (enableDevilsAdvocate && !characters.Any(c => c.IsContrarian))
        {
            forcedDevilId = characters.FirstOrDefault()?.Id;
        }

        foreach (var character in characters)
        {
            try
            {
                // Build a prompt; if this character is acting as devil's advocate, add instruction
                var prompt = userMessage;
                var isAdvocate = enableDevilsAdvocate && (character.IsContrarian || character.Id == forcedDevilId);
                if (isAdvocate)
                {
                    // Use character's system prompt as base and instruct contrarian stance
                    var briefProfile = !string.IsNullOrEmpty(character.SystemPrompt)
                        ? character.SystemPrompt
                        : character.Description;
                    prompt = $"[Devil's Advocate - Tone: {advocateTone}] Using the voice and background: {briefProfile}. " +
                             "Take a reasoned opposing view to the user's point. Raise 2 objections and ask 2 probing questions. Stay in-character and avoid inflammatory language. " +
                             $"Topic: {userMessage}";
                }

                var response = await _aiService.GetChatResponseAsync(
                    character,
                    updatedHistory,
                    prompt,
                    cancellationToken);

                var assistantMsg = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Role = "assistant",
                    Content = response,
                    Timestamp = DateTime.UtcNow,
                    CharacterId = character.Id
                };

                responses.Add(assistantMsg);
                updatedHistory.Add(assistantMsg);
            }
            catch (Exception ex)
            {
                // Log error but continue with other characters
                var errorMsg = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Role = "assistant",
                    Content = $"[Error getting response from {character.Name}: {ex.Message}]",
                    Timestamp = DateTime.UtcNow,
                    CharacterId = character.Id
                };
                responses.Add(errorMsg);
            }
        }

        return responses;
    }

    public async Task<List<ChatMessage>> GetWisdomCouncilResponsesAsync(
        List<BiblicalCharacter> characters,
        string question,
        CancellationToken cancellationToken = default)
    {
        var responses = new List<ChatMessage>();

        // Add user question
        var userMsg = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = "user",
            Content = question,
            Timestamp = DateTime.UtcNow
        };
        responses.Add(userMsg);

        // Get responses from all characters in parallel (they all respond to the same question)
        var tasks = characters.Select(async character =>
        {
            try
            {
                // Create a context-aware prompt for Wisdom Council
                var councilPrompt = $"You are part of a Wisdom Council with {string.Join(", ", characters.Select(c => c.Name))}. " +
                    $"Each of you is being asked the same question. Provide your unique perspective based on your biblical experiences.\n\n" +
                    $"Question: {question}";

                var response = await _aiService.GetChatResponseAsync(
                    character,
                    new List<ChatMessage> { userMsg },
                    councilPrompt,
                    cancellationToken);

                return new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Role = "assistant",
                    Content = response,
                    Timestamp = DateTime.UtcNow,
                    CharacterId = character.Id
                };
            }
            catch (Exception ex)
            {
                return new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Role = "assistant",
                    Content = $"[Error getting response from {character.Name}: {ex.Message}]",
                    Timestamp = DateTime.UtcNow,
                    CharacterId = character.Id
                };
            }
        });

        var characterResponses = await Task.WhenAll(tasks);
        responses.AddRange(characterResponses);

        return responses;
    }

    public async Task<List<ChatMessage>> GetPrayerChainResponsesAsync(
        List<BiblicalCharacter> characters,
        string prayerTopic,
        CancellationToken cancellationToken = default)
    {
        var responses = new List<ChatMessage>();

        // Add user request
        var userMsg = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = "user",
            Content = $"Lead us in prayer about: {prayerTopic}",
            Timestamp = DateTime.UtcNow
        };
        responses.Add(userMsg);

        // Get prayers from each character in sequence
        var prayerHistory = new List<ChatMessage> { userMsg };
        
        foreach (var character in characters)
        {
            try
            {
                // Build context about previous prayers
                var previousPrayers = prayerHistory
                    .Where(m => m.Role == "assistant")
                    .Select(m => $"{m.CharacterId} prayed: {m.Content}")
                    .ToList();

                var prayerPrompt = previousPrayers.Any()
                    ? $"You are part of a prayer chain with {string.Join(", ", characters.Select(c => c.Name))}. " +
                      $"Previous prayers:\n{string.Join("\n\n", previousPrayers)}\n\n" +
                      $"Now continue the prayer chain by offering your own prayer about: {prayerTopic}"
                    : $"You are starting a prayer chain about: {prayerTopic}. " +
                      $"Offer a prayer in your characteristic style, knowing that {string.Join(", ", characters.Skip(1).Select(c => c.Name))} will pray after you.";

                var response = await _aiService.GetChatResponseAsync(
                    character,
                    prayerHistory,
                    prayerPrompt,
                    cancellationToken);

                var prayerMsg = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Role = "assistant",
                    Content = response,
                    Timestamp = DateTime.UtcNow,
                    CharacterId = character.Id
                };

                responses.Add(prayerMsg);
                prayerHistory.Add(prayerMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Role = "assistant",
                    Content = $"[Error getting prayer from {character.Name}: {ex.Message}]",
                    Timestamp = DateTime.UtcNow,
                    CharacterId = character.Id
                };
                responses.Add(errorMsg);
            }
        }

        return responses;
    }

    public async IAsyncEnumerable<(string CharacterId, string Token)> StreamRoundtableResponsesAsync(
        List<BiblicalCharacter> characters,
        List<ChatMessage> conversationHistory,
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Add user message to history
        var updatedHistory = new List<ChatMessage>(conversationHistory)
        {
            new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "user",
                Content = userMessage,
                Timestamp = DateTime.UtcNow
            }
        };

        // Stream response from each character in turn
        foreach (var character in characters)
        {
            var fullResponse = "";
            
            await foreach (var token in _aiService.StreamChatResponseAsync(
                character,
                updatedHistory,
                userMessage,
                cancellationToken))
            {
                fullResponse += token;
                yield return (character.Id, token);
            }

            // Add completed response to history for next character
            // Add completed response to history for next character
            updatedHistory.Add(new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "assistant",
                Content = fullResponse,
                Timestamp = DateTime.UtcNow,
                CharacterId = character.Id
            });
        }
    }

    // State for ongoing discussions
    private List<BiblicalCharacter>? _discussionCharacters;
    private List<ChatMessage>? _discussionHistory;
    private DiscussionSettings? _discussionSettings;
    private int _currentTurnCount;
    private string? _discussionTopic;

    public async IAsyncEnumerable<DiscussionUpdate> StartDynamicDiscussionAsync(
        List<BiblicalCharacter> characters,
        List<ChatMessage> conversationHistory,
        string topic,
        DiscussionSettings settings,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _discussionCharacters = characters;
        _discussionHistory = new List<ChatMessage>(conversationHistory);
        _discussionSettings = settings;
        _currentTurnCount = 0;
        _discussionTopic = topic;
        
        // Initialize question tracking
        _originalTopic = topic;
        _currentQuestion = topic;
        _questionNumber = 1;
        _turnsSinceLastUserInput = 0;
        
        // Clear ALL tracking for fresh discussion
        _characterMemory.Clear();
        _extractedClaims.Clear();
        _characterStances.Clear();
        _debateHighlights.Clear();

        // Add the user's question
        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = "user",
            Content = topic,
            Timestamp = DateTime.UtcNow
        };
        _discussionHistory.Add(userMessage);

        yield return new DiscussionUpdate
        {
            Type = DiscussionUpdateType.CharacterResponse,
            Message = userMessage
        };

        // Get initial responses from all characters
        yield return new DiscussionUpdate
        {
            Type = DiscussionUpdateType.StatusUpdate,
            StatusMessage = "Characters are gathering their initial thoughts..."
        };

        // First round - everyone responds to the topic
        foreach (var character in characters)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.CharacterSpeaking,
                StatusMessage = $"{character.Name} is sharing their perspective..."
            };

            var initialPrompt = BuildInitialDiscussionPrompt(character, characters, topic);
            var response = await GetCharacterDiscussionResponseAsync(character, initialPrompt, cancellationToken);
            
            _discussionHistory.Add(response);
            _currentTurnCount++;

            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.CharacterResponse,
                Message = response
            };
        }

        // Continue discussion rounds
        while (_currentTurnCount < settings.MaxTotalTurns && !cancellationToken.IsCancellationRequested)
        {
            // Check if we should ask for user input
            if (settings.AllowUserInterjection && 
                _currentTurnCount > 0 && 
                _currentTurnCount % settings.MaxTurnsBeforeCheck == 0)
            {
                yield return new DiscussionUpdate
                {
                    Type = DiscussionUpdateType.RequestingUserInput,
                    WaitingForUserInput = true,
                    UserInputPrompt = "Would you like to add something to the discussion, or should they continue? (Type your thoughts, 'continue', or 'conclude')",
                    StatusMessage = "The characters pause, looking to you for input..."
                };
                yield break; // Wait for user to continue via AddUserInputToDiscussionAsync
            }

            // Pick next speaker based on conversation flow
            var (nextSpeaker, responseType) = DetermineNextSpeaker(characters, _discussionHistory);
            
            if (nextSpeaker == null)
            {
                // Discussion has naturally concluded
                var outcome = AnalyzeDiscussionOutcome(_discussionHistory, characters);
                yield return new DiscussionUpdate
                {
                    Type = outcome == DiscussionOutcome.Consensus 
                        ? DiscussionUpdateType.ConsensusReached 
                        : DiscussionUpdateType.NoConsensus,
                    Outcome = outcome,
                    StatusMessage = GetOutcomeMessage(outcome)
                };
                yield break;
            }

            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.CharacterSpeaking,
                StatusMessage = $"{nextSpeaker.Name} wants to respond..."
            };

            var discussionPrompt = BuildDiscussionPrompt(nextSpeaker, characters, _discussionHistory, responseType, settings.SeekConsensus);
            var discussionResponse = await GetCharacterDiscussionResponseAsync(nextSpeaker, discussionPrompt, cancellationToken);
            
            _discussionHistory.Add(discussionResponse);
            _currentTurnCount++;

            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.CharacterResponse,
                Message = discussionResponse
            };

            // Check for natural conclusion
            if (IsDiscussionConcluding(discussionResponse.Content))
            {
                var finalOutcome = AnalyzeDiscussionOutcome(_discussionHistory, characters);
                yield return new DiscussionUpdate
                {
                    Type = DiscussionUpdateType.DiscussionComplete,
                    Outcome = finalOutcome,
                    StatusMessage = GetOutcomeMessage(finalOutcome)
                };
                yield break;
            }
        }

        // Max turns reached
        yield return new DiscussionUpdate
        {
            Type = DiscussionUpdateType.DiscussionComplete,
            Outcome = DiscussionOutcome.MaxTurnsReached,
            StatusMessage = "The discussion has reached its natural limit. Each character has shared their perspective thoroughly."
        };
    }

    public async Task<ChatMessage> AddUserInputToDiscussionAsync(
        string userInput,
        CancellationToken cancellationToken = default)
    {
        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = "user",
            Content = userInput,
            Timestamp = DateTime.UtcNow
        };
        
        _discussionHistory?.Add(userMessage);
        return userMessage;
    }

    private string BuildInitialDiscussionPrompt(BiblicalCharacter speaker, List<BiblicalCharacter> allParticipants, string topic)
    {
        var otherParticipants = allParticipants.Where(c => c.Id != speaker.Id).ToList();
        var otherNames = otherParticipants.Select(c => c.Name);
        
        // Build context about the other participants for richer engagement
        var participantContext = string.Join("\n", otherParticipants.Select(p => 
            $"- {p.Name}: {p.Title} - known for {p.Description?.Split('.').FirstOrDefault() ?? "their faith"}"));

        return $@"You are {speaker.Name}, speaking in a roundtable discussion with these biblical figures:
{participantContext}

The question posed to the group: ""{topic}""

IMPORTANT INSTRUCTIONS:
1. Share your genuine perspective as {speaker.Name}, rooted in YOUR specific biblical experiences, trials, and revelations
2. Reference specific events from your life (your calling, struggles, victories, failures, encounters with God)
3. Speak with theological depth - draw on the wisdom God revealed to you specifically
4. Be bold in your convictions while remaining respectful of others present
5. Directly address one or more of the other participants by name - ask them a challenging question or note where you suspect they might see things differently
6. Don't be generic - be SPECIFICALLY {speaker.Name} with your unique voice and perspective

Your response should reveal deep faith AND invite genuine theological debate. End with a thought-provoking question directed at a specific participant.

Keep response to 2-3 focused paragraphs.";
    }

    /// <summary>
    /// Extracts key theological claims from a response for tracking
    /// </summary>
    private List<string> ExtractKeyClaims(string response, string characterName)
    {
        var claims = new List<string>();
        
        // Look for assertion patterns
        var assertionPatterns = new[] {
            "I believe", "I know", "God has shown", "The truth is", "We must",
            "Faith requires", "Scripture teaches", "My experience proves",
            "The Lord revealed", "I learned that", "This means"
        };
        
        var sentences = response.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var sentence in sentences)
        {
            if (assertionPatterns.Any(p => sentence.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                var claim = sentence.Trim();
                if (claim.Length > 20 && claim.Length < 200)
                    claims.Add($"{characterName}: \"{claim}\"");
            }
        }
        
        return claims.Take(3).ToList(); // Max 3 claims per response
    }

    /// <summary>
    /// Assigns a debate role based on conversation state and character
    /// </summary>
    private DebateRole AssignDebateRole(BiblicalCharacter speaker, List<BiblicalCharacter> allParticipants, 
        List<ChatMessage> history, int speakerTurnCount)
    {
        var totalTurns = history.Count(m => m.Role == "assistant");
        var lastSpeaker = history.LastOrDefault(m => m.Role == "assistant");
        
        // First speaker sets the tone
        if (totalTurns == 0 || speakerTurnCount == 0)
            return DebateRole.Initiator;
        
        // After 6+ turns, start synthesizing
        if (totalTurns >= 6 && speakerTurnCount >= 2)
            return DebateRole.Synthesizer;
        
        // Prophets and strong leaders challenge more
        var challengerTypes = new[] { "prophet", "apostle", "leader" };
        if (challengerTypes.Any(t => speaker.Title?.ToLower().Contains(t) == true))
        {
            // Alternate between challenging and witnessing
            return speakerTurnCount % 2 == 0 ? DebateRole.Challenger : DebateRole.Witness;
        }
        
        // Rotate through roles based on turn count for variety
        return (totalTurns % 5) switch
        {
            0 => DebateRole.Questioner,
            1 => DebateRole.Challenger,
            2 => DebateRole.Supporter,
            3 => DebateRole.Witness,
            _ => DebateRole.Devil_Advocate
        };
    }

    /// <summary>
    /// Gets the instruction text for a specific debate role
    /// </summary>
    private string GetDebateRoleInstruction(DebateRole role, string lastSpeakerName, string lastClaim)
    {
        return role switch
        {
            DebateRole.Initiator => 
                "🎯 ROLE: INITIATOR - You speak first. Make a bold, clear statement about this question. Plant your flag. What do YOU uniquely believe?",
            
            DebateRole.Challenger => 
                $"⚔️ ROLE: CHALLENGER - Push back on what {lastSpeakerName} said. Where are they wrong or incomplete? Say: \"I must challenge {lastSpeakerName}'s view because...\" Be respectful but firm.",
            
            DebateRole.Supporter => 
                $"🤝 ROLE: SUPPORTER - Build on {lastSpeakerName}'s point. What did they get RIGHT? Add evidence from YOUR life. Say: \"{lastSpeakerName} speaks truth - I saw this when...\"",
            
            DebateRole.Questioner => 
                $"❓ ROLE: QUESTIONER - Ask a probing question that goes DEEPER. What hasn't been addressed? Say: \"But {lastSpeakerName}, have you considered...?\" or \"What about when...?\"",
            
            DebateRole.Synthesizer => 
                "🔮 ROLE: SYNTHESIZER - You've heard multiple views. What DEEPER truth emerges? What do you ALL agree on at the core? What remains in tension?",
            
            DebateRole.Devil_Advocate => 
                "😈 ROLE: DEVIL'S ADVOCATE - Take the opposite view, even if you don't fully believe it. What would someone who disagrees say? Steelman the counterargument.",
            
            DebateRole.Witness => 
                "📜 ROLE: WITNESS - Share a SPECIFIC personal experience that relates to this question. A story only YOU can tell. Make it vivid and real.",
            
            DebateRole.Prophet => 
                "🔥 ROLE: PROPHET - Speak with authority. What does GOD say about this? Cite Scripture. Warn or encourage. Be bold.",
            
            _ => "Share your perspective on this question."
        };
    }

    /// <summary>
    /// Checks if a response is too similar to previous responses (semantic anti-repetition)
    /// </summary>
    private bool IsResponseTooSimilar(string newResponse, List<string> previousResponses)
    {
        if (!previousResponses.Any()) return false;
        
        var newWords = new HashSet<string>(
            newResponse.ToLower().Split(new[] { ' ', '.', ',', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 4) // Only meaningful words
        );
        
        foreach (var prev in previousResponses)
        {
            var prevWords = new HashSet<string>(
                prev.ToLower().Split(new[] { ' ', '.', ',', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 4)
            );
            
            // Calculate Jaccard similarity
            var intersection = newWords.Intersect(prevWords).Count();
            var union = newWords.Union(prevWords).Count();
            var similarity = union > 0 ? (double)intersection / union : 0;
            
            // If more than 50% similar, it's too repetitive
            if (similarity > 0.50)
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Validates that a response actually addresses the current question
    /// </summary>
    private bool ResponseAddressesQuestion(string response, string question)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(response))
            return true; // Can't validate empty
            
        // Extract key terms from question (more aggressive filtering)
        var stopWords = new HashSet<string> { 
            "what", "how", "why", "when", "where", "does", "the", "this", "that", 
            "about", "with", "your", "you", "tell", "can", "could", "would", "should",
            "think", "believe", "mean", "explain", "describe"
        };
        
        var questionTerms = question.ToLower()
            .Split(new[] { ' ', '.', ',', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Where(w => !stopWords.Contains(w))
            .ToHashSet();
        
        var responseTerms = response.ToLower()
            .Split(new[] { ' ', '.', ',', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet();
        
        // Need at least 25% of question key terms (was 20%)
        var overlap = questionTerms.Intersect(responseTerms).Count();
        var relevance = questionTerms.Count > 0 ? (double)overlap / questionTerms.Count : 1;
        
        // Also check if response contains the question's core subject
        var coreTerms = questionTerms.Where(t => t.Length > 5).Take(3).ToList();
        var containsCoreTerm = coreTerms.Count == 0 || coreTerms.Any(t => response.ToLower().Contains(t));
        
        return (relevance >= 0.25 && containsCoreTerm) || (relevance >= 0.4);
    }

    private string BuildDiscussionPrompt(BiblicalCharacter speaker, List<BiblicalCharacter> allParticipants, 
        List<ChatMessage> history, string responseType, bool seekConsensus)
    {
        // Find the LATEST user question (not just the original topic)
        var latestUserMessageIndex = history.FindLastIndex(m => m.Role == "user");
        var latestUserMessage = latestUserMessageIndex >= 0 ? history[latestUserMessageIndex] : null;
        var currentQuestionText = latestUserMessage?.Content ?? _currentQuestion;
        
        // CRITICAL: Only get statements made AFTER the latest user question
        // This prevents confusion when a new question is asked
        var messagesForCurrentQuestion = latestUserMessageIndex >= 0 
            ? history.Skip(latestUserMessageIndex + 1).ToList()
            : history.Where(m => m.Role == "assistant").ToList();
        
        // Get what THIS character has already said FOR THIS QUESTION ONLY
        var speakerPreviousStatements = messagesForCurrentQuestion
            .Where(m => m.CharacterId == speaker.Id)
            .Select(m => m.Content)
            .ToList();
        
        var speakerPreviousTurns = speakerPreviousStatements.Count;
        var totalTurns = messagesForCurrentQuestion.Count(m => m.Role == "assistant");
        
        // Get what OTHER characters said FOR THIS QUESTION ONLY (most recent)
        var otherRecentResponses = messagesForCurrentQuestion
            .Where(m => m.Role == "assistant" && m.CharacterId != speaker.Id)
            .TakeLast(3)
            .Select(m => {
                var name = allParticipants.FirstOrDefault(c => c.Id == m.CharacterId)?.Name ?? "Someone";
                return $"{name}: \"{m.Content}\"";
            })
            .ToList();
        
        var conversationSoFar = otherRecentResponses.Any()
            ? "RECENT STATEMENTS FROM OTHERS:\n" + string.Join("\n\n", otherRecentResponses)
            : "";
        
        // Strong anti-repetition: Show key phrases already used
        var alreadySaidPhrases = speakerPreviousStatements
            .SelectMany(s => ExtractKeyPhrases(s))
            .Distinct()
            .Take(10)
            .ToList();
        
        var alreadySaid = speakerPreviousStatements.Any()
            ? $"⚠️ YOU ALREADY SAID THESE THINGS (DO NOT REPEAT OR PARAPHRASE):\n" + 
              string.Join("\n", speakerPreviousStatements.Select((s, i) => 
                $"Turn {i+1}: \"{TruncateText(s, 150)}\"")) +
              (alreadySaidPhrases.Any() ? $"\n\n❌ BANNED PHRASES (do not use): {string.Join(", ", alreadySaidPhrases)}" : "")
            : "";

        // Determine what kind of continuation this is
        var isContinuation = speakerPreviousTurns > 0;
        var continuationDirective = "";
        
        if (isContinuation)
        {
            var lastSpeakerName = allParticipants
                .FirstOrDefault(c => c.Id == history.LastOrDefault(m => m.Role == "assistant")?.CharacterId)?.Name;
            
            continuationDirective = $@"
🔄 THIS IS A CONTINUATION - You have spoken {speakerPreviousTurns} time(s) before.

CRITICAL: You MUST say something COMPLETELY NEW. Options:
1. RESPOND directly to {lastSpeakerName ?? "the last speaker"}'s most recent point
2. CHALLENGE something specific another character said  
3. Add a NEW biblical example you haven't mentioned
4. Share a DIFFERENT aspect of your experience
5. Ask a probing question to another character

DO NOT: Summarize, repeat yourself, or restate your previous points.";
        }

        // Simple, clear prompt structure
        return $@"You are {speaker.Name}, {speaker.Title}.

QUESTION: ""{currentQuestionText}""

{conversationSoFar}

{alreadySaid}

{continuationDirective}

YOUR TASK: {(isContinuation ? "ADD SOMETHING NEW to the discussion" : "Share your initial perspective")}.

RULES:
1. Keep it to 2-3 sentences MAXIMUM
2. Be SPECIFIC - reference your actual biblical experiences  
3. {(isContinuation ? "MUST say something you haven't said before" : "Share your unique perspective")}
4. {(otherRecentResponses.Any() ? "ENGAGE with what others said - agree, disagree, or build on it" : "Open with your perspective")}

{speaker.Name}'s response:";
    }
    
    private IEnumerable<string> ExtractKeyPhrases(string text)
    {
        // Extract significant 3-4 word phrases to track for anti-repetition
        var words = text.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToArray();
        
        for (int i = 0; i < words.Length - 2; i++)
        {
            yield return $"{words[i]} {words[i+1]} {words[i+2]}".ToLower();
        }
    }
    
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }

    private (BiblicalCharacter? Speaker, string ResponseType) DetermineNextSpeaker(
        List<BiblicalCharacter> characters, List<ChatMessage> history)
    {
        var recentMessages = history.Where(m => m.Role == "assistant").TakeLast(characters.Count * 2).ToList();
        
        if (recentMessages.Count < 2) 
            return (characters.FirstOrDefault(), "Share your initial thoughts and how your life experiences speak to this question.");

        // Find who hasn't spoken recently - ensure rotation
        var recentSpeakers = recentMessages.TakeLast(characters.Count - 1)
            .Select(m => m.CharacterId)
            .ToHashSet();

        var nextSpeaker = characters.FirstOrDefault(c => !recentSpeakers.Contains(c.Id))
            ?? characters.FirstOrDefault();

        // Analyze conversation to determine the most engaging response type
        var lastMessage = recentMessages.Last();
        var lastSpeaker = characters.FirstOrDefault(c => c.Id == lastMessage.CharacterId);
        var lastContent = lastMessage.Content;
        
        // Look for specific engagement opportunities
        string responseType;
        
        // Check if someone was directly addressed or asked a question
        if (nextSpeaker != null && lastContent.Contains(nextSpeaker.Name, StringComparison.OrdinalIgnoreCase))
        {
            responseType = $"{lastSpeaker?.Name ?? "Someone"} spoke directly to you. Respond to their specific point or question with your own perspective.";
        }
        else if (lastContent.Contains("?"))
        {
            responseType = $"{lastSpeaker?.Name ?? "Someone"} raised a question. Answer from your unique experience and understanding of God's ways.";
        }
        else if (ContainsDisagreement(lastContent))
        {
            responseType = "There is tension in the perspectives shared. Either defend your position with deeper scriptural insight, or acknowledge where another's view has given you pause to reconsider.";
        }
        else if (ContainsTheologicalClaim(lastContent))
        {
            responseType = "A significant theological claim was made. Either affirm it with your own experience, challenge it with a different understanding, or deepen it with additional insight.";
        }
        else if (recentMessages.Count >= characters.Count * 2)
        {
            responseType = "The discussion has developed. Synthesize what has been said, identify the core tension or insight, and push the conversation toward deeper truth.";
        }
        else
        {
            responseType = "Build on what has been shared. What do your unique experiences reveal that hasn't been said? Where do you see deeper truth emerging?";
        }

        return (nextSpeaker, responseType);
    }

    private bool ContainsTheologicalClaim(string content)
    {
        var theologicalMarkers = new[] {
            "God's will", "the Lord", "scripture teaches", "faith requires", "sin", "grace",
            "salvation", "righteousness", "the truth is", "I believe", "God revealed",
            "covenant", "promise", "commandment", "holy", "spirit", "eternal", "judgment"
        };
        return theologicalMarkers.Any(m => content.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    private bool ContainsDisagreement(string content)
    {
        var disagreementPhrases = new[] { 
            "however", "but I", "disagree", "not sure I agree", "different perspective",
            "on the other hand", "while I respect", "must respectfully", "I must challenge",
            "I see it differently", "that troubles me", "I cannot accept", "my experience differs",
            "with respect", "I question whether", "perhaps not", "yet I wonder"
        };
        return disagreementPhrases.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsDiscussionConcluding(string content)
    {
        // Only trigger conclusion on very explicit conclusion language
        var conclusionPhrases = new[] {
            "to summarize our discussion", "in final conclusion",
            "let us conclude", "as we finish", "to bring this to a close"
        };
        return conclusionPhrases.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private DiscussionOutcome AnalyzeDiscussionOutcome(List<ChatMessage> history, List<BiblicalCharacter> characters)
    {
        var recentMessages = history.Where(m => m.Role == "assistant").TakeLast(characters.Count).ToList();
        var combinedText = string.Join(" ", recentMessages.Select(m => m.Content.ToLower()));

        if (combinedText.Contains("we all agree") || combinedText.Contains("consensus") || 
            combinedText.Contains("we seem to agree") || combinedText.Contains("we are united"))
            return DiscussionOutcome.Consensus;

        if (combinedText.Contains("agree to disagree") || combinedText.Contains("respectfully disagree") ||
            combinedText.Contains("hold different views"))
            return DiscussionOutcome.AgreeToDisagree;

        if (combinedText.Contains("common ground") || combinedText.Contains("partially agree") ||
            combinedText.Contains("share this truth"))
            return DiscussionOutcome.PartialAgreement;

        return DiscussionOutcome.PartialAgreement; // Default - most discussions find some common ground
    }

    private string GetOutcomeMessage(DiscussionOutcome outcome)
    {
        return outcome switch
        {
            DiscussionOutcome.Consensus => "🕊️ Through their dialogue, these servants of God have found unity in truth.",
            DiscussionOutcome.PartialAgreement => "✨ Iron has sharpened iron - different perspectives have illuminated deeper truths.",
            DiscussionOutcome.AgreeToDisagree => "🙏 In the spirit of Christian charity, they honor their differences while remaining united in faith.",
            DiscussionOutcome.UserConcluded => "📖 May this dialogue deepen your understanding and strengthen your faith.",
            DiscussionOutcome.MaxTurnsReached => "📜 A rich discussion has unfolded. May these perspectives guide your reflection.",
            _ => "The discussion has concluded."
        };
    }

    /// <summary>
    /// Gets a response from a character with validation and retry logic
    /// </summary>
    private async Task<ChatMessage> GetCharacterDiscussionResponseAsync(
        BiblicalCharacter character, string prompt, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        string? bestResponse = null;
        
        // CRITICAL FIX: Only get previous statements for the CURRENT question
        // Find where the current question starts in history
        var latestUserMessageIndex = _discussionHistory?.FindLastIndex(m => m.Role == "user") ?? -1;
        var messagesForCurrentQuestion = latestUserMessageIndex >= 0 && _discussionHistory != null
            ? _discussionHistory.Skip(latestUserMessageIndex + 1).ToList()
            : _discussionHistory?.Where(m => m.Role == "assistant").ToList() ?? new List<ChatMessage>();
        
        // Only check against statements made for THIS question
        var previousStatementsForThisQuestion = messagesForCurrentQuestion
            .Where(m => m.CharacterId == character.Id)
            .Select(m => m.Content)
            .ToList();
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Add stronger retry indicator if this is a retry
                var currentPrompt = attempt > 0 
                    ? prompt + $"\n\n🚨 CRITICAL RETRY #{attempt}: Your response was REJECTED. You MUST say something COMPLETELY DIFFERENT. Do NOT repeat any previous ideas. Give a FRESH perspective on the question: \"{_currentQuestion}\""
                    : prompt;
                
                // CRITICAL: Pass EMPTY history because the prompt already contains all context.
                // Passing history + prompt causes the model to see duplicate/conflicting info
                // and it responds to the history instead of our carefully crafted prompt.
                var response = await _aiService.GetChatResponseAsync(
                    character,
                    new List<ChatMessage>(), // Empty! The prompt has everything.
                    currentPrompt,
                    cancellationToken);
                
                // Validate: Check if response is too similar to what was said FOR THIS QUESTION
                if (IsResponseTooSimilar(response, previousStatementsForThisQuestion))
                {
                    System.Diagnostics.Debug.WriteLine($"[MultiChat] {character.Name} response too similar, attempt {attempt}");
                    bestResponse ??= response; // Keep as fallback
                    if (attempt < maxRetries) continue; // Try again
                }
                
                // Validate: Check if response addresses the CURRENT question (stricter check)
                if (!string.IsNullOrEmpty(_currentQuestion) && !ResponseAddressesQuestion(response, _currentQuestion))
                {
                    System.Diagnostics.Debug.WriteLine($"[MultiChat] {character.Name} response doesn't address question, attempt {attempt}");
                    bestResponse ??= response; // Keep as fallback
                    if (attempt < maxRetries) continue; // Try again
                }
                
                // Response passed validation!
                bestResponse = response;
                break;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    return new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = "assistant",
                        Content = $"[{character.Name} pauses thoughtfully...] (Error: {ex.Message})",
                        Timestamp = DateTime.UtcNow,
                        CharacterId = character.Id,
                        CharacterName = character.Name
                    };
                }
            }
        }
        
        var finalResponse = bestResponse ?? $"[{character.Name} contemplates in silence...]";
        
        // Extract claims from the response for future reference
        var claims = ExtractKeyClaims(finalResponse, character.Name);
        if (claims.Any())
        {
            if (!_extractedClaims.ContainsKey(character.Id))
                _extractedClaims[character.Id] = new List<string>();
            _extractedClaims[character.Id].AddRange(claims);
        }

        // Record this interaction in the character's evolving intelligence
        if (_intelligenceService != null)
        {
            try
            {
                var otherParticipants = _discussionCharacters?
                    .Where(c => c.Id != character.Id)
                    .Select(c => c.Name)
                    .ToList();
                    
                await _intelligenceService.RecordInteractionAsync(
                    character,
                    MemoryType.Roundtable,
                    _currentQuestion,
                    finalResponse,
                    _currentQuestion,
                    otherParticipants);
            }
            catch { /* Don't fail if intelligence recording fails */ }
        }

        return new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = "assistant",
            Content = finalResponse,
            Timestamp = DateTime.UtcNow,
            CharacterId = character.Id,
            CharacterName = character.Name
        };
    }

    /// <summary>
    /// Continue an ongoing discussion after user input
    /// </summary>
    public async IAsyncEnumerable<DiscussionUpdate> ContinueDiscussionAsync(
        string userInput,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_discussionCharacters == null || _discussionHistory == null || _discussionSettings == null)
        {
            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.DiscussionComplete,
                StatusMessage = "No active discussion to continue."
            };
            yield break;
        }

        var normalizedInput = userInput.Trim().ToLower();

        // Handle special commands
        if (normalizedInput == "conclude" || normalizedInput == "end" || normalizedInput == "stop")
        {
            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.DiscussionComplete,
                Outcome = DiscussionOutcome.UserConcluded,
                StatusMessage = "You've concluded the discussion. Thank you for guiding this conversation!"
            };
            yield break;
        }

        // Add user input to history if it's not just "continue"
        if (normalizedInput != "continue")
        {
            // This is a NEW question/comment from the user - update tracking
            _questionNumber++;
            _currentQuestion = userInput;
            
            // Clear ALL tracking for fresh responses to new question
            _characterMemory.Clear();
            _extractedClaims.Clear();
            _characterStances.Clear();
            _debateHighlights.Clear();
            _turnsSinceLastUserInput = 0;
            
            var userMessage = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "user",
                Content = userInput,
                Timestamp = DateTime.UtcNow
            };
            _discussionHistory.Add(userMessage);

            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.CharacterResponse,
                Message = userMessage
            };

            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.StatusUpdate,
                StatusMessage = $"🆕 New question #{_questionNumber} - the characters prepare fresh perspectives..."
            };
        }
        else
        {
            _turnsSinceLastUserInput++;
        }

        // Continue the discussion
        var turnsThisRound = 0;
        var maxTurnsThisRound = Math.Min(_discussionSettings.MaxTurnsBeforeCheck, 
            _discussionSettings.MaxTotalTurns - _currentTurnCount);

        while (turnsThisRound < maxTurnsThisRound && _currentTurnCount < _discussionSettings.MaxTotalTurns)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            var (nextSpeaker, responseType) = DetermineNextSpeaker(_discussionCharacters, _discussionHistory);
            
            if (nextSpeaker == null)
            {
                var outcome = AnalyzeDiscussionOutcome(_discussionHistory, _discussionCharacters);
                yield return new DiscussionUpdate
                {
                    Type = DiscussionUpdateType.DiscussionComplete,
                    Outcome = outcome,
                    StatusMessage = GetOutcomeMessage(outcome)
                };
                yield break;
            }

            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.CharacterSpeaking,
                StatusMessage = $"{nextSpeaker.Name} responds..."
            };

            var discussionPrompt = BuildDiscussionPrompt(nextSpeaker, _discussionCharacters, _discussionHistory, 
                responseType, _discussionSettings.SeekConsensus);
            var response = await GetCharacterDiscussionResponseAsync(nextSpeaker, discussionPrompt, cancellationToken);
            
            // Track what this character said in memory (for anti-repetition)
            if (!_characterMemory.ContainsKey(nextSpeaker.Id))
                _characterMemory[nextSpeaker.Id] = new List<string>();
            _characterMemory[nextSpeaker.Id].Add(response.Content);
            
            _discussionHistory.Add(response);
            _currentTurnCount++;
            turnsThisRound++;

            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.CharacterResponse,
                Message = response
            };

            if (IsDiscussionConcluding(response.Content))
            {
                var finalOutcome = AnalyzeDiscussionOutcome(_discussionHistory, _discussionCharacters);
                yield return new DiscussionUpdate
                {
                    Type = DiscussionUpdateType.DiscussionComplete,
                    Outcome = finalOutcome,
                    StatusMessage = GetOutcomeMessage(finalOutcome)
                };
                yield break;
            }
        }

        // Check if we should ask for more user input
        if (_currentTurnCount < _discussionSettings.MaxTotalTurns && _discussionSettings.AllowUserInterjection)
        {
            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.RequestingUserInput,
                WaitingForUserInput = true,
                UserInputPrompt = "Would you like to add to the discussion? (Type your thoughts, 'continue', or 'conclude')",
                StatusMessage = "The characters look to you for guidance..."
            };
        }
        else
        {
            yield return new DiscussionUpdate
            {
                Type = DiscussionUpdateType.DiscussionComplete,
                Outcome = DiscussionOutcome.MaxTurnsReached,
                StatusMessage = GetOutcomeMessage(DiscussionOutcome.MaxTurnsReached)
            };
        }
    }
}
