using AI_Bible_App.Core.Models;
using AI_Bible_App.Maui.ViewModels;
using Microsoft.Maui.Accessibility;

namespace AI_Bible_App.Maui.Views;

public partial class ChatPage : ContentPage, IQueryAttributable
{
    private readonly ChatViewModel _viewModel;

    public ChatPage(ChatViewModel viewModel)
    {
        System.Diagnostics.Debug.WriteLine("[DEBUG] ChatPage constructor START");
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        
        // Subscribe to scroll requests
        _viewModel.ScrollToBottomRequested += OnScrollToBottomRequested;
        
        System.Diagnostics.Debug.WriteLine("[DEBUG] ChatPage constructor END");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Set focus to message input for accessibility
        await Task.Delay(300);
        MessageEntry.Focus();
        
        // Announce page navigation to screen readers
        SemanticScreenReader.Announce($"Chat with {_viewModel.Character?.Name ?? "character"}. Type your message below.");
    }

    private async void OnScrollToBottomRequested(object? sender, EventArgs e)
    {
        // Delay slightly to ensure UI has updated
        await Task.Delay(50);
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await ChatScrollView.ScrollToAsync(0, ChatScrollView.ContentSize.Height, true);
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ScrollToBottomRequested -= OnScrollToBottomRequested;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        System.Diagnostics.Debug.WriteLine("[DEBUG] ApplyQueryAttributes called");
        try
        {
            if (query.ContainsKey("character") && query["character"] is BiblicalCharacter character)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Initializing with character: {character.Name}");
                
                // Check if resuming an existing session
                ChatSession? existingSession = null;
                if (query.ContainsKey("session") && query["session"] is ChatSession session)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Resuming session: {session.Id}");
                    existingSession = session;
                }
                
                // Check if starting a new chat (ignore existing session)
                bool startNewChat = query.ContainsKey("newChat") && query["newChat"] is true;
                if (startNewChat)
                {
                    System.Diagnostics.Debug.WriteLine("[DEBUG] Starting new chat (ignoring existing session)");
                    existingSession = null;
                }
                
                await _viewModel.InitializeAsync(character, existingSession, startNewChat);
                
                // Scroll to bottom after loading messages
                OnScrollToBottomRequested(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] ApplyQueryAttributes failed: {ex}");
        }
    }
}
