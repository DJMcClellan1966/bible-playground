using System.Windows.Input;
using AI_Bible_App.Core.Interfaces;

namespace AI_Bible_App.Maui.Services;

/// <summary>
/// Service for handling keyboard shortcuts across the application
/// </summary>
public interface IKeyboardShortcutService
{
    void RegisterShortcut(string key, string modifiers, ICommand command);
    void RegisterShortcut(string key, string modifiers, Func<Task> action);
    void UnregisterShortcut(string key, string modifiers);
    bool HandleKeyPress(string key, bool ctrlPressed, bool shiftPressed, bool altPressed);
    void RegisterDefaultShortcuts();
    Dictionary<string, string> GetRegisteredShortcuts();
}

public class KeyboardShortcutService : IKeyboardShortcutService
{
    private readonly Dictionary<string, ICommand> _shortcuts = new();
    private readonly Dictionary<string, string> _shortcutDescriptions = new();
    private readonly INavigationService _navigationService;

    public KeyboardShortcutService(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void RegisterShortcut(string key, string modifiers, ICommand command)
    {
        var shortcutKey = BuildShortcutKey(key, modifiers);
        _shortcuts[shortcutKey] = command;
    }

    public void RegisterShortcut(string key, string modifiers, Func<Task> action)
    {
        var command = new AsyncCommand(action);
        RegisterShortcut(key, modifiers, command);
    }

    public void UnregisterShortcut(string key, string modifiers)
    {
        var shortcutKey = BuildShortcutKey(key, modifiers);
        _shortcuts.Remove(shortcutKey);
        _shortcutDescriptions.Remove(shortcutKey);
    }

    public bool HandleKeyPress(string key, bool ctrlPressed, bool shiftPressed, bool altPressed)
    {
        var modifiers = new List<string>();
        if (ctrlPressed) modifiers.Add("ctrl");
        if (shiftPressed) modifiers.Add("shift");
        if (altPressed) modifiers.Add("alt");

        var modifierString = modifiers.Count > 0 ? string.Join("+", modifiers) : "";
        var shortcutKey = string.IsNullOrEmpty(modifierString) 
            ? key.ToLowerInvariant() 
            : $"{modifierString}+{key}".ToLowerInvariant();

        System.Diagnostics.Debug.WriteLine($"[Keyboard] Key press: {shortcutKey}");

        if (_shortcuts.TryGetValue(shortcutKey, out var command))
        {
            if (command.CanExecute(null))
            {
                System.Diagnostics.Debug.WriteLine($"[Keyboard] Executing shortcut: {shortcutKey}");
                command.Execute(null);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Register default navigation shortcuts
    /// </summary>
    public void RegisterDefaultShortcuts()
    {
        // Navigation shortcuts
        RegisterNavigationShortcut("N", "ctrl", "characters", "New Chat (Character Selection)");
        RegisterNavigationShortcut("H", "ctrl", "chathistory", "Chat History");
        RegisterNavigationShortcut("P", "ctrl", "prayer", "Prayer Journal");
        RegisterNavigationShortcut("R", "ctrl", "reflection", "Reflections");
        RegisterNavigationShortcut("Comma", "ctrl", "settings", "Settings"); // Ctrl+,
        
        // Multi-character features
        RegisterNavigationShortcut("W", "ctrl", "wisdomcouncil", "Wisdom Council");
        RegisterNavigationShortcut("T", "ctrl", "roundtable", "Roundtable Discussion");
        RegisterNavigationShortcut("Y", "ctrl", "prayerchain", "Prayer Chain");
        
        // System
        RegisterNavigationShortcut("D", "ctrl+shift", "diagnostics", "System Diagnostics");
        
        System.Diagnostics.Debug.WriteLine($"[Keyboard] Registered {_shortcuts.Count} default shortcuts");
    }

    private void RegisterNavigationShortcut(string key, string modifiers, string route, string description)
    {
        var shortcutKey = BuildShortcutKey(key, modifiers);
        _shortcutDescriptions[shortcutKey] = description;
        
        RegisterShortcut(key, modifiers, async () =>
        {
            try
            {
                await _navigationService.NavigateToAsync($"//{route}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Keyboard] Navigation error: {ex.Message}");
            }
        });
    }

    private static string BuildShortcutKey(string key, string modifiers)
    {
        if (string.IsNullOrEmpty(modifiers))
            return key.ToLowerInvariant();
        return $"{modifiers.ToLowerInvariant()}+{key.ToLowerInvariant()}";
    }

    public Dictionary<string, string> GetRegisteredShortcuts()
    {
        return new Dictionary<string, string>(_shortcutDescriptions);
    }

    /// <summary>
    /// Simple async command wrapper
    /// </summary>
    private class AsyncCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private bool _isExecuting;

        public AsyncCommand(Func<Task> execute)
        {
            _execute = execute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_isExecuting;

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;
            
            _isExecuting = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            
            try
            {
                await _execute();
            }
            finally
            {
                _isExecuting = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
