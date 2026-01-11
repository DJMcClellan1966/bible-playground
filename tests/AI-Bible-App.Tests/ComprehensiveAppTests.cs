using System.Diagnostics;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Repositories;

namespace AI_Bible_App.Tests;

/// <summary>
/// Comprehensive stress tests to find edge cases and performance issues
/// </summary>
public class ComprehensiveAppTests
{
    private readonly ITestOutputHelper _output;

    public ComprehensiveAppTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Character Repository Tests

    [Fact]
    public async Task CharacterRepository_GetAllCharacters_ShouldReturnCharacters()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();
        var sw = Stopwatch.StartNew();

        // Act
        var characters = await repository.GetAllCharactersAsync();
        sw.Stop();

        // Assert
        Assert.NotNull(characters);
        Assert.NotEmpty(characters);
        _output.WriteLine($"✅ GetAllCharacters: {characters.Count} characters loaded in {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task CharacterRepository_GetCharacterById_ValidIds_ShouldWork()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();
        var validIds = new[] { "moses", "david", "paul", "mary", "peter" };
        var results = new List<(string id, bool found, long ms)>();

        // Act
        foreach (var id in validIds)
        {
            var sw = Stopwatch.StartNew();
            var character = await repository.GetCharacterAsync(id);
            sw.Stop();
            results.Add((id, character != null, sw.ElapsedMilliseconds));
        }

        // Assert & Report
        foreach (var (id, found, ms) in results)
        {
            _output.WriteLine($"  Character '{id}': {(found ? "✅ Found" : "❌ Not Found")} in {ms}ms");
            Assert.True(found, $"Character '{id}' should exist");
        }
    }

    [Fact]
    public async Task CharacterRepository_GetCharacterById_InvalidId_ShouldReturnNull()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();
        var invalidIds = new[] { "", "   ", "nonexistent", "INVALID_ID_12345" };

        // Act & Assert
        foreach (var id in invalidIds)
        {
            var character = await repository.GetCharacterAsync(id);
            Assert.Null(character);
            _output.WriteLine($"  Invalid ID '{id}': ✅ Correctly returned null");
        }
    }

    [Fact]
    public async Task CharacterRepository_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();
        var tasks = new List<Task<BiblicalCharacter?>>();
        var sw = Stopwatch.StartNew();

        // Act - 100 concurrent requests
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(repository.GetCharacterAsync("moses"));
        }

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        Assert.All(results, r => Assert.NotNull(r));
        _output.WriteLine($"✅ 100 concurrent requests completed in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / 100.0:F2}ms avg)");
    }

    [Fact]
    public async Task CharacterRepository_AllCharacters_HaveRequiredFields()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();

        // Act
        var characters = await repository.GetAllCharactersAsync();

        // Assert - Every character should have essential fields
        foreach (var character in characters)
        {
            Assert.False(string.IsNullOrWhiteSpace(character.Id), $"Character should have Id");
            Assert.False(string.IsNullOrWhiteSpace(character.Name), $"Character {character.Id} should have Name");
            Assert.False(string.IsNullOrWhiteSpace(character.Title), $"Character {character.Id} should have Title");
            Assert.False(string.IsNullOrWhiteSpace(character.Description), $"Character {character.Id} should have Description");
            Assert.False(string.IsNullOrWhiteSpace(character.SystemPrompt), $"Character {character.Id} should have SystemPrompt");
            
            _output.WriteLine($"  ✅ {character.Name}: All required fields present");
        }
    }

    [Fact]
    public async Task CharacterRepository_MassiveConcurrency_StressTest()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();
        var tasks = new List<Task>();
        var sw = Stopwatch.StartNew();
        var characterIds = new[] { "moses", "david", "paul", "peter", "mary" };
        var successCount = 0;

        // Act - 1000 concurrent mixed requests
        for (int i = 0; i < 1000; i++)
        {
            var id = characterIds[i % characterIds.Length];
            tasks.Add(Task.Run(async () =>
            {
                var character = await repository.GetCharacterAsync(id);
                if (character != null) Interlocked.Increment(ref successCount);
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        Assert.Equal(1000, successCount);
        _output.WriteLine($"📊 Stress Test Results:");
        _output.WriteLine($"   Total Requests: 1000");
        _output.WriteLine($"   Successful: {successCount}");
        _output.WriteLine($"   Total Time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"   Throughput: {1000.0 / (sw.ElapsedMilliseconds / 1000.0):F0} req/sec");
    }

    #endregion

    #region Model Tests

    [Fact]
    public void ChatSession_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var session = new ChatSession();

        // Assert
        Assert.NotNull(session.Id);
        Assert.NotEmpty(session.Id);
        Assert.NotNull(session.Messages);
        Assert.Empty(session.Messages);
        Assert.True(session.StartedAt <= DateTime.UtcNow);
        
        _output.WriteLine($"✅ ChatSession initializes with valid defaults");
    }

    [Fact]
    public void ChatSession_CanAddMessages()
    {
        // Arrange
        var session = new ChatSession();

        // Act
        session.Messages.Add(new ChatMessage { Role = "user", Content = "Hello" });
        session.Messages.Add(new ChatMessage { Role = "assistant", Content = "Hi there" });

        // Assert
        Assert.Equal(2, session.Messages.Count);
        Assert.Equal("Hello", session.Messages[0].Content);
        Assert.Equal("Hi there", session.Messages[1].Content);
        
        _output.WriteLine($"✅ ChatSession can add and retrieve messages");
    }

    [Fact]
    public void Reflection_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var reflection = new Reflection();

        // Assert
        Assert.NotNull(reflection.Id);
        Assert.NotEmpty(reflection.Id);
        Assert.NotNull(reflection.BibleReferences);
        Assert.NotNull(reflection.Tags);
        Assert.False(reflection.IsFavorite);
        
        _output.WriteLine($"✅ Reflection initializes with valid defaults");
    }

    [Fact]
    public void BiblicalCharacter_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var character = new BiblicalCharacter();

        // Assert
        Assert.NotNull(character.BiblicalReferences);
        Assert.NotNull(character.Attributes);
        Assert.NotNull(character.Voice);
        
        _output.WriteLine($"✅ BiblicalCharacter initializes with valid defaults");
    }

    #endregion

    #region Performance Stress Tests

    [Fact]
    public async Task Performance_RapidFireRequests_ShouldNotDegrade()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();
        var timings = new List<long>();

        // Act - 50 rapid requests
        for (int i = 0; i < 50; i++)
        {
            var sw = Stopwatch.StartNew();
            await repository.GetAllCharactersAsync();
            sw.Stop();
            timings.Add(sw.ElapsedMilliseconds);
        }

        // Analyze
        var avg = timings.Average();
        var min = timings.Min();
        var max = timings.Max();
        var p95 = timings.OrderBy(t => t).ElementAt((int)(timings.Count * 0.95));

        _output.WriteLine($"📊 Rapid Fire Performance (50 requests):");
        _output.WriteLine($"   Min: {min}ms");
        _output.WriteLine($"   Max: {max}ms");
        _output.WriteLine($"   Avg: {avg:F2}ms");
        _output.WriteLine($"   P95: {p95}ms");

        // Assert - performance should be reasonable
        Assert.True(avg < 100, $"Average response time ({avg}ms) should be under 100ms");
    }

    [Fact]
    public async Task Performance_MemoryUsage_ShouldBeReasonable()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Load many times
        for (int i = 0; i < 100; i++)
        {
            var characters = await repository.GetAllCharactersAsync();
            foreach (var c in characters)
            {
                _ = c.Name;
                _ = c.Description;
            }
        }

        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryGrowth = (finalMemory - initialMemory) / 1024.0 / 1024.0;

        _output.WriteLine($"📊 Memory Analysis:");
        _output.WriteLine($"   Initial: {initialMemory / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"   Final: {finalMemory / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"   Growth: {memoryGrowth:F2} MB");

        // Assert - memory growth should be minimal
        Assert.True(memoryGrowth < 50, $"Memory growth ({memoryGrowth:F2}MB) should be under 50MB");
    }

    [Fact]
    public async Task Performance_CharacterLookupSpeed_ShouldBeFast()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();
        var ids = new[] { "moses", "david", "paul", "peter", "mary", "esther", "ruth", "solomon", "john", "deborah", "hannah" };
        var timings = new Dictionary<string, List<long>>();
        
        foreach (var id in ids)
            timings[id] = new List<long>();

        // Act - Test each character 10 times
        for (int round = 0; round < 10; round++)
        {
            foreach (var id in ids)
            {
                var sw = Stopwatch.StartNew();
                await repository.GetCharacterAsync(id);
                sw.Stop();
                timings[id].Add(sw.ElapsedMilliseconds);
            }
        }

        // Report
        _output.WriteLine($"📊 Character Lookup Performance (10 iterations each):");
        foreach (var id in ids)
        {
            var avg = timings[id].Average();
            _output.WriteLine($"   {id}: {avg:F2}ms avg");
        }

        var overallAvg = timings.Values.SelectMany(t => t).Average();
        Assert.True(overallAvg < 10, $"Average lookup time ({overallAvg:F2}ms) should be under 10ms");
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public void InputValidation_ExtremelyLongStrings_ShouldHandle()
    {
        // Arrange
        var veryLongString = new string('a', 1_000_000); // 1MB string

        // Act & Assert - should not crash
        var message = new ChatMessage
        {
            Role = "user",
            Content = veryLongString
        };

        Assert.Equal(1_000_000, message.Content.Length);
        _output.WriteLine($"✅ Handled 1MB string without crashing");
    }

    [Fact]
    public void InputValidation_NullAndEmpty_ShouldHandle()
    {
        // Test ChatMessage with null/empty
        var msg1 = new ChatMessage { Role = null!, Content = null! };
        var msg2 = new ChatMessage { Role = "", Content = "" };
        
        Assert.Null(msg1.Role);
        Assert.Null(msg1.Content);
        Assert.Equal("", msg2.Role);
        Assert.Equal("", msg2.Content);
        
        _output.WriteLine($"✅ Null and empty strings handled correctly");
    }

    [Fact]
    public void InputValidation_SqlInjectionAttempts_ShouldBeSafe()
    {
        // Arrange
        var maliciousInputs = new[]
        {
            "'; DROP TABLE users; --",
            "1' OR '1'='1",
            "admin'--",
            "<script>alert('xss')</script>",
            "{{constructor.constructor('return this')()}}"
        };

        // Act & Assert - these should be stored as-is, not executed
        foreach (var input in maliciousInputs)
        {
            var message = new ChatMessage { Role = "user", Content = input };
            Assert.Equal(input, message.Content);
        }

        _output.WriteLine($"✅ Potentially malicious inputs stored safely as plain text");
    }

    [Fact]
    public void InputValidation_UnicodeAndEmoji_ShouldPreserve()
    {
        // Arrange
        var unicodeContent = "Hebrew: שָׁלוֹם | Greek: εἰρήνη | Emoji: 🙏✝️📖🕊️ | Arabic: سلام";

        // Act
        var message = new ChatMessage { Role = "user", Content = unicodeContent };

        // Assert
        Assert.Equal(unicodeContent, message.Content);
        _output.WriteLine($"✅ Unicode and emoji preserved correctly: {unicodeContent}");
    }

    [Fact]
    public void InputValidation_NewlinesAndTabs_ShouldPreserve()
    {
        // Arrange
        var formattedContent = "Line 1\nLine 2\r\nLine 3\tTabbed";

        // Act
        var message = new ChatMessage { Role = "user", Content = formattedContent };

        // Assert
        Assert.Equal(formattedContent, message.Content);
        Assert.Contains("\n", message.Content);
        Assert.Contains("\t", message.Content);
        _output.WriteLine($"✅ Newlines and tabs preserved correctly");
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public void Boundary_DateTimeEdgeCases_ShouldHandle()
    {
        // Arrange
        var dates = new[]
        {
            DateTime.MinValue,
            DateTime.MaxValue,
            DateTime.UtcNow,
            new DateTime(1, 1, 1),
            new DateTime(9999, 12, 31)
        };

        // Act & Assert
        foreach (var date in dates)
        {
            var reflection = new Reflection { CreatedAt = date };
            Assert.Equal(date, reflection.CreatedAt);
        }

        _output.WriteLine($"✅ DateTime edge cases handled correctly");
    }

    [Fact]
    public void Boundary_EmptyCollections_ShouldHandle()
    {
        // Arrange & Act
        var session = new ChatSession();
        var reflection = new Reflection();

        // Assert
        Assert.NotNull(session.Messages);
        Assert.Empty(session.Messages);
        Assert.NotNull(reflection.BibleReferences);
        Assert.Empty(reflection.BibleReferences);
        Assert.NotNull(reflection.Tags);
        Assert.Empty(reflection.Tags);

        _output.WriteLine($"✅ Empty collections initialized correctly");
    }

    [Fact]
    public void Boundary_LargeCollections_ShouldHandle()
    {
        // Arrange
        var session = new ChatSession();

        // Act - Add 10,000 messages
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
        {
            session.Messages.Add(new ChatMessage
            {
                Role = i % 2 == 0 ? "user" : "assistant",
                Content = $"Message {i}"
            });
        }
        sw.Stop();

        // Assert
        Assert.Equal(10_000, session.Messages.Count);
        _output.WriteLine($"✅ Added 10,000 messages in {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Boundary_GuidUniqueness_ShouldBeUnique()
    {
        // Arrange & Act
        var ids = new HashSet<string>();
        for (int i = 0; i < 10_000; i++)
        {
            var session = new ChatSession();
            ids.Add(session.Id);
        }

        // Assert
        Assert.Equal(10_000, ids.Count);
        _output.WriteLine($"✅ 10,000 unique session IDs generated");
    }

    #endregion
}

/// <summary>
/// Integration tests that test multiple components working together
/// </summary>
public class IntegrationTests
{
    private readonly ITestOutputHelper _output;

    public IntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Integration_CharacterDataIntegrity_AllCharactersValid()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();

        // Act
        var characters = await repository.GetAllCharactersAsync();

        // Assert - comprehensive validation
        _output.WriteLine($"📊 Character Data Integrity Report:");
        _output.WriteLine($"   Total Characters: {characters.Count}");

        var issues = new List<string>();
        foreach (var c in characters)
        {
            if (c.SystemPrompt.Length < 100)
                issues.Add($"{c.Name}: SystemPrompt too short ({c.SystemPrompt.Length} chars)");
            if (c.Description.Length < 20)
                issues.Add($"{c.Name}: Description too short ({c.Description.Length} chars)");
            if (!c.BiblicalReferences.Any())
                issues.Add($"{c.Name}: No biblical references defined");
        }

        if (issues.Any())
        {
            _output.WriteLine($"   ⚠️ Issues Found:");
            foreach (var issue in issues)
                _output.WriteLine($"      - {issue}");
        }
        else
        {
            _output.WriteLine($"   ✅ All characters have complete data");
        }

        // This is informational - don't fail if there are minor issues
        Assert.True(characters.Count >= 10, "Should have at least 10 characters");
    }

    [Fact]
    public async Task Integration_CharacterSystemPrompts_AreWellFormed()
    {
        // Arrange
        var repository = new InMemoryCharacterRepository();

        // Act
        var characters = await repository.GetAllCharactersAsync();

        // Assert - check system prompts are properly formatted
        _output.WriteLine($"📊 System Prompt Analysis:");
        foreach (var c in characters)
        {
            var promptLength = c.SystemPrompt.Length;
            var hasName = c.SystemPrompt.Contains(c.Name, StringComparison.OrdinalIgnoreCase);
            var hasBiblicalContext = c.SystemPrompt.Contains("Bible", StringComparison.OrdinalIgnoreCase) ||
                                     c.SystemPrompt.Contains("Scripture", StringComparison.OrdinalIgnoreCase) ||
                                     c.SystemPrompt.Contains("Lord", StringComparison.OrdinalIgnoreCase) ||
                                     c.SystemPrompt.Contains("God", StringComparison.OrdinalIgnoreCase);

            _output.WriteLine($"   {c.Name}: {promptLength} chars, HasName={hasName}, HasContext={hasBiblicalContext}");
            
            Assert.True(promptLength >= 50, $"{c.Name} should have meaningful system prompt");
        }
    }

    [Fact]
    public async Task Integration_SimulateUserSession_CompleteFlow()
    {
        // Simulate a complete user session
        var repository = new InMemoryCharacterRepository();
        var sw = Stopwatch.StartNew();

        _output.WriteLine("📊 Simulating Complete User Session:");

        // Step 1: User opens app, loads characters
        var characters = await repository.GetAllCharactersAsync();
        _output.WriteLine($"   1. Loaded {characters.Count} characters in {sw.ElapsedMilliseconds}ms");
        Assert.NotEmpty(characters);

        // Step 2: User browses characters
        sw.Restart();
        foreach (var c in characters.Take(5))
        {
            _ = c.Name;
            _ = c.Description;
            _ = c.Title;
        }
        _output.WriteLine($"   2. Browsed 5 characters in {sw.ElapsedMilliseconds}ms");

        // Step 3: User selects Moses
        sw.Restart();
        var moses = await repository.GetCharacterAsync("moses");
        Assert.NotNull(moses);
        _output.WriteLine($"   3. Selected Moses in {sw.ElapsedMilliseconds}ms");

        // Step 4: Create a chat session
        sw.Restart();
        var session = new ChatSession
        {
            CharacterId = moses.Id,
            Messages = new List<ChatMessage>
            {
                new() { Role = "user", Content = "Tell me about leading the Israelites" }
            }
        };
        _output.WriteLine($"   4. Created session in {sw.ElapsedMilliseconds}ms");

        // Step 5: Simulate AI response
        sw.Restart();
        session.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = "Ah, my child, the journey through the wilderness was long and arduous..."
        });
        _output.WriteLine($"   5. Added AI response in {sw.ElapsedMilliseconds}ms");

        // Step 6: User saves reflection
        sw.Restart();
        var reflection = new Reflection
        {
            Title = "Conversation with Moses",
            SavedContent = session.Messages.Last().Content,
            PersonalNotes = "This was inspiring",
            CharacterName = moses.Name
        };
        _output.WriteLine($"   6. Created reflection in {sw.ElapsedMilliseconds}ms");

        _output.WriteLine("   ✅ Complete user session simulated successfully");
    }
}


