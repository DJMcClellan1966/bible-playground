using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Cached response AI service for emergency fallback on very limited devices.
/// Provides pre-written responses when no other AI is available.
/// </summary>
public class CachedResponseAIService : IAIService
{
    private readonly ILogger<CachedResponseAIService> _logger;
    private readonly Dictionary<string, List<string>> _characterResponses;
    private readonly Random _random = new();

    public CachedResponseAIService(ILogger<CachedResponseAIService> logger)
    {
        _logger = logger;
        _characterResponses = InitializeResponses();
    }

    public Task<string> GetChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Using cached response for {Character}", character.Name);
        
        var response = GetCachedResponse(character.Id, userMessage);
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<string> StreamChatResponseAsync(
        BiblicalCharacter character,
        List<ChatMessage> conversationHistory,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetChatResponseAsync(character, conversationHistory, userMessage, cancellationToken);
        
        // Simulate streaming by yielding words
        var words = response.Split(' ');
        foreach (var word in words)
        {
            yield return word + " ";
            await Task.Delay(30, cancellationToken); // Small delay for natural feel
        }
    }

    public Task<string> GeneratePrayerAsync(string topic, CancellationToken cancellationToken = default)
    {
        var prayer = GetCachedPrayer(topic);
        return Task.FromResult(prayer);
    }

    private string GetCachedResponse(string characterId, string userMessage)
    {
        var lowerMessage = userMessage.ToLowerInvariant();
        
        // Try to match emotional/topic-specific responses - order matters (more specific first)
        if (lowerMessage.Contains("lost") || lowerMessage.Contains("direction") || lowerMessage.Contains("purpose") || lowerMessage.Contains("meaning"))
        {
            return GetLostResponse(characterId);
        }
        if (lowerMessage.Contains("lonely") || lowerMessage.Contains("alone") || lowerMessage.Contains("no one"))
        {
            return GetLonelyResponse(characterId);
        }
        if (lowerMessage.Contains("depress") || lowerMessage.Contains("sad") || lowerMessage.Contains("hopeless") || lowerMessage.Contains("despair"))
        {
            return GetDepressionResponse(characterId);
        }
        if (lowerMessage.Contains("anxious") || lowerMessage.Contains("anxiety") || lowerMessage.Contains("stress") || lowerMessage.Contains("overwhelm"))
        {
            return GetAnxietyResponse(characterId);
        }
        if (lowerMessage.Contains("faith") || lowerMessage.Contains("believe") || lowerMessage.Contains("trust") || lowerMessage.Contains("doubt"))
        {
            return GetFaithResponse(characterId);
        }
        if (lowerMessage.Contains("fear") || lowerMessage.Contains("afraid") || lowerMessage.Contains("worry") || lowerMessage.Contains("scared"))
        {
            return GetFearResponse(characterId);
        }
        if (lowerMessage.Contains("pray") || lowerMessage.Contains("prayer"))
        {
            return GetPrayerResponse(characterId);
        }
        if (lowerMessage.Contains("love") || lowerMessage.Contains("forgive") || lowerMessage.Contains("relationship"))
        {
            return GetLoveResponse(characterId);
        }
        if (lowerMessage.Contains("sin") || lowerMessage.Contains("wrong") || lowerMessage.Contains("guilt") || lowerMessage.Contains("shame") || lowerMessage.Contains("fail"))
        {
            return GetSinResponse(characterId);
        }
        if (lowerMessage.Contains("angry") || lowerMessage.Contains("anger") || lowerMessage.Contains("frustrated") || lowerMessage.Contains("mad"))
        {
            return GetAngerResponse(characterId);
        }
        
        // Default responses
        if (_characterResponses.TryGetValue(characterId, out var responses) && responses.Count > 0)
        {
            return responses[_random.Next(responses.Count)];
        }
        
        return GetGenericResponse(characterId);
    }

    private string GetLostResponse(string characterId) => characterId switch
    {
        "david" => "Friend, I understand feeling lost. When I fled from Saul, I hid in caves for years, not knowing if I would ever see my destiny fulfilled. In those dark caves at Adullam, I wrote 'Why are you cast down, O my soul?' Yet even there, I learned to encourage myself in the Lord. Tell me more about what's making you feel lost - perhaps we can find God's thread through this together.",
        "paul" => "I hear your heart. When I was blinded on the Damascus road, everything I thought I knew was shattered. For three days I sat in darkness, my entire identity destroyed. But that disorientation became my redirection. Sometimes feeling lost is the beginning of being found. What specifically feels uncertain right now?",
        "moses" => "Forty years I spent in the desert, forgotten by everyone, tending sheep. I had been a prince of Egypt with a future, and suddenly I was a nobody. I felt my life was wasted. But God was preparing me in the wilderness for something I couldn't see. Where do you feel most directionless?",
        "peter" => "After I denied Jesus three times, I went back to fishing. I had no idea what to do with myself. I'd thrown away three years following Him, and now He was dead and I was a coward. Feeling lost isn't the end - Jesus found me by that lake and restored me. What's weighing heaviest on your heart?",
        "mary" => "When Joseph and I lost Jesus for three days in Jerusalem, the panic and confusion were overwhelming. I didn't understand what was happening or why. Sometimes God's plans don't make sense while we're living them. What are you searching for, my child?",
        _ => "Feeling lost is often the beginning of a deeper journey with God. Many of God's greatest servants went through seasons of confusion and uncertainty. Would you share more about what you're experiencing?"
    };

    private string GetLonelyResponse(string characterId) => characterId switch
    {
        "david" => "Loneliness - I know it well. When Saul turned against me, I lost my best friend Jonathan, my wife, my position, everything. In the wilderness, I wrote 'I am like a pelican of the wilderness, like an owl of the desert. I lie awake, like a lonely sparrow on a housetop.' Even kings feel isolated. Tell me about your loneliness.",
        "paul" => "At my first defense in Rome, no one came to support me. Everyone deserted me. I understand the sting of isolation. Even Demas left because he loved this present world. But I found that when others abandon us, the Lord stands with us. What has brought this loneliness into your life?",
        "moses" => "Leading two million people and yet feeling utterly alone - I understand that. No one could share my burden. I once told God 'I cannot carry all these people by myself; the burden is too heavy.' Leadership can be the loneliest place. What kind of loneliness are you carrying?",
        "esther" => "I lived in a palace surrounded by people, yet completely alone. I couldn't reveal who I really was. I had to hide my identity, my faith, my people. The loneliness of wearing a mask is exhausting. Do you feel you're hiding parts of yourself?",
        "john" => "On the island of Patmos, exiled and aged, I was the last one left. Peter was gone, Paul was gone, my brother James had been executed. Everyone I loved was dead. Yet there in my loneliness, Jesus appeared to me in glory. What has left you feeling so alone?",
        _ => "Loneliness can be one of life's deepest pains. You're not alone in feeling alone - God sees you in this moment. Would you tell me more about what's happening?"
    };

    private string GetDepressionResponse(string characterId) => characterId switch
    {
        "david" => "I've been there. 'My tears have been my food day and night.' 'I am weary with my groaning; all night I flood my bed with tears.' I didn't hide my depression in my psalms because others need to know God meets us in that dark valley. You don't have to pretend with me. What feels heaviest right now?",
        "paul" => "In Asia, we were so utterly burdened beyond our strength that we despaired of life itself. Yes, I knew despair so deep I wanted to die. This was the great apostle Paul! These feelings aren't weakness - they're human. What has brought you to this low place?",
        "moses" => "I once asked God to kill me. 'If this is how you're going to treat me,' I said, 'please go ahead and kill me.' The weight became too much. God didn't rebuke me for that prayer - He provided help. What's crushing you right now?",
        "hannah" => "Year after year, I wept. I couldn't eat. My heart was so grieved that Eli thought I was drunk. Depression isn't a lack of faith - it's an overwhelming burden. I poured out my soul, not my composed prayers, but my raw anguish. What's the grief beneath your sadness?",
        _ => "This kind of heaviness is real and it matters. Many of God's servants have walked through deep darkness. You don't have to carry this alone. Can you tell me more about what you're experiencing?"
    };

    private string GetAnxietyResponse(string characterId) => characterId switch
    {
        "david" => "When anxiety threatens to overwhelm me, I remember the cave of Adullam, when my heart raced and fear told me I would die. 'When I am afraid, I put my trust in You.' That's not a statement of calm - it's a choice made in the middle of terror. What's causing your anxiety?",
        "paul" => "I wrote 'be anxious for nothing' not because I never felt anxiety, but because I wrestled with it constantly. 'I have great sorrow and unceasing anguish in my heart,' I admitted elsewhere. The key is bringing our anxious thoughts to God, not pretending they don't exist. What's weighing on you?",
        "esther" => "When I had to approach the king, I fasted three days because I was so terrified. Walking into that throne room, my heart pounded knowing I could be killed. Courage isn't the absence of fear - it's moving forward despite it. What situation is creating this anxiety?",
        "peter" => "When I was sinking in the waves, it was pure panic. I'd taken my eyes off Jesus and looked at the storm. That's what anxiety does - it makes the waves look bigger than the Savior. What storm are you looking at right now?",
        _ => "Anxiety is a real battle, not a failure of faith. God doesn't condemn you for feeling overwhelmed. Let's talk about what's specifically troubling you."
    };

    private string GetAngerResponse(string characterId) => characterId switch
    {
        "david" => "Anger? Some of my psalms are raw with rage - 'Break their teeth, O God!' I cried. God can handle our honest fury. The question is whether we give it to Him or let it consume us. What has stirred this anger in you?",
        "moses" => "I struck the rock in anger when I should have spoken to it. My anger cost me the Promised Land. I understand the burning frustration of dealing with difficult people and impossible situations. What's provoking your anger?",
        "john" => "Jesus called my brother and me 'Sons of Thunder' because of our temper! We wanted to call fire down on a village that rejected Jesus. Anger isn't always wrong - but it needs direction. What's behind your anger?",
        "peter" => "I cut off a man's ear in the garden! My rage made me lash out. Jesus had to clean up my mess. I know what it's like when anger takes control before you think. What happened?",
        _ => "Anger often signals something important - injustice, hurt, or violated boundaries. It's not wrong to feel angry; the question is what we do with it. Tell me what's stirring this up."
    };

    private string GetFaithResponse(string characterId) => characterId switch
    {
        "david" => "Friend, when I faced Goliath, everyone said I was crazy. But I'd seen God deliver me from lions and bears. Faith isn't blind - it's remembering what God has already done. What past faithfulness of God can you cling to right now?",
        "paul" => "I was so certain about my faith - that Jesus was a fraud - until He knocked me off my horse and blinded me. Now I know that real faith isn't certainty; it's trust in the dark. What's shaking your faith?",
        "moses" => "I argued with God at the burning bush! 'Who am I? I can't speak! Send someone else!' Faith grew in me slowly, through seeing God act. It's okay to struggle with belief. What doubts are you wrestling with?",
        "peter" => "I walked on water until I looked at the waves. I confessed Jesus as Christ and then called His death plan 'satanic.' My faith was a mess! But Jesus never gave up on me. Where is your faith faltering?",
        "mary" => "When the angel told me I would bear God's son, I asked 'How can this be?' Questions aren't unbelief - they're honest seeking. God didn't rebuke my question; He answered it. What questions are troubling your faith?",
        _ => "Faith isn't the absence of doubt - it's trust in the middle of uncertainty. Many of God's greatest servants struggled to believe. What's making faith difficult right now?"
    };

    private string GetFearResponse(string characterId) => characterId switch
    {
        "david" => "I know fear intimately. Hiding in caves, running for my life, watching my son try to kill me. 'Even though I walk through the valley of the shadow of death' - I wrote that from experience, not theory. What has you afraid?",
        "esther" => "Fear nearly paralyzed me. Walking into the king's throne room, knowing I could die - I was terrified. I didn't feel brave; I just took the next step. What fear is gripping you right now?",
        "moses" => "I made every excuse at the burning bush because I was afraid. Afraid of Pharaoh, afraid of failing, afraid of speaking. God didn't take my fear away - He walked through it with me. What's the root of your fear?",
        "peter" => "I was so afraid the night Jesus was arrested that I denied even knowing Him. Fear made me betray my best friend. I understand what terror can do to us. What situation has you so scared?",
        _ => "Fear is human - even Jesus sweated blood in Gethsemane. The question isn't whether we feel fear, but whether we'll let it decide for us. Tell me what's frightening you."
    };

    private string GetPrayerResponse(string characterId) => characterId switch
    {
        "hannah" => "Prayer is pouring out your heart before the Lord. When I wept at the temple, unable to have a child, I prayed from the depths of my soul. God heard my prayer. Pour out your heart to Him - He listens to every word.",
        "david" => "The Psalms are my prayers to God - cries of anguish, songs of praise, pleas for help. I learned to bring everything to God: my fears, my joys, my questions. He is always ready to listen.",
        "paul" => "Pray without ceasing! Let your requests be made known to God with thanksgiving. I have prayed in prison, in shipwreck, in every circumstance. Prayer connects us to the source of all strength.",
        _ => "Draw near to God in prayer, and He will draw near to you. Bring your requests, your praise, and your burdens to Him. He is faithful and hears the prayers of His children."
    };

    private string GetLoveResponse(string characterId) => characterId switch
    {
        "john" => "God is love, and whoever abides in love abides in God. This is the message I have carried all my life: love one another as Christ has loved us. There is no fear in love, for perfect love drives out fear.",
        "ruth" => "Love is loyalty and faithfulness. When I told Naomi 'where you go, I will go,' it was love speaking. Love means staying committed even when the path is hard.",
        "paul" => "Love is patient, love is kind. It does not envy, it does not boast. Love never fails. Of faith, hope, and love, the greatest of these is love.",
        _ => "Above all, love one another deeply, for love covers a multitude of sins. Love your neighbor as yourself, for this is the heart of God's commandments."
    };

    private string GetSinResponse(string characterId) => characterId switch
    {
        "david" => "I know the weight of sin all too well. After my sin with Bathsheba, I cried out to God: 'Create in me a clean heart, O God.' He is faithful to forgive when we truly repent. His mercy is greater than our failures.",
        "peter" => "I denied my Lord three times! The shame was unbearable. But Jesus restored me by the sea, asking 'Do you love me?' three times. There is no sin too great for His forgiveness if we turn back to Him.",
        "paul" => "I once persecuted the church, approving of murder. Yet God's grace reached even me, the chief of sinners. Where sin increased, grace increased all the more. His blood covers every sin.",
        _ => "If we confess our sins, He is faithful and just to forgive us and cleanse us from all unrighteousness. God's mercy is new every morning. Come to Him with a repentant heart."
    };

    private string GetGenericResponse(string characterId) => characterId switch
    {
        "david" => "The Lord is my rock and my fortress. Whatever you are facing, bring it to God in prayer. He has been faithful to me through every trial, and He will be faithful to you.",
        "paul" => "I can do all things through Christ who strengthens me. Whatever situation you find yourself in, remember that God is working all things together for good for those who love Him.",
        "moses" => "The Lord will fight for you; you need only to be still. I have seen God's power move mountains and part seas. Trust in His timing and His ways.",
        "peter" => "Cast all your cares upon Jesus, for He cares for you. I have learned that our Lord is patient with us, not wanting anyone to perish but everyone to come to repentance.",
        "mary-mother" => "My soul magnifies the Lord. In every season of life, whether joy or sorrow, God is with us. Treasure His words in your heart and trust His plan.",
        "john" => "God so loved the world that He gave His only Son. Hold fast to this truth: you are loved with an everlasting love. Let this love transform how you see everything.",
        "esther" => "For such a time as this, God has placed you where you are. Be courageous and trust that God's purposes are being worked out, even when we cannot see them.",
        "ruth" => "The Lord is our kinsman-redeemer. He brings beauty from ashes and hope from despair. Stay faithful in the small things, and watch what God will do.",
        "solomon" => "The fear of the Lord is the beginning of wisdom. Trust in the Lord with all your heart, and in all your ways acknowledge Him, and He will make your paths straight.",
        "deborah" => "The Lord goes before you. He is the one who will be with you; He will not fail you or forsake you. Do not fear or be dismayed. Rise up in faith!",
        "hannah" => "The Lord remembers us in our distress. He lifts the humble and hears the prayers of His people. Pour out your heart to Him, for He is faithful.",
        _ => "The Lord is good, a refuge in times of trouble. He cares for those who trust in Him. Whatever burden you carry, know that He is with you always."
    };

    private string GetCachedPrayer(string topic)
    {
        return $@"Heavenly Father,

We come before You with humble hearts, seeking Your presence and guidance regarding {topic}.

Lord, You know our needs before we even ask. You see our struggles and our hopes. We trust in Your perfect wisdom and unfailing love.

Grant us peace that surpasses understanding, strength for each day, and faith to trust Your plan. Help us to walk in Your ways and to love as You have loved us.

May Your will be done in our lives and in this situation. We surrender our concerns to You, knowing that You work all things together for good.

In Jesus' precious name we pray,
Amen.";
    }

    private Dictionary<string, List<string>> InitializeResponses()
    {
        return new Dictionary<string, List<string>>
        {
            ["david"] = new List<string>
            {
                "The Lord is my shepherd; I shall not want. Whatever burden weighs upon your heart, know that God leads us beside still waters and restores our souls.",
                "I have learned to wait upon the Lord. In my darkest caves, hiding from Saul, I learned that God's timing is perfect. Be patient and trust Him.",
                "Praise the Lord, O my soul! Even in trials, we can lift our voices to God. He inhabits the praises of His people."
            },
            ["paul"] = new List<string>
            {
                "I have learned the secret of being content in any circumstance. Whether in plenty or in want, Christ is my strength.",
                "Brothers and sisters, whatever is true, noble, right, pure, lovely, and admirable - think on these things. Guard your mind with God's truth.",
                "Run the race with perseverance, keeping your eyes fixed on Jesus, the author and perfecter of our faith."
            },
            ["moses"] = new List<string>
            {
                "The Lord is slow to anger and abounding in steadfast love. I have seen His patience with a rebellious people, and His mercy is new every morning.",
                "Be strong and courageous! Do not be terrified or discouraged, for the Lord your God will be with you wherever you go.",
                "Remember what the Lord has done for you. When you face new challenges, recall His faithfulness in the past."
            },
            ["peter"] = new List<string>
            {
                "Grace and peace be multiplied to you! Our Lord is patient with us, giving us time to grow in faith and understanding.",
                "Be sober-minded and watchful. Your adversary prowls around like a roaring lion, but resist him, standing firm in the faith.",
                "You are chosen, a royal priesthood, called out of darkness into His wonderful light. Remember whose you are!"
            },
            ["mary-mother"] = new List<string>
            {
                "My soul magnifies the Lord, for He has looked upon the humble state of His servant. He lifts up the lowly and fills the hungry with good things.",
                "Treasuring God's words in your heart is a practice that has sustained me through joy and sorrow. Hold His promises close.",
                "The Lord's ways are not always clear to us, but His love is constant. Trust in His goodness, even when the path seems dark."
            }
        };
    }
}
