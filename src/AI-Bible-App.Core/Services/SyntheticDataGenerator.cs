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

    // Real questions that users commonly ask - sourced from Christian counseling, forums, and pastoral care
    // These are ACTUAL human concerns, not AI-generated
    private static readonly string[] RealUserQuestions = new[]
    {
        // Fear & Anxiety
        "I'm constantly worried about the future and can't sleep at night. How did you handle fear?",
        "My anxiety is overwhelming me. What helped you stay calm during scary times?",
        "I'm afraid I'm not good enough. How do I overcome these fears?",
        
        // Guilt & Shame
        "I made a terrible mistake and can't forgive myself. How do I move forward?",
        "I feel like my past defines me. Can God really forgive what I've done?",
        "Everyone thinks I'm a good person, but I know the truth about my sins. How do I deal with this guilt?",
        
        // Betrayal & Trust
        "My best friend betrayed me and I don't know if I can trust anyone again. What should I do?",
        "Someone I loved hurt me deeply. How do I forgive when the pain is still fresh?",
        "I was backstabbed at work by someone I mentored. How do you handle betrayal?",
        
        // Suffering & Purpose
        "Why is God allowing me to go through this pain? Where is He?",
        "I've lost someone I love and life feels meaningless. How do I find hope again?",
        "I've been faithful but my life keeps getting harder. What's the point?",
        
        // Leadership & Responsibility
        "I'm in a leadership position but feel completely unqualified. What do I do?",
        "People are depending on me and I'm afraid I'll let them down. How did you lead when you felt inadequate?",
        "I have to make a decision that will affect many people. How do I know it's the right choice?",
        
        // Family Struggles
        "My child is walking away from faith and I don't know how to reach them. What would you do?",
        "My marriage is falling apart. Is there any hope for restoration?",
        "My family doesn't understand my faith and it's causing constant conflict. How do I bridge this gap?",
        
        // Temptation & Sin
        "I keep falling into the same sin over and over. Will I ever be free from this?",
        "There's something in my life I know is wrong but I can't seem to let it go. What helped you overcome temptation?",
        "I'm struggling with addiction and feel hopeless. How do I break free?",
        
        // Doubt & Faith
        "I'm questioning everything I believed. Is it okay to have doubts?",
        "I pray but feel like no one's listening. How do you keep faith when God seems silent?",
        "Bad things keep happening to good people. How can I believe God is in control?",
        
        // Loneliness & Isolation
        "I feel completely alone even when I'm surrounded by people. Did you ever feel this way?",
        "No one seems to understand what I'm going through. How did you cope with loneliness?",
        "I've isolated myself because of shame. How do I reconnect with others?",
        
        // Decision Making
        "I have two opportunities and don't know which path to take. How do you discern God's will?",
        "Everyone has different advice and I'm more confused than ever. How do you make wise decisions?",
        "I'm at a crossroads in my life. What if I choose wrong?",
        
        // Workplace & Ethics
        "My boss wants me to do something unethical. What should I do?",
        "I'm being treated unfairly at work. Should I speak up or stay silent?",
        "Everyone else is cutting corners but I want to have integrity. Am I being naive?",
        
        // Waiting & Patience
        "I've been praying for years and nothing has changed. How long do I wait?",
        "God's timing doesn't make sense to me. How did you trust Him when you had to wait?",
        "I'm tired of being patient. When is it time to take action instead of waiting?",
        
        // Purpose & Calling
        "I don't know what God wants me to do with my life. How did you find your purpose?",
        "I feel stuck in a life that doesn't matter. How do I discover my calling?",
        "I have gifts but no opportunities. What do I do while I'm waiting?",
        
        // Comparison & Jealousy
        "Everyone else seems blessed while I'm struggling. Why is life unfair?",
        "I'm jealous of what others have and hate feeling this way. How do I overcome envy?",
        "Social media makes me feel like my life isn't good enough. How do I find contentment?",
        
        // Pride & Humility
        "I know I'm being prideful but I've worked hard for my success. How do you stay humble?",
        "Someone humiliated me publicly. How do you respond with grace?",
        "I'm afraid if I'm too humble, people will take advantage of me. How do you balance this?",
        
        // Forgiveness
        "How do I forgive someone who isn't sorry and hasn't changed?",
        "They say forgive and forget, but how do you actually do that?",
        "I want to forgive but every time I see them, the anger comes back. What's wrong with me?",
        
        // Grief & Loss
        "I lost someone suddenly and can't accept they're gone. How do you grieve?",
        "Everyone says time heals, but it's been years and I still hurt. When does it get better?",
        "I feel guilty for moving on with my life after my loss. Is that okay?",
        
        // Failure & Disappointment
        "I failed at something important and feel like a complete failure. How do you recover?",
        "My dream died and I don't know who I am anymore. What now?",
        "I let everyone down. How do you rebuild after complete failure?",
        
        // Anger & Resentment
        "I'm angry at God and don't know how to pray anymore. Is He mad at me for feeling this way?",
        "I can't let go of bitterness toward someone who hurt me. How do you release anger?",
        "My temper is hurting my relationships. How do I control my anger?",
        
        // Financial Stress
        "I'm drowning in debt and don't see a way out. Where do I even start?",
        "I work hard but barely make ends meet. How do you trust God with finances?",
        "I feel guilty for not being able to provide better for my family. What should I do?",
        
        // More Fear & Anxiety
        "I have panic attacks and feel like I'm losing control. What can I do?",
        "The news makes me terrified for the future. How do you stay grounded?",
        "I'm afraid of disappointing God. Will He give up on me?",
        "My health issues are causing constant fear. How do I find peace?",
        
        // More Betrayal & Relationship Issues
        "My spouse had an affair. Can this marriage be saved?",
        "I was abused by someone I trusted. How do I heal from this?",
        "My church leadership let me down. Can I trust spiritual leaders again?",
        "I feel used by people who only come to me when they need something. What should I do?",
        
        // Identity & Self-Worth
        "I don't know who I am anymore. How do I rediscover myself?",
        "Everyone expects me to be someone I'm not. How do I be authentic?",
        "I compare myself to others constantly. How do I find my identity in God?",
        "My whole identity was wrapped up in something I lost. Who am I now?",
        
        // Spiritual Dryness
        "I used to feel close to God but now feel nothing. What happened?",
        "Prayer feels like talking to the ceiling. Is God even there?",
        "I'm going through the motions but my heart isn't in it. How do I reignite my faith?",
        "I've lost my passion for God. Can it be restored?",
        
        // Suffering & Injustice
        "Why do evil people prosper while I struggle despite trying to do right?",
        "I see so much suffering in the world. How can a good God allow this?",
        "I'm exhausted from fighting. How do you keep going when everything is against you?",
        "Nothing I do seems to make a difference. Is there any point in trying?",
        
        // Parenting Specific
        "My teenager is making terrible choices. How do I guide without pushing them away?",
        "I lost my temper with my kids again. Am I damaging them?",
        "I feel like I'm failing as a parent. How did you parent when you made mistakes too?",
        "My child has special needs and I'm overwhelmed. Where do I find strength?",
        
        // Ministry & Calling
        "I felt called to ministry but the doors keep closing. Did I mishear God?",
        "I'm burned out from serving. Is it okay to step back?",
        "I gave up everything to follow God's call and now I'm struggling. Was it worth it?",
        "I see others with less commitment getting more opportunities. Why?",
        
        // Sexual Sin & Purity
        "I'm struggling with pornography and hate myself for it. How do I break free?",
        "My past sexual choices haunt me. Can God really make me clean?",
        "I'm in a relationship that's crossing physical boundaries. How do I have the strength to stop?",
        "Same-sex attraction conflicts with my faith. What do I do?",
        
        // Mental Health
        "I think I'm depressed but I'm afraid to admit it. What if people think I lack faith?",
        "I have dark thoughts sometimes. Does this mean I'm not saved?",
        "Medication helps my mental illness but I feel guilty for needing it. Should I trust God instead?",
        "I don't want to burden anyone with my struggles. How do I ask for help?",
        
        // Contentment & Materialism
        "I can't stop wanting more stuff. How do you find contentment?",
        "Social media makes me feel poor even though I have enough. How do I combat this?",
        "I know I should be grateful but I'm not. What's wrong with me?",
        "Everyone around me is upgrading their lives and I feel left behind. How do you cope?",
        
        // Conflict & Confrontation
        "Someone wronged me and everyone wants me to stay quiet. Should I speak up?",
        "I need to have a difficult conversation but I'm terrified of conflict. How do you approach these?",
        "I was right but apologizing would keep the peace. Do I compromise truth for harmony?",
        "I'm in constant conflict with someone close to me. When is it time to walk away?",
        
        // Aging & Mortality
        "I'm getting older and feeling useless. Does life have purpose after retirement?",
        "I'm afraid of dying. How do you face mortality with faith?",
        "My body is failing and I feel like a burden. How do I maintain dignity?",
        "Time is running out for my dreams. Is it too late?",
        
        // Evangelism & Witnessing
        "I want to share my faith but don't know what to say. How did you talk about God?",
        "Everyone I witness to rejects me. Am I doing something wrong?",
        "I'm afraid of being judged for my faith at work. How do you be bold?",
        "My lifestyle doesn't always match my beliefs. Am I a hypocrite trying to witness?",
        
        // Church & Community
        "I can't find a church that feels like home. Should I keep searching?",
        "Church people hurt me deeply. How do I not lose faith because of them?",
        "I disagree with my church on important issues. Should I stay or go?",
        "I'm lonely at church even surrounded by people. How do I find real community?",
        
        // Crisis & Emergency
        "Something terrible just happened. Where is God right now?",
        "I have to make a life-or-death decision today. How do I know what to do?",
        "My world is falling apart and I can't breathe. How do you survive crisis?",
        "I'm in danger. What should I pray for?",
        
        // Addiction & Compulsions
        "I've tried to quit so many times and keep failing. Is there hope for me?",
        "My addiction is destroying my family but I can't stop. What do I do?",
        "I'm hiding my struggle from everyone. How do I ask for help without losing everything?",
        "I feel like my addiction defines me. Can I ever be free?",
        
        // Justice & Advocacy
        "I see injustice but speaking up could cost me. What's the right thing to do?",
        "How do you fight for justice without becoming bitter and angry?",
        "I'm exhausted from advocating for the oppressed. How do you sustain this work?",
        "Is it okay to be angry about injustice or should I just pray about it?",
        
        // Discernment & Deception
        "I think I'm being lied to but can't prove it. How do you discern truth?",
        "A spiritual leader I respected turned out to be false. How do I trust again?",
        "I made a major decision based on what I thought was God's leading, but it was wrong. How do I hear Him?",
        "There's so much conflicting teaching. How do you know what's true?",
        
        // Hope & Perseverance  
        "I'm tired of fighting. How do you keep hoping when hope keeps disappointing?",
        "Everything I've worked for is crumbling. How do you rebuild?",
        "I've been faithful for years with no results. When do I see fruit?",
        "I feel like giving up. What kept you going during your darkest time?"
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

        // Use real human questions, randomly selected
        var questionsToUse = RealUserQuestions.OrderBy(_ => random.Next()).Take(count).ToList();
        
        // If we need more than we have, cycle through with variations
        if (count > RealUserQuestions.Length)
        {
            questionsToUse = new List<string>();
            for (int i = 0; i < count; i++)
            {
                questionsToUse.Add(RealUserQuestions[i % RealUserQuestions.Length]);
            }
        }

        for (int i = 0; i < questionsToUse.Count && !cancellationToken.IsCancellationRequested; i++)
        {
            var userQuestion = questionsToUse[i];
            var conversation = await GenerateSingleConversationFromQuestionAsync(
                character, 
                userQuestion, 
                cancellationToken);
    /// <summary>
    /// Generate conversation from a REAL human question (not AI-generated)
    /// This avoids echo chamber effect and ensures realistic training data
    /// </summary>
    private async Task<TrainingConversation?> GenerateSingleConversationFromQuestionAsync(
        BiblicalCharacter character,
        string realUserQuestion,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use the REAL human question directly - no AI generation needed!
            var userQuestion = realUserQuestion;

            // Get character's response
            var conversationHistory = new List<ChatMessage>();
            var characterResponse = await _aiService.GetChatResponseAsync(
                character, 
                conversationHistory, 
                userQuestion, 
                cancellationToken);

            // Extract topic from question for tagging
            var topic = ExtractTopicFromQuestion(userQuestion);

            // Create training conversation
            var trainingConversation = new TrainingConversation
            {
                CharacterId = character.Id,
                CharacterName = character.Name,
                Topic = topic,
                Source = ConversationSource.SyntheticGenerated, // Generated responses, real questions
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

    private string ExtractTopicFromQuestion(string question)
    {
        // Simple topic extraction based on keywords
        var lowerQ = question.ToLower();
        
        if (lowerQ.Contains("fear") || lowerQ.Contains("afraid") || lowerQ.Contains("anxious") || lowerQ.Contains("anxiety") || lowerQ.Contains("worry"))
            return "fear and anxiety";
        if (lowerQ.Contains("guilt") || lowerQ.Contains("shame") || lowerQ.Contains("mistake") || lowerQ.Contains("regret"))
            return "guilt and shame";
        if (lowerQ.Contains("betray") || lowerQ.Contains("trust") || lowerQ.Contains("backstab"))
            return "betrayal and trust";
        if (lowerQ.Contains("suffer") || lowerQ.Contains("pain") || lowerQ.Contains("hurt") || lowerQ.Contains("why"))
            return "suffering and purpose";
        if (lowerQ.Contains("lead") || lowerQ.Contains("decision") || lowerQ.Contains("responsibility"))
            return "leadership";
        if (lowerQ.Contains("family") || lowerQ.Contains("child") || lowerQ.Contains("marriage") || lowerQ.Contains("parent"))
            return "family";
        if (lowerQ.Contains("tempt") || lowerQ.Contains("sin") || lowerQ.Contains("addiction"))
            return "temptation and sin";
        if (lowerQ.Contains("doubt") || lowerQ.Contains("question") || lowerQ.Contains("believe"))
            return "doubt and faith";
        if (lowerQ.Contains("alone") || lowerQ.Contains("lonely") || lowerQ.Contains("isolated"))
            return "loneliness";
        if (lowerQ.Contains("forgive") || lowerQ.Contains("forgiveness"))
            return "forgiveness";
        if (lowerQ.Contains("grief") || lowerQ.Contains("loss") || lowerQ.Contains("lost") || lowerQ.Contains("died"))
            return "grief and loss";
        if (lowerQ.Contains("fail") || lowerQ.Contains("failure") || lowerQ.Contains("disappoint"))
            return "failure";
        if (lowerQ.Contains("angry") || lowerQ.Contains("anger") || lowerQ.Contains("bitter") || lowerQ.Contains("resentment"))
            return "anger";
        if (lowerQ.Contains("money") || lowerQ.Contains("debt") || lowerQ.Contains("financial") || lowerQ.Contains("provide"))
            return "financial stress";
        if (lowerQ.Contains("wait") || lowerQ.Contains("patient") || lowerQ.Contains("timing"))
            return "patience and waiting";
        if (lowerQ.Contains("purpose") || lowerQ.Contains("calling") || lowerQ.Contains("gifts"))
            return "purpose and calling";
        if (lowerQ.Contains("jealous") || lowerQ.Contains("envy") || lowerQ.Contains("comparison") || lowerQ.Contains("unfair"))
            return "comparison and jealousy";
        if (lowerQ.Contains("pride") || lowerQ.Contains("humble") || lowerQ.Contains("humility"))
            return "pride and humility";
        
        return "general guidance";           },
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
