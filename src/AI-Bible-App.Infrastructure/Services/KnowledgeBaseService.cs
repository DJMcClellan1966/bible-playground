using AI_Bible_App.Core.Models;
using AI_Bible_App.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Manages historical context, language insights, and thematic connections
/// </summary>
public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly ILogger<KnowledgeBaseService> _logger;
    private readonly IDeviceCapabilityService? _deviceService;
    private readonly string _dataDirectory;
    
    private List<HistoricalContext> _historicalContexts = new();
    private List<LanguageInsight> _languageInsights = new();
    private List<ThematicConnection> _thematicConnections = new();
    private bool _initialized = false;
    private ModelConfiguration? _currentConfig;
    
    public KnowledgeBaseService(
        ILogger<KnowledgeBaseService> logger,
        IDeviceCapabilityService? deviceService = null)
    {
        _logger = logger;
        _deviceService = deviceService;
        _dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIBibleApp",
            "KnowledgeBase");
        
        Directory.CreateDirectory(_dataDirectory);
    }
    
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;
            
        try
        {
            _logger.LogInformation("Initializing knowledge base");
            
            // Get device configuration for pagination limits
            if (_deviceService != null)
            {
                _currentConfig = await _deviceService.GetRecommendedConfigurationAsync();
                _logger.LogInformation("Using configuration: {Config}", _currentConfig.DisplayName);
            }
            
            // Load historical contexts
            var historicalPath = Path.Combine(_dataDirectory, "historical_context.json");
            if (File.Exists(historicalPath))
            {
                var json = await File.ReadAllTextAsync(historicalPath);
                _historicalContexts = JsonSerializer.Deserialize<List<HistoricalContext>>(json) ?? new();
                _logger.LogInformation("Loaded {Count} historical contexts", _historicalContexts.Count);
            }
            else
            {
                // Create initial data
                _historicalContexts = CreateInitialHistoricalData();
                await SaveHistoricalContextsAsync();
            }
            
            // Load language insights
            var languagePath = Path.Combine(_dataDirectory, "language_insights.json");
            if (File.Exists(languagePath))
            {
                var json = await File.ReadAllTextAsync(languagePath);
                _languageInsights = JsonSerializer.Deserialize<List<LanguageInsight>>(json) ?? new();
                _logger.LogInformation("Loaded {Count} language insights", _languageInsights.Count);
            }
            else
            {
                _languageInsights = CreateInitialLanguageData();
                await SaveLanguageInsightsAsync();
            }
            
            // Load thematic connections
            var connectionsPath = Path.Combine(_dataDirectory, "thematic_connections.json");
            if (File.Exists(connectionsPath))
            {
                var json = await File.ReadAllTextAsync(connectionsPath);
                _thematicConnections = JsonSerializer.Deserialize<List<ThematicConnection>>(json) ?? new();
                _logger.LogInformation("Loaded {Count} thematic connections", _thematicConnections.Count);
            }
            else
            {
                _thematicConnections = CreateInitialConnectionsData();
                await SaveThematicConnectionsAsync();
            }
            
            _initialized = true;
            _logger.LogInformation("Knowledge base initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize knowledge base");
        }
    }
    
    public async Task<List<HistoricalContext>> GetHistoricalContextAsync(
        string characterId,
        string userQuestion,
        int maxResults = 3)
    {
        await InitializeAsync();
        
        // Apply device-specific limits
        if (_currentConfig != null && _currentConfig.UseKnowledgeBasePagination)
        {
            maxResults = Math.Min(maxResults, _currentConfig.MaxHistoricalContexts);
        }
        
        // Find contexts relevant to this character
        var characterContexts = _historicalContexts
            .Where(c => c.RelatedCharacters.Contains(characterId) || c.RelatedCharacters.Count == 0)
            .ToList();
        
        // Score by keyword relevance
        var questionWords = userQuestion.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet();
        
        var scored = characterContexts.Select(context => new
        {
            Context = context,
            Score = context.Keywords.Count(k => questionWords.Contains(k.ToLower())) * context.RelevanceWeight
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .Take(maxResults)
        .Select(x => x.Context)
        .ToList();
        
        return scored;
    }
    
    public async Task<List<LanguageInsight>> GetLanguageInsightsAsync(
        string passage,
        int maxResults = 5)
    {
        await InitializeAsync();
        
        // Apply device-specific limits
        if (_currentConfig != null && _currentConfig.UseKnowledgeBasePagination)
        {
            maxResults = Math.Min(maxResults, _currentConfig.MaxLanguageInsights);
        }
        
        // Find insights that match words in the passage
        var passageWords = passage.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var relevant = _languageInsights
            .Where(insight => passageWords.Any(w => insight.Word.ToLower().Contains(w)))
            .Take(maxResults)
            .ToList();
        
        return relevant;
    }
    
    public async Task<List<ThematicConnection>> FindThematicConnectionsAsync(
        strApply device-specific limits
        if (_currentConfig != null && _currentConfig.UseKnowledgeBasePagination)
        {
            maxResults = Math.Min(maxResults, _currentConfig.MaxThematicConnections);
        }
        
        // ing passage,
        string theme,
        int maxResults = 3)
    {
        await InitializeAsync();
        
        // Find connections that match the passage or theme
        var connections = _thematicConnections
            .Where(c => c.PrimaryPassage.Contains(passage) || 
                       c.SecondaryPassage.Contains(passage) ||
                       c.Theme.Contains(theme, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList();
        
        return connections;
    }
    
    private List<HistoricalContext> CreateInitialHistoricalData()
    {
        return new List<HistoricalContext>
        {
            // EGYPTIAN PERIOD (Moses)
            new()
            {
                Title = "Egyptian Slavery System",
                Period = "Egyptian Bondage (1500-1400 BCE)",
                Category = "Social Structure",
                Content = "Israelites were forced laborers (corvée) building store cities like Pithom and Rameses. Unlike chattel slavery, they maintained family units but had no freedom. Brick-making without straw was particularly brutal - straw was the binding agent, so they had to gather it while maintaining quotas. Overseers used beatings to enforce production. This created a generation that knew only oppression.",
                RelatedCharacters = new() { "moses", "aaron" },
                Keywords = new() { "slavery", "egypt", "pharaoh", "oppression", "freedom", "exodus" },
                Source = "Archaeological evidence from Kahun, Amarna letters",
                RelevanceWeight = 9
            },
            new()
            {
                Title = "Pharaoh as Divine King",
                Period = "Egyptian Bondage",
                Category = "Religion",
                Content = "Pharaohs were considered living gods (horus incarnate), mediators between gods and humanity. To challenge Pharaoh was to challenge a god. This explains why the plagues were targeted at specific Egyptian gods - turning the Nile to blood mocked Hapi (god of the Nile), darkness mocked Ra (sun god), death of firstborn challenged Pharaoh's own divinity. The contest was between gods, not just political powers.",
                RelatedCharacters = new() { "moses", "aaron" },
                Keywords = new() { "pharaoh", "god", "divine", "power", "plagues", "authority" },
                Source = "Egyptian theology, temple inscriptions",
                RelevanceWeight = 10
            },
            new()
            {
                Title = "Wilderness Survival",
                Period = "Exodus & Wandering (1400-1360 BCE)",
                Category = "Daily Life",
                Content = "The Sinai wilderness is one of the harshest environments on Earth. Temperatures exceed 120°F by day, drop to freezing at night. Water sources are rare and seasonal. Manna and quail were supernatural provision - no natural explanation fits daily manna that spoiled overnight (except Sabbath). The constant complaints make sense when you realize they left guaranteed food (even as slaves) for total uncertainty. Trust had to be rebuilt daily.",
                RelatedCharacters = new() { "moses", "aaron", "joshua" },
                Keywords = new() { "wilderness", "desert", "manna", "provision", "trust", "complaining" },
                Source = "Sinai topography, Bedouin survival accounts",
                RelevanceWeight = 8
            },
            
            // KINGDOM PERIOD (David, Solomon)
            new()
            {
                Title = "Shepherd Culture",
                Period = "United Monarchy (1000-930 BCE)",
                Category = "Occupation",
                Content = "Shepherds were considered lowly and unclean by urban Israelites - they lived outdoors, couldn't observe ceremonial purity, smelled of sheep. Yet David was a shepherd before king. Shepherds faced real dangers: lions, bears, thieves. A good shepherd would risk his life for one sheep because each sheep represented wealth and the owner trusted him. This makes 'The Lord is my shepherd' and Jesus as 'Good Shepherd' profound - God takes the lowly role of total care.",
                RelatedCharacters = new() { "david" },
                Keywords = new() { "shepherd", "sheep", "protection", "care", "lowly", "servant" },
                Source = "Cultural anthropology, biblical references",
                RelevanceWeight = 9
            },
            new()
            {
                Title = "Ancient Near Eastern Kingship",
                Period = "United Monarchy",
                Category = "Politics",
                Content = "Kings in the ancient Near East had absolute power - they were law, owned all land, had harems to cement political alliances, and could take whatever they wanted. David taking Bathsheba wasn't unusual for kings - Nathan's parable worked because David was supposed to be different. God's king was supposed to be like a shepherd (caring), not like other kings (taking). Solomon's 700 wives and 300 concubines were normal for empire-building through marriage alliances.",
                RelatedCharacters = new() { "david", "solomon" },
                Keywords = new() { "king", "power", "authority", "marriage", "politics", "covenant" },
                Source = "Mesopotamian king lists, Hittite treaties",
                RelevanceWeight = 8
            },
            
            // EXILE PERIOD (Jeremiah, Ezekiel, Daniel)
            new()
            {
                Title = "Babylonian Exile",
                Period = "Exile (586-538 BCE)",
                Category = "Politics",
                Content = "The Babylonian exile was catastrophic - temple destroyed (God's dwelling place), king captured and blinded (no Davidic ruler), city walls demolished (no protection), leading citizens deported (brain drain). Jews asked 'How can we sing the Lord's song in a foreign land?' because they believed God lived in Jerusalem. The exile forced a revolutionary question: Can God exist outside the promised land? This reshaped Judaism forever - synagogues, written scripture, and portable faith emerged.",
                RelatedCharacters = new() { "jeremiah", "ezekiel", "daniel" },
                Keywords = new() { "exile", "babylon", "temple", "captivity", "hope", "restoration" },
                Source = "Babylonian chronicles, Lachish letters",
                RelevanceWeight = 10
            },
            
            // ROMAN PERIOD (Jesus, disciples, Paul)
            new()
            {
                Title = "Roman Occupation",
                Period = "Second Temple / Roman Period (63 BCE - 70 CE)",
                Category = "Politics",
                Content = "Rome occupied Judea with military force. Jews paid crushing taxes to Rome AND temple taxes. Roman soldiers could legally compel Jews to carry their gear for one mile (hence 'go the second mile'). Crucifixion was Rome's tool for political rebels - it was public torture designed to terrorize occupied peoples. 'King of the Jews' on Jesus' cross was Rome's mockery: 'This is what happens to rebels.' Zealots wanted violent revolution; Jesus offered subversive peace.",
                RelatedCharacters = new() { "jesus", "peter", "john", "paul" },
                Keywords = new() { "rome", "occupation", "taxes", "crucifixion", "kingdom", "rebellion" },
                Source = "Josephus, Roman military records, Tacitus",
                RelevanceWeight = 10
            },
            new()
            {
                Title = "Pharisees, Sadducees, and Essenes",
                Period = "Second Temple Period",
                Category = "Religion",
                Content = "Jewish religious groups competed over how to be faithful: Sadducees (temple priests, aristocracy, collaborated with Rome, rejected oral law and resurrection), Pharisees (middle class, emphasized oral law and purity, believed in resurrection, popular with common people), Essenes (separatist commune, awaited apocalypse). Jesus criticized Pharisees not because they were worst, but because they were CLOSEST to truth yet missed the heart of the law. Sadducees disappear after 70 CE (temple destroyed); Pharisaic Judaism survives and becomes rabbinic Judaism.",
                RelatedCharacters = new() { "jesus", "paul", "peter", "john" },
                Keywords = new() { "pharisees", "sadducees", "law", "tradition", "purity", "religion" },
                Source = "Josephus, Dead Sea Scrolls, Mishnah",
                RelevanceWeight = 9
            },
            new()
            {
                Title = "Honor-Shame Culture",
                Period = "Ancient Mediterranean",
                Category = "Culture",
                Content = "Ancient Mediterranean culture operated on honor-shame (not guilt-innocence). Honor was public reputation; shame was public disgrace. 'Turn the other cheek' in this context: a backhanded slap (right hand to right cheek) was how superiors shamed inferiors. Offering the left cheek forces them to use a palm strike - treating you as an equal. Jesus taught dignity resistance, not passivity. Washing feet (servant's task) was shameful for a master. Jesus washing disciples' feet redefined greatness as service.",
                RelatedCharacters = new() { "jesus", "peter", "paul", "mary" },
                Keywords = new() { "honor", "shame", "dignity", "humility", "status", "culture" },
                Source = "Cultural anthropology, Mediterranean honor codes",
                RelevanceWeight = 8
            },
            new()
            {
                Title = "Women in Ancient Israel",
                Period = "All Periods",
                Category = "Social Structure",
                Content = "Women in ancient Israel had limited legal rights but more agency than often assumed. They could own property (Proverbs 31), engage in business, prophesy (Miriam, Deborah, Huldah). But they were excluded from temple worship's inner courts, couldn't testify in court, were under male authority (father, then husband). Jesus' treatment of women was revolutionary: speaking with Samaritan woman (John 4), teaching Mary while Martha served (Luke 10), appearing first to women after resurrection (making them the first evangelists). Paul's 'no male or female in Christ' was radical.",
                RelatedCharacters = new() { "mary", "jesus", "paul" },
                Keywords = new() { "women", "equality", "dignity", "rights", "culture", "radical" },
                Source = "Biblical law codes, archaeological evidence, rabbinic texts",
                RelevanceWeight = 7
            }
        };
    }
    
    private List<LanguageInsight> CreateInitialLanguageData()
    {
        return new List<LanguageInsight>
        {
            new()
            {
                Word = "peace",
                OriginalLanguage = "Hebrew",
                Transliteration = "shalom",
                StrongsNumber = "H7965",
                Definition = "Completeness, wholeness, harmony, prosperity, welfare, and tranquility",
                AlternateMeanings = new() { "wholeness", "prosperity", "well-being", "harmony", "completeness" },
                CulturalContext = "Shalom is much richer than absence of conflict - it means everything is as it should be. When you greet someone with 'Shalom,' you're blessing them with total flourishing. God's peace (shalom) means restoration of all things to their intended state.",
                ExampleVerses = new() { "Numbers 6:26", "Psalm 29:11", "Isaiah 26:3", "John 14:27" }
            },
            new()
            {
                Word = "love",
                OriginalLanguage = "Greek",
                Transliteration = "agape",
                StrongsNumber = "G26",
                Definition = "Unconditional, sacrificial, selfless love based on choice and commitment",
                AlternateMeanings = new() { "charity", "divine love", "unconditional love" },
                CulturalContext = "Greek had 4 words for love: eros (romantic), storge (family affection), phileo (friendship), agape (unconditional commitment). Agape was rare in classical Greek but became the Christian word for God's love - choosing to love regardless of response. When Jesus asked Peter 'Do you agape me?' and Peter answered 'I phileo you,' it showed Peter's honesty about his limits after denying Jesus.",
                ExampleVerses = new() { "John 3:16", "John 21:15-17", "Romans 5:8", "1 Corinthians 13", "1 John 4:8" }
            },
            new()
            {
                Word = "repent",
                OriginalLanguage = "Greek",
                Transliteration = "metanoia",
                StrongsNumber = "G3341",
                Definition = "A complete change of mind, heart, and direction; to turn around and go a different way",
                AlternateMeanings = new() { "change of mind", "turn around", "transformation", "conversion" },
                CulturalContext = "Meta = change, noia = mind. It's not just feeling sorry (that's remorse), it's a 180-degree turn. John the Baptist calling people to metanoia meant: stop going that direction, completely reverse course. It implies both recognition that your current path is wrong AND active decision to walk differently.",
                ExampleVerses = new() { "Matthew 3:2", "Mark 1:15", "Acts 2:38", "Acts 3:19" }
            },
            new()
            {
                Word = "faith",
                OriginalLanguage = "Greek",
                Transliteration = "pistis",
                StrongsNumber = "G4102",
                Definition = "Trust, confidence, belief, faithfulness, and commitment",
                AlternateMeanings = new() { "trust", "belief", "confidence", "faithfulness", "reliability" },
                CulturalContext = "Pistis means active trust, not just intellectual agreement. Hebrew 'emunah has the same root as 'amen' (firm, established). Biblical faith is leaning your full weight on God - like a child jumping into parent's arms. It's trust that produces action (James 2:17 - faith without works is dead).",
                ExampleVerses = new() { "Hebrews 11:1", "Romans 10:17", "Ephesians 2:8", "James 2:17" }
            },
            new()
            {
                Word = "grace",
                OriginalLanguage = "Greek",
                Transliteration = "charis",
                StrongsNumber = "G5485",
                Definition = "Unmerited favor, gift, kindness freely given",
                AlternateMeanings = new() { "favor", "gift", "kindness", "blessing", "generosity" },
                CulturalContext = "Charis is the root of 'charisma' (gift). It's favor you don't deserve and can't earn. In Roman culture, patrons showed charis to clients who would respond with loyalty. But God's charis expects nothing in return - it's purely gift. The scandal of the gospel is grace for the ungodly (Romans 5:8).",
                ExampleVerses = new() { "Ephesians 2:8-9", "Romans 3:24", "Romans 5:8", "2 Corinthians 12:9" }
            }
        };
    }
    
    private List<ThematicConnection> CreateInitialConnectionsData()
    {
        return new List<ThematicConnection>
        {
            new()
            {
                Theme = "Leadership Failure and Restoration",
                PrimaryPassage = "Numbers 20:1-13",
                SecondaryPassage = "John 21:15-19",
                ConnectionType = "Parallel",
                Insight = "Moses struck the rock in anger and never entered the Promised Land. Peter denied Jesus three times. But while Moses' failure was final in earthly terms, Peter was restored through Jesus' three questions 'Do you love me?' God's grace under the new covenant offers restoration that the law couldn't provide.",
                RelatedCharacters = new() { "moses", "peter", "jesus" }
            },
            new()
            {
                Theme = "From Murderer to Leader",
                PrimaryPassage = "Exodus 2:11-15",
                SecondaryPassage = "Acts 7:58-8:3, 9:1-19",
                ConnectionType = "Parallel",
                Insight = "Moses killed an Egyptian and fled to the wilderness for 40 years before God called him. Paul participated in Stephen's murder and persecuted the church before his Damascus road encounter. Both learned that God's timing involves preparation in wilderness/brokenness before calling to leadership.",
                RelatedCharacters = new() { "moses", "paul" }
            },
            new()
            {
                Theme = "Unlikely Messengers",
                PrimaryPassage = "Exodus 4:10-16",
                SecondaryPassage = "1 Corinthians 1:26-29",
                ConnectionType = "Echo",
                Insight = "Moses said 'I'm not eloquent, I'm slow of speech' yet God chose him. Paul wrote 'God chose the foolish things to shame the wise, the weak things to shame the strong.' God consistently chooses unlikely messengers so the power is clearly His, not theirs. Your weakness qualifies you.",
                RelatedCharacters = new() { "moses", "paul" }
            },
            new()
            {
                Theme = "Wilderness Testing",
                PrimaryPassage = "Exodus 16-17",
                SecondaryPassage = "Matthew 4:1-11",
                ConnectionType = "Fulfillment",
                Insight = "Israel spent 40 years in wilderness, complaining and failing tests. Jesus spent 40 days in wilderness, facing same temptations (turn stones to bread/manna, test God, worship other gods) but succeeded where Israel failed. Jesus relived Israel's story and got it right, becoming the faithful Israel.",
                RelatedCharacters = new() { "moses", "jesus" }
            },
            new()
            {
                Theme = "Substitutionary Intercession",
                PrimaryPassage = "Exodus 32:30-35",
                SecondaryPassage = "Romans 9:1-3",
                ConnectionType = "Echo",
                Insight = "Moses pleaded 'blot me out of your book' to save Israel after the golden calf. Paul wrote 'I could wish to be cut off from Christ' for the sake of his people. Both were willing to be cursed to save others. This foreshadows Christ who actually WAS cut off/cursed (Galatians 3:13) to save us.",
                RelatedCharacters = new() { "moses", "paul", "jesus" }
            },
            new()
            {
                Theme = "The Heart vs. The Law",
                PrimaryPassage = "Deuteronomy 10:16",
                SecondaryPassage = "Romans 2:28-29",
                ConnectionType = "Fulfillment",
                Insight = "Moses commanded 'circumcise your hearts' - recognizing that physical rituals weren't enough. Paul taught 'true circumcision is of the heart, by the Spirit.' The law itself pointed beyond external obedience to internal transformation, which only becomes possible through the Spirit.",
                RelatedCharacters = new() { "moses", "paul" }
            }
        };
    }
    
    private async Task SaveHistoricalContextsAsync()
    {
        var path = Path.Combine(_dataDirectory, "historical_context.json");
        var json = JsonSerializer.Serialize(_historicalContexts, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
    
    private async Task SaveLanguageInsightsAsync()
    {
        var path = Path.Combine(_dataDirectory, "language_insights.json");
        var json = JsonSerializer.Serialize(_languageInsights, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
    
    private async Task SaveThematicConnectionsAsync()
    {
        var path = Path.Combine(_dataDirectory, "thematic_connections.json");
        var json = JsonSerializer.Serialize(_thematicConnections, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
}
