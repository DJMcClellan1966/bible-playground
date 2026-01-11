namespace AI_Bible_App.Core.Models;

/// <summary>
/// Options for personalizing generated prayers
/// </summary>
public class PrayerOptions
{
    /// <summary>
    /// The main topic/request for the prayer
    /// </summary>
    public string Topic { get; set; } = "";

    /// <summary>
    /// Current emotional state/mood of the person
    /// </summary>
    public PrayerMood? Mood { get; set; }

    /// <summary>
/// The type/style of prayer request
/// </summary>
public PrayerRequestType RequestType { get; set; } = PrayerRequestType.General;
    /// <summary>
    /// Life circumstances or events to incorporate
    /// </summary>
    public string? LifeCircumstances { get; set; }

    /// <summary>
    /// Personal prayer intentions to include
    /// </summary>
    public List<string> Intentions { get; set; } = new();

    /// <summary>
    /// Time of day for contextual prayers
    /// </summary>
    public TimeOfDayContext? TimeContext { get; set; }

    /// <summary>
    /// Whether to include Scripture references
    /// </summary>
    public bool IncludeScripture { get; set; } = true;

    /// <summary>
    /// Preferred prayer length
    /// </summary>
    public PrayerLength Length { get; set; } = PrayerLength.Medium;

    /// <summary>
    /// Name(s) to personalize the prayer for
    /// </summary>
    public string? PrayingFor { get; set; }

    /// <summary>
    /// Religious tradition style (affects language/structure)
    /// </summary>
    public PrayerTradition Tradition { get; set; } = PrayerTradition.General;

    /// <summary>
    /// Creates default options from just a topic string
    /// </summary>
    public static PrayerOptions FromTopic(string topic) => new() { Topic = topic };
}

/// <summary>
/// Emotional state/mood for personalized prayers
/// </summary>
public enum PrayerMood
{
    Grateful,
    Anxious,
    Joyful,
    Grieving,
    Hopeful,
    Overwhelmed,
    Peaceful,
    Confused,
    Fearful,
    Lonely,
    Angry,
    Content,
    Seeking
}

/// <summary>
/// Type/style of prayer request
/// </summary>
public enum PrayerRequestType
{
    General,
    Thanksgiving,
    Petition,
    Intercession,
    Confession,
    Adoration,
    Lament,
    Protection,
    Healing,
    Guidance,
    Blessing,
    Dedication
}

/// <summary>
/// Time of day context for prayers
/// </summary>
public enum TimeOfDayContext
{
    Morning,
    Midday,
    Evening,
    Night,
    BeforeMeal,
    AfterMeal,
    BeforeSleep,
    BeforeWork,
    BeforeTravel
}

/// <summary>
/// Prayer length preference
/// </summary>
public enum PrayerLength
{
    Brief,      // 2-3 sentences
    Short,      // 1 paragraph
    Medium,     // 2-3 paragraphs
    Long,       // 4-5 paragraphs
    Extended    // Full devotional prayer
}

/// <summary>
/// Prayer tradition/style (affects language)
/// </summary>
public enum PrayerTradition
{
    General,
    Traditional,    // More formal, King James style
    Contemporary,   // Modern language
    Contemplative,  // Meditative, Lectio Divina style
    Liturgical      // Structured, responsive style
}

/// <summary>
/// Extensions for prayer enums
/// </summary>
public static class PrayerEnumExtensions
{
    public static string GetDescription(this PrayerMood mood) => mood switch
    {
        PrayerMood.Grateful => "feeling thankful and blessed",
        PrayerMood.Anxious => "struggling with worry and anxiety",
        PrayerMood.Joyful => "filled with happiness and celebration",
        PrayerMood.Grieving => "experiencing loss and sorrow",
        PrayerMood.Hopeful => "looking forward with hope",
        PrayerMood.Overwhelmed => "feeling burdened by life's challenges",
        PrayerMood.Peaceful => "experiencing calm and tranquility",
        PrayerMood.Confused => "seeking clarity and understanding",
        PrayerMood.Fearful => "facing fears and uncertainties",
        PrayerMood.Lonely => "feeling isolated or alone",
        PrayerMood.Angry => "processing frustration or hurt",
        PrayerMood.Content => "at peace with current circumstances",
        PrayerMood.Seeking => "searching for direction or purpose",
        _ => ""
    };

    public static string GetDescription(this PrayerRequestType style) => style switch
    {
        PrayerRequestType.Thanksgiving => "a prayer of gratitude and praise for God's blessings",
        PrayerRequestType.Petition => "a prayer asking God for specific needs",
        PrayerRequestType.Intercession => "a prayer on behalf of others",
        PrayerRequestType.Confession => "a prayer acknowledging sins and seeking forgiveness",
        PrayerRequestType.Adoration => "a prayer of worship and praise",
        PrayerRequestType.Lament => "a prayer expressing grief or sorrow to God",
        PrayerRequestType.Protection => "a prayer asking for God's protection and safety",
        PrayerRequestType.Healing => "a prayer for physical, emotional, or spiritual healing",
        PrayerRequestType.Guidance => "a prayer seeking God's wisdom and direction",
        PrayerRequestType.Blessing => "a prayer of blessing over someone or something",
        PrayerRequestType.Dedication => "a prayer consecrating something or someone to God",
        _ => "a heartfelt prayer"
    };

    public static string GetTimeGreeting(this TimeOfDayContext time) => time switch
    {
        TimeOfDayContext.Morning => "as this new day begins",
        TimeOfDayContext.Midday => "in the midst of this day",
        TimeOfDayContext.Evening => "as this day draws to a close",
        TimeOfDayContext.Night => "in the quiet of this night",
        TimeOfDayContext.BeforeMeal => "as we gather for this meal",
        TimeOfDayContext.AfterMeal => "having been nourished by this food",
        TimeOfDayContext.BeforeSleep => "as I prepare for rest",
        TimeOfDayContext.BeforeWork => "as I begin my work",
        TimeOfDayContext.BeforeTravel => "as I prepare to travel",
        _ => ""
    };

    public static int GetWordCount(this PrayerLength length) => length switch
    {
        PrayerLength.Brief => 50,
        PrayerLength.Short => 100,
        PrayerLength.Medium => 200,
        PrayerLength.Long => 350,
        PrayerLength.Extended => 500,
        _ => 200
    };

    public static string GetStyleHint(this PrayerTradition tradition) => tradition switch
    {
        PrayerTradition.Traditional => "Use traditional, reverent language (like King James style). Include 'Thee', 'Thou', 'Thy' when addressing God.",
        PrayerTradition.Contemporary => "Use modern, accessible language that feels natural and relatable.",
        PrayerTradition.Contemplative => "Create a meditative, reflective prayer with pauses for silence and listening.",
        PrayerTradition.Liturgical => "Structure the prayer with responses, using a call-and-response or litany format.",
        _ => "Use warm, sincere language appropriate for personal prayer."
    };
}
