using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

#pragma warning disable MVVMTK0045 // AOT compatibility warning for WinRT scenarios

namespace AI_Bible_App.Maui.ViewModels;

public partial class ChatHistoryViewModel : BaseViewModel
{
    private readonly IChatRepository _chatRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly IDialogService _dialogService;
    private readonly IUserService _userService;
    private readonly IUsageMetricsService? _usageMetrics;

    [ObservableProperty]
    private ObservableCollection<ChatHistoryItem> chatSessions = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private ChatHistoryItem? selectedSession;

    [ObservableProperty]
    private string searchText = string.Empty;

    private ObservableCollection<ChatHistoryItem> _allSessions = new();

    public ChatHistoryViewModel(
        IChatRepository chatRepository,
        ICharacterRepository characterRepository,
        IDialogService dialogService,
        IUserService userService,
        IUsageMetricsService? usageMetrics = null)
    {
        _chatRepository = chatRepository;
        _characterRepository = characterRepository;
        _dialogService = dialogService;
        _userService = userService;
        _usageMetrics = usageMetrics;
        Title = "Chat History";
    }

    partial void OnSelectedSessionChanged(ChatHistoryItem? value)
    {
        if (value != null)
        {
            // Fire and forget - navigate then clear selection
            _ = HandleSessionSelectedAsync(value);
        }
    }

    private async Task HandleSessionSelectedAsync(ChatHistoryItem item)
    {
        await ResumeChat(item);
        SelectedSession = null; // Clear selection for next time
    }

    public async Task InitializeAsync()
    {
        _usageMetrics?.TrackFeatureUsed("ChatHistory");
        await LoadSessionsAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterSessions();
    }

    private void FilterSessions()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ChatSessions = new ObservableCollection<ChatHistoryItem>(_allSessions);
        }
        else
        {
            var searchLower = SearchText.ToLowerInvariant();
            var filtered = _allSessions.Where(item =>
                item.CharacterName.ToLowerInvariant().Contains(searchLower) ||
                item.LastMessage.ToLowerInvariant().Contains(searchLower) ||
                item.Session.Messages.Any(m => m.Content.ToLowerInvariant().Contains(searchLower))
            ).ToList();
            
            ChatSessions = new ObservableCollection<ChatHistoryItem>(filtered);
        }
        IsEmpty = ChatSessions.Count == 0;
    }

    [RelayCommand]
    private async Task LoadSessionsAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            var sessions = await _chatRepository.GetAllSessionsAsync();
            var characters = await _characterRepository.GetAllCharactersAsync();

            var historyItems = new List<ChatHistoryItem>();

            foreach (var session in sessions.OrderByDescending(s => s.StartedAt))
            {
                var character = characters.FirstOrDefault(c => c.Id == session.CharacterId);
                var lastMessage = session.Messages.LastOrDefault(m => m.Role == "assistant");
                
                historyItems.Add(new ChatHistoryItem
                {
                    Session = session,
                    CharacterName = character?.Name ?? "Unknown",
                    CharacterTitle = character?.Title ?? "",
                    CharacterIconFileName = character?.IconFileName ?? "default_avatar.png",
                    LastMessage = lastMessage?.Content?.Length > 100 
                        ? lastMessage.Content.Substring(0, 100) + "..." 
                        : lastMessage?.Content ?? "No messages",
                    MessageCount = session.Messages.Count,
                    StartedAt = session.StartedAt
                });
            }

            ChatSessions = new ObservableCollection<ChatHistoryItem>(historyItems);
            _allSessions = new ObservableCollection<ChatHistoryItem>(historyItems);
            IsEmpty = ChatSessions.Count == 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load chat sessions: {ex}");
            await _dialogService.ShowAlertAsync("Error", $"Failed to load chat history: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ResumeChat(ChatHistoryItem item)
    {
        if (item?.Session == null) return;

        var characters = await _characterRepository.GetAllCharactersAsync();
        var character = characters.FirstOrDefault(c => c.Id == item.Session.CharacterId);

        if (character == null)
        {
            await _dialogService.ShowAlertAsync("Error", "Character not found");
            return;
        }

        var navigationParams = new Dictionary<string, object>
        {
            { "character", character },
            { "session", item.Session }
        };

        await Shell.Current.GoToAsync("chat", navigationParams);
    }

    [RelayCommand]
    private async Task DeleteSession(ChatHistoryItem item)
    {
        if (item?.Session == null) return;

        bool confirm = await _dialogService.ShowConfirmAsync(
            "Delete Chat",
            $"Delete conversation with {item.CharacterName}?",
            "Delete",
            "Cancel");

        if (confirm)
        {
            try
            {
                await _chatRepository.DeleteSessionAsync(item.Session.Id);
                ChatSessions.Remove(item);
                IsEmpty = ChatSessions.Count == 0;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Failed to delete: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ExportSession(ChatHistoryItem item)
    {
        if (item?.Session == null) return;

        try
        {
            // Build the text content
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  CONVERSATION WITH {item.CharacterName.ToUpper()}");
            sb.AppendLine($"  {item.CharacterTitle}");
            sb.AppendLine($"═══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Started: {item.StartedAt:MMMM d, yyyy 'at' h:mm tt}");
            sb.AppendLine($"Messages: {item.MessageCount}");
            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine();

            foreach (var message in item.Session.Messages)
            {
                var speaker = message.Role == "user" ? "You" : item.CharacterName;
                var timestamp = message.Timestamp.ToLocalTime().ToString("h:mm tt");
                
                sb.AppendLine($"[{timestamp}] {speaker}:");
                sb.AppendLine(message.Content);
                sb.AppendLine();
            }

            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine($"Exported from Voices of Scripture on {DateTime.Now:MMMM d, yyyy}");

            var content = sb.ToString();
            var fileName = $"Chat_{item.CharacterName}_{item.StartedAt:yyyyMMdd_HHmmss}.txt";

            // Use Share API to let user save/share the file
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, content);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Export conversation with {item.CharacterName}",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Export failed: {ex}");
            if (Shell.Current?.CurrentPage != null)
            {
                await Shell.Current.CurrentPage.DisplayAlert("Error", $"Failed to export: {ex.Message}", "OK");
            }
        }
    }

    [RelayCommand]
    private async Task ShareSession(ChatHistoryItem item)
    {
        if (item?.Session == null) return;

        try
        {
            var allUsers = await _userService.GetAllUsersAsync();
            var currentUserId = _userService.CurrentUser?.Id;
            
            // Filter out current user
            var otherUsers = allUsers.Where(u => u.Id != currentUserId).ToList();
            
            if (otherUsers.Count == 0)
            {
                await _dialogService.ShowAlertAsync("No Other Users", "Create additional user profiles to share chats with them.");
                return;
            }

            // Build options list
            var options = new List<string> { "📢 Share with Everyone" };
            options.AddRange(otherUsers.Select(u => $"{u.AvatarEmoji ?? "👤"} {u.Name}"));
            options.Add("🚫 Stop Sharing");
            
            var result = await _dialogService.ShowActionSheetAsync(
                $"Share chat with {item.CharacterName}",
                "Cancel",
                null,
                options.ToArray());

            if (result == null || result == "Cancel") return;

            if (result == "📢 Share with Everyone")
            {
                item.Session.IsSharedWithAll = true;
                item.Session.SharedWithUserIds.Clear();
                await _chatRepository.SaveSessionAsync(item.Session);
                await _dialogService.ShowAlertAsync("Shared", "This chat is now visible to all users on this device.");
            }
            else if (result == "🚫 Stop Sharing")
            {
                item.Session.IsSharedWithAll = false;
                item.Session.SharedWithUserIds.Clear();
                await _chatRepository.SaveSessionAsync(item.Session);
                await _dialogService.ShowAlertAsync("Private", "This chat is now private to you.");
            }
            else
            {
                // Find selected user
                var selectedUser = otherUsers.FirstOrDefault(u => result.Contains(u.Name));
                if (selectedUser != null)
                {
                    item.Session.IsSharedWithAll = false;
                    if (!item.Session.SharedWithUserIds.Contains(selectedUser.Id))
                    {
                        item.Session.SharedWithUserIds.Add(selectedUser.Id);
                    }
                    await _chatRepository.SaveSessionAsync(item.Session);
                    await _dialogService.ShowAlertAsync("Shared", $"This chat is now shared with {selectedUser.Name}.");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Share failed: {ex}");
            await _dialogService.ShowAlertAsync("Error", $"Failed to share: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task GoToCharacters()
    {
        await Shell.Current.GoToAsync("//characters");
    }
}

/// <summary>
/// Display model for chat history items
/// </summary>
public class ChatHistoryItem
{
    public ChatSession Session { get; set; } = new();
    public string CharacterName { get; set; } = string.Empty;
    public string CharacterTitle { get; set; } = string.Empty;
    public string CharacterIconFileName { get; set; } = "default_avatar.png";
    public string LastMessage { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public DateTime StartedAt { get; set; }
}
