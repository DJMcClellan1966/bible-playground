using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text;

namespace AI_Bible_App.Maui.ViewModels;

// Summary models for display
public class ChatSessionSummary
{
    public string Id { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string CharacterInitial => CharacterName.Length > 0 ? CharacterName[0].ToString() : "?";
    public string LastMessagePreview { get; set; } = string.Empty;
    public DateTime LastActivity { get; set; }
    public int MessageCount { get; set; }
    public string FormattedDate => LastActivity.ToString("MMM d, yyyy");
}

public class PrayerSummary
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string FormattedDate => CreatedAt.ToString("MMM d, yyyy");
}

public class ReflectionSummary
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ScriptureReference { get; set; }
    public bool HasScriptureReference => !string.IsNullOrEmpty(ScriptureReference);
    public DateTime CreatedAt { get; set; }
    public string FormattedDate => CreatedAt.ToString("MMM d, yyyy");
}

public partial class HistoryDashboardViewModel : BaseViewModel
{
    private readonly IChatRepository _chatRepository;
    private readonly IPrayerRepository _prayerRepository;
    private readonly IReflectionRepository _reflectionRepository;
    private readonly IDialogService _dialogService;
    private readonly ICharacterRepository _characterRepository;
    private readonly IUsageMetricsService? _usageMetrics;
    
    [ObservableProperty]
    private ObservableCollection<ChatSessionSummary> chatSessions = new();
    
    [ObservableProperty]
    private ObservableCollection<PrayerSummary> prayers = new();
    
    [ObservableProperty]
    private ObservableCollection<ReflectionSummary> reflections = new();
    
    [ObservableProperty]
    private int totalConversations;
    
    [ObservableProperty]
    private int totalPrayers;
    
    [ObservableProperty]
    private int totalReflections;
    
    [ObservableProperty]
    private int daysActive;
    
    public HistoryDashboardViewModel(
        IChatRepository chatRepository,
        IPrayerRepository prayerRepository,
        IReflectionRepository reflectionRepository,
        IDialogService dialogService,
        ICharacterRepository characterRepository,
        IUsageMetricsService? usageMetrics = null)
    {
        _chatRepository = chatRepository;
        _prayerRepository = prayerRepository;
        _reflectionRepository = reflectionRepository;
        _dialogService = dialogService;
        _characterRepository = characterRepository;
        _usageMetrics = usageMetrics;
        Title = "Your Journey";
    }
    
    public async Task InitializeAsync()
    {
        if (IsBusy) return;
        
        try
        {
            IsBusy = true;
            _usageMetrics?.TrackFeatureUsed("HistoryDashboard");
            
            await LoadChatSessions();
            await LoadPrayers();
            await LoadReflections();
            CalculateStats();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryDashboard] Init error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private async Task LoadChatSessions()
    {
        try
        {
            ChatSessions.Clear();
            var sessions = await _chatRepository.GetAllSessionsAsync();
            var characters = await _characterRepository.GetAllCharactersAsync();
            
            foreach (var session in sessions.OrderByDescending(s => s.StartedAt))
            {
                var character = characters.FirstOrDefault(c => c.Id == session.CharacterId);
                var lastMessage = session.Messages.LastOrDefault();
                
                ChatSessions.Add(new ChatSessionSummary
                {
                    Id = session.Id,
                    CharacterId = session.CharacterId,
                    CharacterName = character?.Name ?? "Unknown",
                    LastMessagePreview = lastMessage?.Content?.Length > 100 
                        ? lastMessage.Content[..100] + "..." 
                        : lastMessage?.Content ?? "",
                    LastActivity = session.StartedAt,
                    MessageCount = session.Messages.Count
                });
            }
            
            TotalConversations = ChatSessions.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryDashboard] Load chats error: {ex.Message}");
        }
    }
    
    private async Task LoadPrayers()
    {
        try
        {
            Prayers.Clear();
            var prayerList = await _prayerRepository.GetAllPrayersAsync();
            
            foreach (var prayer in prayerList.OrderByDescending(p => p.CreatedAt))
            {
                Prayers.Add(new PrayerSummary
                {
                    Id = prayer.Id,
                    Title = prayer.Topic ?? "Prayer",
                    PreviewText = prayer.Content.Length > 150 ? prayer.Content[..150] + "..." : prayer.Content,
                    FullText = prayer.Content,
                    Style = "Traditional",
                    Mood = "Peaceful",
                    CreatedAt = prayer.CreatedAt
                });
            }
            
            TotalPrayers = Prayers.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryDashboard] Load prayers error: {ex.Message}");
        }
    }
    
    private async Task LoadReflections()
    {
        try
        {
            Reflections.Clear();
            var reflectionList = await _reflectionRepository.GetAllReflectionsAsync();
            
            foreach (var reflection in reflectionList.OrderByDescending(r => r.CreatedAt))
            {
                var content = reflection.SavedContent;
                Reflections.Add(new ReflectionSummary
                {
                    Id = reflection.Id,
                    Title = reflection.Title,
                    Content = content.Length > 150 ? content[..150] + "..." : content,
                    ScriptureReference = reflection.BibleReferences.FirstOrDefault() ?? string.Empty,
                    CreatedAt = reflection.CreatedAt
                });
            }
            
            TotalReflections = Reflections.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryDashboard] Load reflections error: {ex.Message}");
        }
    }
    
    private void CalculateStats()
    {
        try
        {
            var allDates = new List<DateTime>();
            
            allDates.AddRange(ChatSessions.Select(c => c.LastActivity.Date));
            allDates.AddRange(Prayers.Select(p => p.CreatedAt.Date));
            allDates.AddRange(Reflections.Select(r => r.CreatedAt.Date));
            
            DaysActive = allDates.Distinct().Count();
        }
        catch
        {
            DaysActive = 0;
        }
    }
    
    [RelayCommand]
    private async Task ViewChat(ChatSessionSummary session)
    {
        if (session == null) return;
        
        // Navigate to chat page with the session
        await Shell.Current.GoToAsync($"///Chat?sessionId={session.Id}&characterId={session.CharacterId}");
    }
    
    [RelayCommand]
    private async Task ViewPrayer(PrayerSummary prayer)
    {
        if (prayer == null) return;
        
        await _dialogService.ShowAlertAsync(
            prayer.Title,
            prayer.FullText);
    }
    
    [RelayCommand]
    private async Task ViewReflection(ReflectionSummary reflection)
    {
        if (reflection == null) return;
        
        var message = reflection.Content;
        if (!string.IsNullOrEmpty(reflection.ScriptureReference))
        {
            message += $"\n\n📖 {reflection.ScriptureReference}";
        }
        
        await _dialogService.ShowAlertAsync(
            reflection.Title,
            message);
    }
    
    [RelayCommand]
    private async Task DeleteChat(ChatSessionSummary session)
    {
        if (session == null) return;
        
        var confirm = await _dialogService.ShowConfirmAsync(
            "Delete Conversation",
            $"Are you sure you want to delete this conversation with {session.CharacterName}?");
        
        if (confirm)
        {
            await _chatRepository.DeleteSessionAsync(session.Id);
            ChatSessions.Remove(session);
            TotalConversations = ChatSessions.Count;
        }
    }
    
    [RelayCommand]
    private async Task DeletePrayer(PrayerSummary prayer)
    {
        if (prayer == null) return;
        
        var confirm = await _dialogService.ShowConfirmAsync(
            "Delete Prayer",
            "Are you sure you want to delete this prayer?");
        
        if (confirm)
        {
            await _prayerRepository.DeletePrayerAsync(prayer.Id);
            Prayers.Remove(prayer);
            TotalPrayers = Prayers.Count;
        }
    }
    
    [RelayCommand]
    private async Task DeleteReflection(ReflectionSummary reflection)
    {
        if (reflection == null) return;
        
        var confirm = await _dialogService.ShowConfirmAsync(
            "Delete Reflection",
            "Are you sure you want to delete this reflection?");
        
        if (confirm)
        {
            await _reflectionRepository.DeleteReflectionAsync(reflection.Id);
            Reflections.Remove(reflection);
            TotalReflections = Reflections.Count;
        }
    }
    
    [RelayCommand]
    private async Task ExportPdf()
    {
        try
        {
            IsBusy = true;
            
            // Build PDF content
            var content = new StringBuilder();
            content.AppendLine("═══════════════════════════════════════════════");
            content.AppendLine("        MY SPIRITUAL JOURNEY JOURNAL");
            content.AppendLine($"        Exported on {DateTime.Now:MMMM d, yyyy}");
            content.AppendLine("═══════════════════════════════════════════════");
            content.AppendLine();
            
            // Summary
            content.AppendLine("📊 JOURNEY SUMMARY");
            content.AppendLine($"   Conversations: {TotalConversations}");
            content.AppendLine($"   Prayers: {TotalPrayers}");
            content.AppendLine($"   Reflections: {TotalReflections}");
            content.AppendLine($"   Days Active: {DaysActive}");
            content.AppendLine();
            
            // Conversations
            if (ChatSessions.Any())
            {
                content.AppendLine("═══════════════════════════════════════════════");
                content.AppendLine("💬 CONVERSATIONS");
                content.AppendLine("───────────────────────────────────────────────");
                foreach (var chat in ChatSessions)
                {
                    content.AppendLine($"\n📅 {chat.FormattedDate} - {chat.CharacterName}");
                    content.AppendLine($"   Messages: {chat.MessageCount}");
                    content.AppendLine($"   Preview: {chat.LastMessagePreview}");
                }
                content.AppendLine();
            }
            
            // Prayers
            if (Prayers.Any())
            {
                content.AppendLine("═══════════════════════════════════════════════");
                content.AppendLine("🙏 PRAYERS");
                content.AppendLine("───────────────────────────────────────────────");
                foreach (var prayer in Prayers)
                {
                    content.AppendLine($"\n📅 {prayer.FormattedDate} - {prayer.Title}");
                    content.AppendLine($"   Style: {prayer.Style} | Mood: {prayer.Mood}");
                    content.AppendLine($"   {prayer.FullText}");
                }
                content.AppendLine();
            }
            
            // Reflections
            if (Reflections.Any())
            {
                content.AppendLine("═══════════════════════════════════════════════");
                content.AppendLine("📝 REFLECTIONS");
                content.AppendLine("───────────────────────────────────────────────");
                foreach (var reflection in Reflections)
                {
                    content.AppendLine($"\n📅 {reflection.FormattedDate} - {reflection.Title}");
                    if (reflection.HasScriptureReference)
                        content.AppendLine($"   📖 {reflection.ScriptureReference}");
                    content.AppendLine($"   {reflection.Content}");
                }
            }
            
            content.AppendLine();
            content.AppendLine("═══════════════════════════════════════════════");
            content.AppendLine("          Generated by AI Bible App");
            content.AppendLine("═══════════════════════════════════════════════");
            
            // Save as text file (PDF generation requires additional libraries)
            var fileName = $"spiritual_journey_{DateTime.Now:yyyy-MM-dd}.txt";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, content.ToString());
            
            // Share the file
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export Spiritual Journey",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryDashboard] Export PDF error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Export Error", "Failed to export your journal.");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private async Task ExportCsv()
    {
        try
        {
            IsBusy = true;
            
            // Build CSV content
            var content = new StringBuilder();
            
            // Header
            content.AppendLine("Type,Date,Title/Character,Content,Style,Scripture");
            
            // Conversations
            foreach (var chat in ChatSessions)
            {
                var sanitizedPreview = chat.LastMessagePreview.Replace("\"", "\"\"").Replace("\n", " ");
                content.AppendLine($"\"Conversation\",\"{chat.FormattedDate}\",\"{chat.CharacterName}\",\"{sanitizedPreview}\",\"\",\"\"");
            }
            
            // Prayers
            foreach (var prayer in Prayers)
            {
                var sanitizedText = prayer.FullText.Replace("\"", "\"\"").Replace("\n", " ");
                content.AppendLine($"\"Prayer\",\"{prayer.FormattedDate}\",\"{prayer.Title}\",\"{sanitizedText}\",\"{prayer.Style}\",\"\"");
            }
            
            // Reflections
            foreach (var reflection in Reflections)
            {
                var sanitizedContent = reflection.Content.Replace("\"", "\"\"").Replace("\n", " ");
                content.AppendLine($"\"Reflection\",\"{reflection.FormattedDate}\",\"{reflection.Title}\",\"{sanitizedContent}\",\"\",\"{reflection.ScriptureReference ?? ""}\"");
            }
            
            // Save CSV file
            var fileName = $"spiritual_journey_{DateTime.Now:yyyy-MM-dd}.csv";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, content.ToString());
            
            // Share the file
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export Spiritual Journey (CSV)",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryDashboard] Export CSV error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Export Error", "Failed to export your journal.");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private async Task ShowExportOptions()
    {
        var action = await _dialogService.ShowActionSheetAsync(
            "Export Options",
            "Cancel",
            null,
            "📄 Export All as Text/PDF",
            "📊 Export All as CSV",
            "💬 Export Conversations Only",
            "🙏 Export Prayers Only",
            "📝 Export Reflections Only");
        
        switch (action)
        {
            case "📄 Export All as Text/PDF":
                await ExportPdf();
                break;
            case "📊 Export All as CSV":
                await ExportCsv();
                break;
            // Add more export options as needed
        }
    }
    
    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.GoToAsync("..");
    }
}
