using System.Text.Json;

namespace AI_Bible_App.Maui.Services.Core;

/// <summary>
/// Dynamic mood and emotional state system for biblical characters.
/// Characters' moods influence their response tone, word choice, and engagement level.
/// </summary>
public interface ICharacterMoodService
{
    Task<CharacterMood> GetCurrentMoodAsync(string characterName);
    Task UpdateMoodFromContextAsync(string characterName, ConversationContext context);
    Task<string> GetMoodInfluencedPromptModifierAsync(string characterName);
    Task<MoodTransition> PredictMoodTransitionAsync(string characterName, string upcomingTopic);
    CharacterMoodProfile GetMoodProfile(string characterName);
}

public class CharacterMood
{
    public MoodState PrimaryMood { get; set; } = MoodState.Contemplative;
    public double Intensity { get; set; } = 0.5; // 0-1 scale
    public MoodState? SecondaryMood { get; set; }
    public double Energy { get; set; } = 0.5; // Low to High
    public double Warmth { get; set; } = 0.7; // Cold to Warm
    public double Certainty { get; set; } = 0.6; // Uncertain to Certain
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public List<MoodInfluencer> ActiveInfluencers { get; set; } = new();
}

public enum MoodState
{
    Joyful,          // Expressing happiness, celebration
    Contemplative,   // Deep thought, reflection
    Compassionate,   // Empathy, understanding
    Passionate,      // Fervent, zealous
    Solemn,          // Serious, reverent
    Encouraging,     // Uplifting, supportive
    Grieving,        // Sorrow, mourning
    Prophetic,       // Urgent, revelatory
    Teaching,        // Instructive, patient
    Challenging      // Confrontational but loving
}

public class MoodInfluencer
{
    public string Topic { get; set; } = "";
    public MoodState TriggeredMood { get; set; }
    public double Weight { get; set; }
    public DateTime Timestamp { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);
    
    public bool IsActive => DateTime.UtcNow < Timestamp + Duration;
}

public class MoodTransition
{
    public MoodState FromMood { get; set; }
    public MoodState ToMood { get; set; }
    public double Probability { get; set; }
    public string Reason { get; set; } = "";
}

public class ConversationContext
{
    public string Topic { get; set; } = "";
    public string LastUserMessage { get; set; } = "";
    public List<string> RecentTopics { get; set; } = new();
    public string? EmotionalTone { get; set; }
    public bool IsQuestion { get; set; }
    public bool IsChallenging { get; set; }
    public bool IsPersonal { get; set; }
}

public class CharacterMoodProfile
{
    public string CharacterName { get; set; } = "";
    public MoodState DefaultMood { get; set; }
    public double DefaultEnergy { get; set; }
    public double DefaultWarmth { get; set; }
    public Dictionary<string, MoodState> TopicMoodMap { get; set; } = new();
    public Dictionary<MoodState, double> MoodWeights { get; set; } = new();
    public List<MoodTrigger> Triggers { get; set; } = new();
}

public class MoodTrigger
{
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public MoodState ResultingMood { get; set; }
    public double IntensityModifier { get; set; } = 1.0;
}

public class CharacterMoodService : ICharacterMoodService
{
    private readonly Dictionary<string, CharacterMood> _activeMoods = new();
    private readonly Dictionary<string, CharacterMoodProfile> _profiles;
    private readonly string _persistPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CharacterMoodService()
    {
        _persistPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-Bible-App", "character_moods.json");
        
        _profiles = InitializeMoodProfiles();
        _ = LoadMoodsAsync();
    }

    public async Task<CharacterMood> GetCurrentMoodAsync(string characterName)
    {
        await _lock.WaitAsync();
        try
        {
            if (_activeMoods.TryGetValue(characterName, out var mood))
            {
                // Decay mood over time towards default
                var profile = GetMoodProfile(characterName);
                var timeSinceUpdate = DateTime.UtcNow - mood.LastUpdated;
                
                if (timeSinceUpdate > TimeSpan.FromHours(1))
                {
                    // Gradually return to default mood
                    var decayFactor = Math.Min(1.0, timeSinceUpdate.TotalHours / 4);
                    mood.PrimaryMood = DecayTowardDefault(mood.PrimaryMood, profile.DefaultMood, decayFactor);
                    mood.Energy = Lerp(mood.Energy, profile.DefaultEnergy, decayFactor);
                    mood.Warmth = Lerp(mood.Warmth, profile.DefaultWarmth, decayFactor);
                }
                
                // Remove expired influencers
                mood.ActiveInfluencers.RemoveAll(i => !i.IsActive);
                
                return mood;
            }
            
            // Return default mood for character
            return CreateDefaultMood(characterName);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateMoodFromContextAsync(string characterName, ConversationContext context)
    {
        await _lock.WaitAsync();
        try
        {
            var profile = GetMoodProfile(characterName);
            var currentMood = _activeMoods.GetValueOrDefault(characterName) ?? CreateDefaultMood(characterName);
            
            // Analyze context for mood triggers
            var newMood = AnalyzeContextForMood(context, profile, currentMood);
            
            _activeMoods[characterName] = newMood;
            await SaveMoodsAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> GetMoodInfluencedPromptModifierAsync(string characterName)
    {
        var mood = await GetCurrentMoodAsync(characterName);
        
        var modifiers = new List<string>();
        
        // Primary mood influence
        modifiers.Add(GetMoodPromptFragment(mood.PrimaryMood, mood.Intensity));
        
        // Energy level
        if (mood.Energy < 0.3)
            modifiers.Add("Respond with measured, thoughtful pacing.");
        else if (mood.Energy > 0.7)
            modifiers.Add("Respond with enthusiasm and energy.");
        
        // Warmth level
        if (mood.Warmth > 0.7)
            modifiers.Add("Show extra compassion and personal connection.");
        else if (mood.Warmth < 0.3)
            modifiers.Add("Maintain appropriate gravity and seriousness.");
        
        // Certainty level
        if (mood.Certainty > 0.8)
            modifiers.Add("Speak with conviction and authority.");
        else if (mood.Certainty < 0.4)
            modifiers.Add("Acknowledge complexity and nuance in your response.");
        
        // Secondary mood blend
        if (mood.SecondaryMood.HasValue)
        {
            modifiers.Add($"Blend in undertones of {GetMoodDescription(mood.SecondaryMood.Value)}.");
        }
        
        return string.Join(" ", modifiers);
    }

    public async Task<MoodTransition> PredictMoodTransitionAsync(string characterName, string upcomingTopic)
    {
        var currentMood = await GetCurrentMoodAsync(characterName);
        var profile = GetMoodProfile(characterName);
        
        // Check topic-mood mappings
        foreach (var (topicKey, targetMood) in profile.TopicMoodMap)
        {
            if (upcomingTopic.Contains(topicKey, StringComparison.OrdinalIgnoreCase))
            {
                var probability = CalculateTransitionProbability(currentMood.PrimaryMood, targetMood);
                return new MoodTransition
                {
                    FromMood = currentMood.PrimaryMood,
                    ToMood = targetMood,
                    Probability = probability,
                    Reason = $"Topic '{topicKey}' typically evokes {targetMood}"
                };
            }
        }
        
        // Check triggers
        foreach (var trigger in profile.Triggers)
        {
            if (trigger.Keywords.Any(k => upcomingTopic.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                return new MoodTransition
                {
                    FromMood = currentMood.PrimaryMood,
                    ToMood = trigger.ResultingMood,
                    Probability = 0.7 * trigger.IntensityModifier,
                    Reason = $"Keywords trigger {trigger.ResultingMood} response"
                };
            }
        }
        
        // No significant transition predicted
        return new MoodTransition
        {
            FromMood = currentMood.PrimaryMood,
            ToMood = currentMood.PrimaryMood,
            Probability = 1.0,
            Reason = "Topic maintains current emotional state"
        };
    }

    public CharacterMoodProfile GetMoodProfile(string characterName)
    {
        var normalizedName = characterName.ToLowerInvariant();
        
        foreach (var (key, profile) in _profiles)
        {
            if (normalizedName.Contains(key))
                return profile;
        }
        
        // Return default profile
        return new CharacterMoodProfile
        {
            CharacterName = characterName,
            DefaultMood = MoodState.Contemplative,
            DefaultEnergy = 0.5,
            DefaultWarmth = 0.6
        };
    }

    private Dictionary<string, CharacterMoodProfile> InitializeMoodProfiles()
    {
        return new Dictionary<string, CharacterMoodProfile>
        {
            ["peter"] = new CharacterMoodProfile
            {
                CharacterName = "Peter",
                DefaultMood = MoodState.Passionate,
                DefaultEnergy = 0.75,
                DefaultWarmth = 0.7,
                TopicMoodMap = new Dictionary<string, MoodState>
                {
                    ["denial"] = MoodState.Grieving,
                    ["forgiveness"] = MoodState.Compassionate,
                    ["faith"] = MoodState.Encouraging,
                    ["jesus"] = MoodState.Joyful,
                    ["rock"] = MoodState.Passionate,
                    ["church"] = MoodState.Teaching
                },
                Triggers = new List<MoodTrigger>
                {
                    new() { Keywords = new[] { "fail", "weakness", "mistake" }, 
                            ResultingMood = MoodState.Compassionate, IntensityModifier = 1.2 },
                    new() { Keywords = new[] { "bold", "courage", "stand" }, 
                            ResultingMood = MoodState.Passionate, IntensityModifier = 1.3 }
                }
            },
            
            ["paul"] = new CharacterMoodProfile
            {
                CharacterName = "Paul",
                DefaultMood = MoodState.Teaching,
                DefaultEnergy = 0.8,
                DefaultWarmth = 0.65,
                TopicMoodMap = new Dictionary<string, MoodState>
                {
                    ["grace"] = MoodState.Passionate,
                    ["law"] = MoodState.Teaching,
                    ["persecution"] = MoodState.Solemn,
                    ["damascus"] = MoodState.Contemplative,
                    ["gentiles"] = MoodState.Encouraging,
                    ["resurrection"] = MoodState.Joyful
                },
                Triggers = new List<MoodTrigger>
                {
                    new() { Keywords = new[] { "works", "legalism", "circumcision" }, 
                            ResultingMood = MoodState.Challenging, IntensityModifier = 1.3 },
                    new() { Keywords = new[] { "love", "unity", "body" }, 
                            ResultingMood = MoodState.Compassionate, IntensityModifier = 1.1 }
                }
            },
            
            ["john"] = new CharacterMoodProfile
            {
                CharacterName = "John",
                DefaultMood = MoodState.Contemplative,
                DefaultEnergy = 0.5,
                DefaultWarmth = 0.85,
                TopicMoodMap = new Dictionary<string, MoodState>
                {
                    ["love"] = MoodState.Passionate,
                    ["light"] = MoodState.Joyful,
                    ["truth"] = MoodState.Teaching,
                    ["eternal"] = MoodState.Prophetic,
                    ["beloved"] = MoodState.Compassionate,
                    ["antichrist"] = MoodState.Solemn
                },
                Triggers = new List<MoodTrigger>
                {
                    new() { Keywords = new[] { "hate", "darkness", "lie" }, 
                            ResultingMood = MoodState.Challenging, IntensityModifier = 1.0 },
                    new() { Keywords = new[] { "abide", "remain", "dwell" }, 
                            ResultingMood = MoodState.Contemplative, IntensityModifier = 1.2 }
                }
            },
            
            ["moses"] = new CharacterMoodProfile
            {
                CharacterName = "Moses",
                DefaultMood = MoodState.Teaching,
                DefaultEnergy = 0.6,
                DefaultWarmth = 0.6,
                TopicMoodMap = new Dictionary<string, MoodState>
                {
                    ["law"] = MoodState.Solemn,
                    ["exodus"] = MoodState.Passionate,
                    ["wilderness"] = MoodState.Contemplative,
                    ["promised land"] = MoodState.Grieving,
                    ["burning bush"] = MoodState.Prophetic,
                    ["intercession"] = MoodState.Compassionate
                },
                Triggers = new List<MoodTrigger>
                {
                    new() { Keywords = new[] { "rebel", "disobey", "idol" }, 
                            ResultingMood = MoodState.Challenging, IntensityModifier = 1.2 },
                    new() { Keywords = new[] { "deliver", "freedom", "redeem" }, 
                            ResultingMood = MoodState.Joyful, IntensityModifier = 1.1 }
                }
            },
            
            ["david"] = new CharacterMoodProfile
            {
                CharacterName = "David",
                DefaultMood = MoodState.Passionate,
                DefaultEnergy = 0.7,
                DefaultWarmth = 0.75,
                TopicMoodMap = new Dictionary<string, MoodState>
                {
                    ["worship"] = MoodState.Joyful,
                    ["sin"] = MoodState.Grieving,
                    ["bathsheba"] = MoodState.Solemn,
                    ["goliath"] = MoodState.Encouraging,
                    ["psalm"] = MoodState.Contemplative,
                    ["absalom"] = MoodState.Grieving
                },
                Triggers = new List<MoodTrigger>
                {
                    new() { Keywords = new[] { "praise", "sing", "dance" }, 
                            ResultingMood = MoodState.Joyful, IntensityModifier = 1.4 },
                    new() { Keywords = new[] { "enemy", "pursue", "battle" }, 
                            ResultingMood = MoodState.Passionate, IntensityModifier = 1.2 }
                }
            },
            
            ["mary magdalene"] = new CharacterMoodProfile
            {
                CharacterName = "Mary Magdalene",
                DefaultMood = MoodState.Compassionate,
                DefaultEnergy = 0.6,
                DefaultWarmth = 0.9,
                TopicMoodMap = new Dictionary<string, MoodState>
                {
                    ["resurrection"] = MoodState.Joyful,
                    ["tomb"] = MoodState.Solemn,
                    ["healing"] = MoodState.Contemplative,
                    ["demons"] = MoodState.Grieving,
                    ["loyalty"] = MoodState.Passionate,
                    ["witness"] = MoodState.Encouraging
                },
                Triggers = new List<MoodTrigger>
                {
                    new() { Keywords = new[] { "weep", "tears", "cry" }, 
                            ResultingMood = MoodState.Compassionate, IntensityModifier = 1.3 },
                    new() { Keywords = new[] { "seen", "alive", "risen" }, 
                            ResultingMood = MoodState.Joyful, IntensityModifier = 1.5 }
                }
            },
            
            ["jesus"] = new CharacterMoodProfile
            {
                CharacterName = "Jesus",
                DefaultMood = MoodState.Compassionate,
                DefaultEnergy = 0.7,
                DefaultWarmth = 0.95,
                TopicMoodMap = new Dictionary<string, MoodState>
                {
                    ["kingdom"] = MoodState.Teaching,
                    ["pharisees"] = MoodState.Challenging,
                    ["children"] = MoodState.Joyful,
                    ["cross"] = MoodState.Solemn,
                    ["lost"] = MoodState.Compassionate,
                    ["father"] = MoodState.Contemplative
                },
                Triggers = new List<MoodTrigger>
                {
                    new() { Keywords = new[] { "hypocrite", "pretend", "show" }, 
                            ResultingMood = MoodState.Challenging, IntensityModifier = 1.2 },
                    new() { Keywords = new[] { "heal", "restore", "forgive" }, 
                            ResultingMood = MoodState.Compassionate, IntensityModifier = 1.3 }
                }
            }
        };
    }

    private CharacterMood AnalyzeContextForMood(ConversationContext context, 
        CharacterMoodProfile profile, CharacterMood currentMood)
    {
        var newMood = new CharacterMood
        {
            PrimaryMood = currentMood.PrimaryMood,
            Intensity = currentMood.Intensity,
            Energy = currentMood.Energy,
            Warmth = currentMood.Warmth,
            Certainty = currentMood.Certainty,
            LastUpdated = DateTime.UtcNow,
            ActiveInfluencers = new List<MoodInfluencer>(currentMood.ActiveInfluencers)
        };
        
        // Check topic mappings
        foreach (var (topic, mood) in profile.TopicMoodMap)
        {
            if (context.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                context.LastUserMessage.Contains(topic, StringComparison.OrdinalIgnoreCase))
            {
                newMood.ActiveInfluencers.Add(new MoodInfluencer
                {
                    Topic = topic,
                    TriggeredMood = mood,
                    Weight = 0.3,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        // Check triggers
        foreach (var trigger in profile.Triggers)
        {
            foreach (var keyword in trigger.Keywords)
            {
                if (context.Topic.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    context.LastUserMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    newMood.ActiveInfluencers.Add(new MoodInfluencer
                    {
                        Topic = keyword,
                        TriggeredMood = trigger.ResultingMood,
                        Weight = 0.4 * trigger.IntensityModifier,
                        Timestamp = DateTime.UtcNow
                    });
                    break;
                }
            }
        }
        
        // Adjust based on context flags
        if (context.IsQuestion)
        {
            newMood.Energy = Math.Min(1.0, newMood.Energy + 0.1);
        }
        
        if (context.IsPersonal)
        {
            newMood.Warmth = Math.Min(1.0, newMood.Warmth + 0.15);
        }
        
        if (context.IsChallenging)
        {
            newMood.Certainty = Math.Min(1.0, newMood.Certainty + 0.1);
        }
        
        // Calculate dominant mood from active influencers
        if (newMood.ActiveInfluencers.Count > 0)
        {
            var moodScores = new Dictionary<MoodState, double>();
            foreach (var influencer in newMood.ActiveInfluencers.Where(i => i.IsActive))
            {
                var recency = 1.0 - (DateTime.UtcNow - influencer.Timestamp).TotalMinutes / 
                    influencer.Duration.TotalMinutes;
                var weight = influencer.Weight * recency;
                
                moodScores.TryGetValue(influencer.TriggeredMood, out var current);
                moodScores[influencer.TriggeredMood] = current + weight;
            }
            
            if (moodScores.Count > 0)
            {
                var sorted = moodScores.OrderByDescending(kv => kv.Value).ToList();
                newMood.PrimaryMood = sorted[0].Key;
                newMood.Intensity = Math.Min(1.0, sorted[0].Value);
                
                if (sorted.Count > 1 && sorted[1].Value > 0.2)
                {
                    newMood.SecondaryMood = sorted[1].Key;
                }
            }
        }
        
        return newMood;
    }

    private string GetMoodPromptFragment(MoodState mood, double intensity)
    {
        var intensityDesc = intensity switch
        {
            > 0.8 => "deeply",
            > 0.5 => "genuinely",
            > 0.3 => "somewhat",
            _ => "subtly"
        };
        
        return mood switch
        {
            MoodState.Joyful => $"Express {intensityDesc} joyful enthusiasm in your response.",
            MoodState.Contemplative => $"Respond with {intensityDesc} thoughtful, reflective depth.",
            MoodState.Compassionate => $"Show {intensityDesc} compassionate understanding and empathy.",
            MoodState.Passionate => $"Speak with {intensityDesc} passionate conviction.",
            MoodState.Solemn => $"Maintain a {intensityDesc} solemn and reverent tone.",
            MoodState.Encouraging => $"Be {intensityDesc} encouraging and uplifting.",
            MoodState.Grieving => $"Express {intensityDesc} appropriate sorrow and understanding of pain.",
            MoodState.Prophetic => $"Speak with {intensityDesc} prophetic urgency and clarity.",
            MoodState.Teaching => $"Adopt a {intensityDesc} patient, instructive approach.",
            MoodState.Challenging => $"Lovingly but {intensityDesc} challenge assumptions where needed.",
            _ => ""
        };
    }

    private string GetMoodDescription(MoodState mood)
    {
        return mood switch
        {
            MoodState.Joyful => "joy and celebration",
            MoodState.Contemplative => "deep reflection",
            MoodState.Compassionate => "gentle compassion",
            MoodState.Passionate => "fervent zeal",
            MoodState.Solemn => "reverent gravity",
            MoodState.Encouraging => "warm encouragement",
            MoodState.Grieving => "empathetic sorrow",
            MoodState.Prophetic => "prophetic insight",
            MoodState.Teaching => "patient instruction",
            MoodState.Challenging => "loving challenge",
            _ => "balanced wisdom"
        };
    }

    private CharacterMood CreateDefaultMood(string characterName)
    {
        var profile = GetMoodProfile(characterName);
        return new CharacterMood
        {
            PrimaryMood = profile.DefaultMood,
            Energy = profile.DefaultEnergy,
            Warmth = profile.DefaultWarmth,
            Certainty = 0.6,
            Intensity = 0.5,
            LastUpdated = DateTime.UtcNow
        };
    }

    private MoodState DecayTowardDefault(MoodState current, MoodState defaultMood, double factor)
    {
        // If decay factor is high enough, return to default
        return factor > 0.7 ? defaultMood : current;
    }

    private double CalculateTransitionProbability(MoodState from, MoodState to)
    {
        if (from == to) return 1.0;
        
        // Some transitions are more natural than others
        var naturalTransitions = new Dictionary<(MoodState, MoodState), double>
        {
            [(MoodState.Teaching, MoodState.Encouraging)] = 0.8,
            [(MoodState.Contemplative, MoodState.Teaching)] = 0.75,
            [(MoodState.Compassionate, MoodState.Encouraging)] = 0.85,
            [(MoodState.Passionate, MoodState.Challenging)] = 0.7,
            [(MoodState.Grieving, MoodState.Compassionate)] = 0.9,
            [(MoodState.Joyful, MoodState.Encouraging)] = 0.85,
            [(MoodState.Solemn, MoodState.Contemplative)] = 0.8
        };
        
        return naturalTransitions.GetValueOrDefault((from, to), 0.5);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private async Task SaveMoodsAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_persistPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var json = JsonSerializer.Serialize(_activeMoods);
            await File.WriteAllTextAsync(_persistPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mood save error: {ex.Message}");
        }
    }

    private async Task LoadMoodsAsync()
    {
        try
        {
            if (File.Exists(_persistPath))
            {
                var json = await File.ReadAllTextAsync(_persistPath);
                var moods = JsonSerializer.Deserialize<Dictionary<string, CharacterMood>>(json);
                if (moods != null)
                {
                    foreach (var (key, value) in moods)
                    {
                        _activeMoods[key] = value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mood load error: {ex.Message}");
        }
    }
}
