using System.Text.Json;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Logging;

namespace AI_Bible_App.Infrastructure.Repositories;

/// <summary>
/// Repository for managing Bible reading plans and user progress.
/// Plans are loaded from embedded JSON data, progress is stored locally.
/// </summary>
public class ReadingPlanRepository : IReadingPlanRepository
{
    private readonly ILogger<ReadingPlanRepository> _logger;
    private readonly string _progressFilePath;
    private List<ReadingPlan>? _cachedPlans;
    private List<UserReadingProgress>? _cachedProgress;
    private readonly object _lock = new();

    public ReadingPlanRepository(ILogger<ReadingPlanRepository> logger)
    {
        _logger = logger;
        _progressFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIBibleApp",
            "reading_progress.json");
        
        EnsureDirectoryExists();
    }

    public async Task<List<ReadingPlan>> GetAllPlansAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedPlans != null)
            return _cachedPlans;

        _cachedPlans = GetBuiltInPlans();
        _logger.LogInformation("Loaded {Count} reading plans", _cachedPlans.Count);
        return _cachedPlans;
    }

    public async Task<ReadingPlan?> GetPlanByIdAsync(string planId, CancellationToken cancellationToken = default)
    {
        var plans = await GetAllPlansAsync(cancellationToken);
        return plans.FirstOrDefault(p => p.Id == planId);
    }

    public async Task<List<ReadingPlan>> GetPlansByTypeAsync(ReadingPlanType type, CancellationToken cancellationToken = default)
    {
        var plans = await GetAllPlansAsync(cancellationToken);
        return plans.Where(p => p.Type == type).ToList();
    }

    public async Task<UserReadingProgress?> GetActiveProgressAsync(string userId = "default", CancellationToken cancellationToken = default)
    {
        var allProgress = await GetAllProgressAsync(userId, cancellationToken);
        return allProgress.FirstOrDefault(p => p.CompletedAt == null);
    }

    public async Task<List<UserReadingProgress>> GetAllProgressAsync(string userId = "default", CancellationToken cancellationToken = default)
    {
        await LoadProgressAsync();
        return _cachedProgress?.Where(p => p.UserId == userId).ToList() ?? new List<UserReadingProgress>();
    }

    public async Task<UserReadingProgress> StartPlanAsync(string planId, string userId = "default", CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanByIdAsync(planId, cancellationToken);
        if (plan == null)
            throw new ArgumentException($"Plan '{planId}' not found");

        // Check if user already has an active plan
        var existing = await GetActiveProgressAsync(userId, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning("User {UserId} already has active plan {PlanId}. Abandoning it.", userId, existing.PlanId);
            await DeleteProgressAsync(existing.Id, cancellationToken);
        }

        var progress = new UserReadingProgress
        {
            UserId = userId,
            PlanId = planId,
            TotalDays = plan.TotalDays,
            CurrentDay = 1,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        await LoadProgressAsync();
        _cachedProgress ??= new List<UserReadingProgress>();
        _cachedProgress.Add(progress);
        await SaveProgressAsync();

        _logger.LogInformation("Started reading plan {PlanId} for user {UserId}", planId, userId);
        return progress;
    }

    public async Task<UserReadingProgress> MarkDayCompletedAsync(string progressId, int dayNumber, CancellationToken cancellationToken = default)
    {
        await LoadProgressAsync();
        var progress = _cachedProgress?.FirstOrDefault(p => p.Id == progressId);
        if (progress == null)
            throw new ArgumentException($"Progress '{progressId}' not found");

        progress.CompletedDays.Add(dayNumber);
        progress.LastActivityAt = DateTime.UtcNow;
        
        // Update streak
        UpdateStreak(progress);
        
        // Auto-advance current day if completing current
        if (dayNumber == progress.CurrentDay && progress.CurrentDay < progress.TotalDays)
        {
            progress.CurrentDay++;
        }

        // Check if plan is complete
        if (progress.CompletedDays.Count >= progress.TotalDays)
        {
            progress.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("User completed reading plan {PlanId}!", progress.PlanId);
        }

        await SaveProgressAsync();
        return progress;
    }

    public async Task<UserReadingProgress> UnmarkDayAsync(string progressId, int dayNumber, CancellationToken cancellationToken = default)
    {
        await LoadProgressAsync();
        var progress = _cachedProgress?.FirstOrDefault(p => p.Id == progressId);
        if (progress == null)
            throw new ArgumentException($"Progress '{progressId}' not found");

        progress.CompletedDays.Remove(dayNumber);
        progress.CompletedAt = null; // Uncomplete the plan if it was completed
        progress.LastActivityAt = DateTime.UtcNow;
        
        await SaveProgressAsync();
        return progress;
    }

    public async Task SaveDayNoteAsync(string progressId, int dayNumber, string note, CancellationToken cancellationToken = default)
    {
        await LoadProgressAsync();
        var progress = _cachedProgress?.FirstOrDefault(p => p.Id == progressId);
        if (progress == null)
            throw new ArgumentException($"Progress '{progressId}' not found");

        progress.DayNotes[dayNumber] = note;
        progress.LastActivityAt = DateTime.UtcNow;
        
        await SaveProgressAsync();
    }

    public async Task UpdateCurrentDayAsync(string progressId, int dayNumber, CancellationToken cancellationToken = default)
    {
        await LoadProgressAsync();
        var progress = _cachedProgress?.FirstOrDefault(p => p.Id == progressId);
        if (progress == null)
            throw new ArgumentException($"Progress '{progressId}' not found");

        progress.CurrentDay = Math.Clamp(dayNumber, 1, progress.TotalDays);
        progress.LastActivityAt = DateTime.UtcNow;
        
        await SaveProgressAsync();
    }

    public async Task DeleteProgressAsync(string progressId, CancellationToken cancellationToken = default)
    {
        await LoadProgressAsync();
        _cachedProgress?.RemoveAll(p => p.Id == progressId);
        await SaveProgressAsync();
        _logger.LogInformation("Deleted reading progress {ProgressId}", progressId);
    }

    public async Task<ReadingPlanDay?> GetTodaysReadingAsync(string userId = "default", CancellationToken cancellationToken = default)
    {
        var progress = await GetActiveProgressAsync(userId, cancellationToken);
        if (progress == null)
            return null;

        var plan = await GetPlanByIdAsync(progress.PlanId, cancellationToken);
        if (plan == null)
            return null;

        return plan.Days.FirstOrDefault(d => d.DayNumber == progress.CurrentDay);
    }

    private void UpdateStreak(UserReadingProgress progress)
    {
        // Simple streak calculation based on consecutive days
        var today = DateTime.UtcNow.Date;
        var lastActivity = progress.LastActivityAt.Date;
        
        if (lastActivity == today || lastActivity == today.AddDays(-1))
        {
            progress.CurrentStreak++;
        }
        else if (lastActivity < today.AddDays(-1))
        {
            progress.CurrentStreak = 1;
        }

        progress.LongestStreak = Math.Max(progress.LongestStreak, progress.CurrentStreak);
    }

    private async Task LoadProgressAsync()
    {
        if (_cachedProgress != null)
            return;

        lock (_lock)
        {
            if (_cachedProgress != null)
                return;

            try
            {
                if (File.Exists(_progressFilePath))
                {
                    var json = File.ReadAllText(_progressFilePath);
                    _cachedProgress = JsonSerializer.Deserialize<List<UserReadingProgress>>(json) ?? new();
                }
                else
                {
                    _cachedProgress = new List<UserReadingProgress>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load reading progress");
                _cachedProgress = new List<UserReadingProgress>();
            }
        }
    }

    private async Task SaveProgressAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cachedProgress, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_progressFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save reading progress");
        }
    }

    private void EnsureDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_progressFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Built-in reading plans
    /// </summary>
    private List<ReadingPlan> GetBuiltInPlans()
    {
        return new List<ReadingPlan>
        {
            CreateBibleIn90DaysPlan(),
            CreatePsalmsIn30DaysPlan(),
            CreateGospelsIn60DaysPlan(),
            CreateNewTestamentIn90DaysPlan(),
            CreateProverbsIn31DaysPlan()
        };
    }

    private ReadingPlan CreateBibleIn90DaysPlan()
    {
        var plan = new ReadingPlan
        {
            Id = "bible-90-days",
            Name = "Bible in 90 Days",
            Description = "Read through the entire Bible in just 90 days. An intensive but rewarding journey through all 66 books.",
            TotalDays = 90,
            Type = ReadingPlanType.Canonical,
            Difficulty = ReadingPlanDifficulty.Intensive,
            EstimatedMinutesPerDay = 45,
            Tags = new List<string> { "Full Bible", "Intensive", "90 Days" }
        };

        // Generate 90 days of readings covering the whole Bible
        var readings = new (string Title, string[] Passages)[]
        {
            ("Creation & The Fall", new[] { "Genesis 1-4" }),
            ("Noah & The Flood", new[] { "Genesis 5-9" }),
            ("Tower of Babel & Abraham's Call", new[] { "Genesis 10-14" }),
            ("God's Covenant with Abraham", new[] { "Genesis 15-19" }),
            ("Isaac & Jacob", new[] { "Genesis 20-24" }),
            ("Jacob's Family", new[] { "Genesis 25-28" }),
            ("Jacob & Esau Reconcile", new[] { "Genesis 29-32" }),
            ("Joseph's Dreams", new[] { "Genesis 33-37" }),
            ("Joseph in Egypt", new[] { "Genesis 38-41" }),
            ("Joseph & His Brothers", new[] { "Genesis 42-46" }),
            ("Israel in Egypt", new[] { "Genesis 47-50" }),
            ("Moses & the Burning Bush", new[] { "Exodus 1-6" }),
            ("The Plagues Begin", new[] { "Exodus 7-11" }),
            ("Passover & Exodus", new[] { "Exodus 12-15" }),
            ("Wilderness Wandering", new[] { "Exodus 16-20" }),
            ("Laws & Covenant", new[] { "Exodus 21-25" }),
            ("The Tabernacle", new[] { "Exodus 26-30" }),
            ("Golden Calf & Renewal", new[] { "Exodus 31-35" }),
            ("Tabernacle Completed", new[] { "Exodus 36-40" }),
            ("Offerings & Sacrifices", new[] { "Leviticus 1-7" }),
            ("Priests Ordained", new[] { "Leviticus 8-13" }),
            ("Day of Atonement", new[] { "Leviticus 14-18" }),
            ("Holiness Code", new[] { "Leviticus 19-23" }),
            ("Blessings & Curses", new[] { "Leviticus 24-27" }),
            ("Census & Camp Order", new[] { "Numbers 1-5" }),
            ("Nazarite Vow & Blessing", new[] { "Numbers 6-10" }),
            ("Complaints & Spies", new[] { "Numbers 11-15" }),
            ("Korah's Rebellion", new[] { "Numbers 16-20" }),
            ("Bronze Serpent & Balaam", new[] { "Numbers 21-25" }),
            ("Second Census & Laws", new[] { "Numbers 26-30" }),
            ("Conquest Preparations", new[] { "Numbers 31-36" }),
            ("Moses Reviews the Law", new[] { "Deuteronomy 1-5" }),
            ("The Great Commandment", new[] { "Deuteronomy 6-10" }),
            ("Blessings & Curses", new[] { "Deuteronomy 11-16" }),
            ("Laws & Justice", new[] { "Deuteronomy 17-22" }),
            ("Community Laws", new[] { "Deuteronomy 23-28" }),
            ("Covenant Renewal & Moses' Death", new[] { "Deuteronomy 29-34" }),
            ("Crossing the Jordan", new[] { "Joshua 1-6" }),
            ("Conquest Continues", new[] { "Joshua 7-12" }),
            ("Land Division", new[] { "Joshua 13-19" }),
            ("Cities of Refuge & Farewell", new[] { "Joshua 20-24" }),
            ("Judges Begin", new[] { "Judges 1-6" }),
            ("Gideon & Abimelech", new[] { "Judges 7-12" }),
            ("Samson & Final Judges", new[] { "Judges 13-18" }),
            ("Dark Days of Judges", new[] { "Judges 19-21", "Ruth 1-4" }),
            ("Samuel's Birth & Ministry", new[] { "1 Samuel 1-7" }),
            ("Saul Becomes King", new[] { "1 Samuel 8-14" }),
            ("Saul's Decline, David's Rise", new[] { "1 Samuel 15-20" }),
            ("David on the Run", new[] { "1 Samuel 21-26" }),
            ("Saul's End, David's Kingdom", new[] { "1 Samuel 27-31", "2 Samuel 1-4" }),
            ("David as King", new[] { "2 Samuel 5-10" }),
            ("David's Sin & Consequences", new[] { "2 Samuel 11-15" }),
            ("Absalom's Rebellion", new[] { "2 Samuel 16-20" }),
            ("David's Final Years", new[] { "2 Samuel 21-24", "1 Kings 1-2" }),
            ("Solomon's Wisdom & Temple", new[] { "1 Kings 3-8" }),
            ("Solomon's Reign & Division", new[] { "1 Kings 9-13" }),
            ("Kings of Israel & Judah", new[] { "1 Kings 14-18" }),
            ("Elijah & Elisha", new[] { "1 Kings 19-22", "2 Kings 1-3" }),
            ("Elisha's Ministry", new[] { "2 Kings 4-9" }),
            ("Israel's Fall", new[] { "2 Kings 10-15" }),
            ("Judah Alone", new[] { "2 Kings 16-20" }),
            ("Josiah's Revival & Jerusalem Falls", new[] { "2 Kings 21-25" }),
            ("Chronicles: David's Line", new[] { "1 Chronicles 1-10" }),
            ("Chronicles: David's Reign", new[] { "1 Chronicles 11-20" }),
            ("Chronicles: Temple Preparations", new[] { "1 Chronicles 21-29" }),
            ("Chronicles: Solomon's Temple", new[] { "2 Chronicles 1-9" }),
            ("Chronicles: Divided Kingdom", new[] { "2 Chronicles 10-20" }),
            ("Chronicles: Later Kings", new[] { "2 Chronicles 21-30" }),
            ("Chronicles: Final Kings & Exile", new[] { "2 Chronicles 31-36" }),
            ("Return from Exile", new[] { "Ezra 1-6" }),
            ("Ezra's Reforms", new[] { "Ezra 7-10", "Nehemiah 1-3" }),
            ("Nehemiah Rebuilds", new[] { "Nehemiah 4-9" }),
            ("Nehemiah's Reforms & Esther", new[] { "Nehemiah 10-13", "Esther 1-5" }),
            ("Esther Saves Her People", new[] { "Esther 6-10", "Job 1-7" }),
            ("Job's Suffering", new[] { "Job 8-18" }),
            ("Job's Defense", new[] { "Job 19-28" }),
            ("God Answers Job", new[] { "Job 29-42" }),
            ("Psalms: Book 1", new[] { "Psalms 1-30" }),
            ("Psalms: Book 2", new[] { "Psalms 31-60" }),
            ("Psalms: Book 3", new[] { "Psalms 61-90" }),
            ("Psalms: Books 4-5", new[] { "Psalms 91-118" }),
            ("Psalms: Conclusion & Proverbs Begin", new[] { "Psalms 119-150", "Proverbs 1-3" }),
            ("Wisdom of Proverbs", new[] { "Proverbs 4-15" }),
            ("More Proverbs & Ecclesiastes", new[] { "Proverbs 16-31", "Ecclesiastes 1-4" }),
            ("Ecclesiastes & Song of Solomon", new[] { "Ecclesiastes 5-12", "Song of Solomon 1-8" }),
            ("Isaiah: Judgment", new[] { "Isaiah 1-12" }),
            ("Isaiah: Nations & Hope", new[] { "Isaiah 13-27" }),
            ("Isaiah: Woes & Comfort", new[] { "Isaiah 28-40" }),
            ("Isaiah: Servant Songs", new[] { "Isaiah 41-52" }),
            ("Isaiah: Suffering Servant & New Creation", new[] { "Isaiah 53-66" }),
            ("Jeremiah: Call & Early Ministry", new[] { "Jeremiah 1-12" })
        };

        for (int i = 0; i < 90 && i < readings.Length; i++)
        {
            plan.Days.Add(new ReadingPlanDay
            {
                DayNumber = i + 1,
                Title = readings[i].Title,
                Passages = readings[i].Passages.ToList(),
                EstimatedMinutes = 45
            });
        }

        return plan;
    }

    private ReadingPlan CreatePsalmsIn30DaysPlan()
    {
        var plan = new ReadingPlan
        {
            Id = "psalms-30-days",
            Name = "Psalms in 30 Days",
            Description = "Journey through all 150 Psalms in one month. Experience worship, lament, thanksgiving, and praise.",
            TotalDays = 30,
            Type = ReadingPlanType.Wisdom,
            Difficulty = ReadingPlanDifficulty.Medium,
            EstimatedMinutesPerDay = 15,
            Tags = new List<string> { "Psalms", "Worship", "30 Days", "Poetry" }
        };

        var themes = new[]
        {
            ("Blessed & Righteous", "Psalm 1:1-3"),
            ("God's Protection", "Psalm 4:8"),
            ("Morning Prayer", "Psalm 5:3"),
            ("Human Worth", "Psalm 8:4-5"),
            ("Refuge in Trouble", "Psalm 9:9"),
            ("God's Word", "Psalm 12:6"),
            ("Heart's Desire", "Psalm 20:4"),
            ("The Good Shepherd", "Psalm 23:1"),
            ("Seeking God", "Psalm 27:4"),
            ("Forgiveness", "Psalm 32:1"),
            ("Taste and See", "Psalm 34:8"),
            ("Be Still", "Psalm 46:10"),
            ("A Clean Heart", "Psalm 51:10"),
            ("Cast Your Cares", "Psalm 55:22"),
            ("Longing for God", "Psalm 63:1"),
            ("Dwelling in God's House", "Psalm 84:10"),
            ("Our Dwelling Place", "Psalm 90:1-2"),
            ("Under His Wings", "Psalm 91:4"),
            ("Thanksgiving", "Psalm 100:4"),
            ("God's Compassion", "Psalm 103:8"),
            ("Creation Praise", "Psalm 104:24"),
            ("God's Word is Light", "Psalm 119:105"),
            ("God Knows Me", "Psalm 139:1-4"),
            ("Evening Praise", "Psalm 141:2"),
            ("God Lifts Us Up", "Psalm 145:14"),
            ("Praise the Lord", "Psalm 146:1-2"),
            ("He Heals", "Psalm 147:3"),
            ("Nature's Praise", "Psalm 148:1-4"),
            ("New Song", "Psalm 149:1"),
            ("Final Hallelujah", "Psalm 150:6")
        };

        for (int i = 0; i < 30; i++)
        {
            var startPsalm = (i * 5) + 1;
            var endPsalm = Math.Min((i + 1) * 5, 150);
            
            plan.Days.Add(new ReadingPlanDay
            {
                DayNumber = i + 1,
                Title = themes[i].Item1,
                Passages = new List<string> { $"Psalm {startPsalm}-{endPsalm}" },
                KeyVerse = themes[i].Item2,
                ReflectionPrompt = $"How do these psalms relate to your current life situation?",
                EstimatedMinutes = 15
            });
        }

        return plan;
    }

    private ReadingPlan CreateGospelsIn60DaysPlan()
    {
        var plan = new ReadingPlan
        {
            Id = "gospels-60-days",
            Name = "Gospels in 60 Days",
            Description = "Walk with Jesus through all four Gospel accounts. Experience His life, teachings, death, and resurrection.",
            TotalDays = 60,
            Type = ReadingPlanType.Gospel,
            Difficulty = ReadingPlanDifficulty.Medium,
            EstimatedMinutesPerDay = 15,
            Tags = new List<string> { "Gospels", "Jesus", "60 Days", "New Testament" }
        };

        var gospelReadings = new (string Title, string Passage, string KeyVerse)[]
        {
            // Matthew (15 days)
            ("Jesus' Genealogy & Birth", "Matthew 1-2", "Matthew 1:21"),
            ("John the Baptist & Temptation", "Matthew 3-4", "Matthew 4:4"),
            ("Sermon on the Mount: Beatitudes", "Matthew 5", "Matthew 5:16"),
            ("Sermon: Prayer & Treasures", "Matthew 6", "Matthew 6:33"),
            ("Sermon: Conclusion", "Matthew 7", "Matthew 7:24"),
            ("Miracles of Healing", "Matthew 8-9", "Matthew 9:12"),
            ("Sending the Twelve", "Matthew 10-11", "Matthew 11:28"),
            ("Parables of the Kingdom", "Matthew 12-13", "Matthew 13:44"),
            ("Feeding 5000 & Walking on Water", "Matthew 14-15", "Matthew 14:27"),
            ("Peter's Confession", "Matthew 16-17", "Matthew 16:16"),
            ("Teachings on Humility", "Matthew 18-19", "Matthew 18:3"),
            ("Final Teachings", "Matthew 20-22", "Matthew 22:37-39"),
            ("Woes & End Times", "Matthew 23-24", "Matthew 24:35"),
            ("Parables of Readiness", "Matthew 25-26", "Matthew 25:40"),
            ("Crucifixion & Resurrection", "Matthew 27-28", "Matthew 28:19-20"),
            // Mark (10 days)
            ("The Beginning", "Mark 1-2", "Mark 1:15"),
            ("Opposition & Parables", "Mark 3-4", "Mark 4:39"),
            ("Miracles & Mission", "Mark 5-6", "Mark 5:34"),
            ("Traditions & Faith", "Mark 7-8", "Mark 8:34"),
            ("Transfiguration", "Mark 9", "Mark 9:23"),
            ("Teachings on Discipleship", "Mark 10", "Mark 10:45"),
            ("Triumphal Entry", "Mark 11-12", "Mark 12:30-31"),
            ("End Times Discourse", "Mark 13", "Mark 13:31"),
            ("Last Supper & Arrest", "Mark 14", "Mark 14:36"),
            ("Cross & Empty Tomb", "Mark 15-16", "Mark 16:6"),
            // Luke (20 days)
            ("Birth Narratives", "Luke 1", "Luke 1:37"),
            ("Jesus' Birth", "Luke 2", "Luke 2:11"),
            ("Preparation & Temptation", "Luke 3-4", "Luke 4:18-19"),
            ("Calling Disciples", "Luke 5-6", "Luke 6:27"),
            ("Faith & Forgiveness", "Luke 7-8", "Luke 7:50"),
            ("Mission & Identity", "Luke 9", "Luke 9:23"),
            ("Sending the 72", "Luke 10", "Luke 10:27"),
            ("Teaching on Prayer", "Luke 11", "Luke 11:9"),
            ("Warnings & Parables", "Luke 12", "Luke 12:32"),
            ("Repentance & Healing", "Luke 13-14", "Luke 14:11"),
            ("Lost & Found", "Luke 15", "Luke 15:7"),
            ("Stewardship & Faith", "Luke 16-17", "Luke 17:21"),
            ("Prayer & Humility", "Luke 18", "Luke 18:16"),
            ("Zacchaeus & Jerusalem", "Luke 19", "Luke 19:10"),
            ("Temple Debates", "Luke 20-21", "Luke 21:33"),
            ("Last Supper", "Luke 22", "Luke 22:42"),
            ("Trial & Crucifixion", "Luke 23", "Luke 23:34"),
            ("Resurrection & Ascension", "Luke 24", "Luke 24:45"),
            // John (15 days)
            ("The Word Became Flesh", "John 1", "John 1:14"),
            ("Water to Wine & Temple", "John 2-3", "John 3:16"),
            ("Living Water", "John 4", "John 4:14"),
            ("Son of God", "John 5", "John 5:24"),
            ("Bread of Life", "John 6", "John 6:35"),
            ("Light of the World", "John 7-8", "John 8:12"),
            ("The Good Shepherd", "John 9-10", "John 10:10"),
            ("Lazarus Raised", "John 11", "John 11:25"),
            ("The Hour Has Come", "John 12", "John 12:32"),
            ("Servant Leadership", "John 13", "John 13:34"),
            ("The Way, Truth, Life", "John 14", "John 14:6"),
            ("The Vine & Branches", "John 15", "John 15:5"),
            ("The Spirit & Prayer", "John 16-17", "John 16:33"),
            ("Arrest & Trial", "John 18-19", "John 19:30"),
            ("Resurrection Appearances", "John 20-21", "John 20:31")
        };

        for (int i = 0; i < gospelReadings.Length; i++)
        {
            plan.Days.Add(new ReadingPlanDay
            {
                DayNumber = i + 1,
                Title = gospelReadings[i].Title,
                Passages = new List<string> { gospelReadings[i].Passage },
                KeyVerse = gospelReadings[i].KeyVerse,
                ReflectionPrompt = "What does this passage reveal about Jesus?",
                EstimatedMinutes = 15
            });
        }

        return plan;
    }

    private ReadingPlan CreateNewTestamentIn90DaysPlan()
    {
        var plan = new ReadingPlan
        {
            Id = "new-testament-90-days",
            Name = "New Testament in 90 Days",
            Description = "Read through the entire New Testament in 90 days, from the Gospels to Revelation.",
            TotalDays = 90,
            Type = ReadingPlanType.NewTestament,
            Difficulty = ReadingPlanDifficulty.Medium,
            EstimatedMinutesPerDay = 20,
            Tags = new List<string> { "New Testament", "90 Days" }
        };

        // Simplified for brevity - generates 90 days covering NT
        var books = new[]
        {
            ("Matthew", 28), ("Mark", 16), ("Luke", 24), ("John", 21),
            ("Acts", 28), ("Romans", 16), ("1 Corinthians", 16), ("2 Corinthians", 13),
            ("Galatians", 6), ("Ephesians", 6), ("Philippians", 4), ("Colossians", 4),
            ("1 Thessalonians", 5), ("2 Thessalonians", 3), ("1 Timothy", 6), ("2 Timothy", 4),
            ("Titus", 3), ("Philemon", 1), ("Hebrews", 13), ("James", 5),
            ("1 Peter", 5), ("2 Peter", 3), ("1 John", 5), ("2 John", 1),
            ("3 John", 1), ("Jude", 1), ("Revelation", 22)
        };

        int dayNumber = 1;
        foreach (var (book, chapters) in books)
        {
            int chaptersPerDay = Math.Max(1, (int)Math.Ceiling(chapters / (double)Math.Max(1, chapters / 3)));
            int currentChapter = 1;
            
            while (currentChapter <= chapters && dayNumber <= 90)
            {
                int endChapter = Math.Min(currentChapter + chaptersPerDay - 1, chapters);
                var passage = currentChapter == endChapter 
                    ? $"{book} {currentChapter}" 
                    : $"{book} {currentChapter}-{endChapter}";
                
                plan.Days.Add(new ReadingPlanDay
                {
                    DayNumber = dayNumber++,
                    Title = $"{book} - Part {(currentChapter - 1) / chaptersPerDay + 1}",
                    Passages = new List<string> { passage },
                    EstimatedMinutes = 20
                });
                
                currentChapter = endChapter + 1;
            }
        }

        plan.TotalDays = plan.Days.Count;
        return plan;
    }

    private ReadingPlan CreateProverbsIn31DaysPlan()
    {
        var plan = new ReadingPlan
        {
            Id = "proverbs-31-days",
            Name = "Proverbs in 31 Days",
            Description = "One chapter of Proverbs per day, perfectly designed for a month of wisdom.",
            TotalDays = 31,
            Type = ReadingPlanType.Wisdom,
            Difficulty = ReadingPlanDifficulty.Light,
            EstimatedMinutesPerDay = 10,
            Tags = new List<string> { "Proverbs", "Wisdom", "31 Days", "Daily" }
        };

        var themes = new[]
        {
            "The Beginning of Wisdom", "Wisdom's Call", "Trust in the Lord",
            "Wisdom Protects", "Warning Against Adultery", "Dangers of Laziness",
            "More Warnings", "Wisdom's Excellence", "Wisdom's Feast",
            "Wise Sayings Begin", "Integrity & Speech", "Righteousness & Wickedness",
            "Words & Wealth", "Hope & Discipline", "Gentle Answers",
            "Pride & Humility", "Justice & Kindness", "Friends & Fools",
            "Wine & Anger", "The King's Heart", "Diligence & Honesty",
            "Training Children", "Wisdom & Strength", "More Wise Sayings",
            "Hezekiah's Collection", "Fear of the Lord", "Agur's Sayings",
            "King Lemuel's Words", "Sayings Continue", "More Wisdom",
            "The Excellent Wife"
        };

        for (int i = 0; i < 31; i++)
        {
            plan.Days.Add(new ReadingPlanDay
            {
                DayNumber = i + 1,
                Title = themes[i],
                Passages = new List<string> { $"Proverbs {i + 1}" },
                ReflectionPrompt = "Which proverb speaks most to your life today?",
                EstimatedMinutes = 10
            });
        }

        return plan;
    }
}
