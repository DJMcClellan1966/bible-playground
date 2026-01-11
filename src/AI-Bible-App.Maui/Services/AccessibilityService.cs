using Microsoft.Maui.Accessibility;
using System.Globalization;

namespace AI_Bible_App.Maui.Services;

/// <summary>
/// Service for managing accessibility features including
/// high-contrast modes, screen reader announcements, and localization
/// </summary>
public interface IAccessibilityService
{
    bool IsHighContrastEnabled { get; }
    bool IsScreenReaderActive { get; }
    string CurrentLanguage { get; }
    
    void SetHighContrastMode(bool enabled);
    void AnnounceForAccessibility(string message);
    void SetLanguage(string languageCode);
    string GetLocalizedString(string key);
    Task<IEnumerable<string>> GetAvailableLanguagesAsync();
}

public class AccessibilityService : IAccessibilityService
{
    private const string HighContrastKey = "HighContrastEnabled";
    private const string LanguageKey = "AppLanguage";
    
    private bool _isHighContrastEnabled;
    private string _currentLanguage = "en";
    
    // Localized strings dictionary
    private static readonly Dictionary<string, Dictionary<string, string>> LocalizedStrings = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            // General
            { "app_title", "AI Bible App" },
            { "loading", "Loading..." },
            { "error", "Error" },
            { "cancel", "Cancel" },
            { "save", "Save" },
            { "delete", "Delete" },
            { "share", "Share" },
            { "close", "Close" },
            
            // Navigation
            { "nav_home", "Home" },
            { "nav_chat", "Chat" },
            { "nav_bible", "Bible" },
            { "nav_prayer", "Prayer" },
            { "nav_settings", "Settings" },
            { "nav_history", "History" },
            
            // Characters
            { "select_character", "Select a Character" },
            { "swipe_hint", "Swipe to explore • Tap to chat" },
            { "tap_to_chat", "Tap to start conversation" },
            
            // Chat
            { "type_message", "Type a message..." },
            { "send", "Send" },
            { "listening", "Listening..." },
            { "speaking", "Speaking..." },
            { "is_typing", "{0} is typing..." },
            
            // Bible Reader
            { "bible_reader", "Bible Reader" },
            { "search_verses", "Search verses..." },
            { "select_book", "Select Book" },
            { "select_chapter", "Select Chapter" },
            { "previous", "Previous" },
            { "next", "Next" },
            { "bookmark", "Bookmark" },
            { "copy", "Copy" },
            { "read_aloud", "Read Aloud" },
            
            // Prayer
            { "generate_prayer", "Generate Prayer" },
            { "prayer_topic", "Prayer Topic" },
            { "prayer_style", "Prayer Style" },
            { "prayer_mood", "Mood" },
            
            // History
            { "your_journey", "Your Journey" },
            { "conversations", "Conversations" },
            { "prayers", "Prayers" },
            { "reflections", "Reflections" },
            { "days_active", "Days Active" },
            { "export_pdf", "Export PDF" },
            { "export_csv", "Export CSV" },
            
            // Accessibility
            { "high_contrast", "High Contrast Mode" },
            { "font_size", "Font Size" },
            { "screen_reader", "Screen Reader Support" },
            { "voice_control", "Voice Control" }
        },
        
        ["es"] = new Dictionary<string, string>
        {
            // General
            { "app_title", "Biblia IA" },
            { "loading", "Cargando..." },
            { "error", "Error" },
            { "cancel", "Cancelar" },
            { "save", "Guardar" },
            { "delete", "Eliminar" },
            { "share", "Compartir" },
            { "close", "Cerrar" },
            
            // Navigation
            { "nav_home", "Inicio" },
            { "nav_chat", "Chat" },
            { "nav_bible", "Biblia" },
            { "nav_prayer", "Oración" },
            { "nav_settings", "Configuración" },
            { "nav_history", "Historial" },
            
            // Characters
            { "select_character", "Selecciona un Personaje" },
            { "swipe_hint", "Desliza para explorar • Toca para chatear" },
            { "tap_to_chat", "Toca para iniciar conversación" },
            
            // Chat
            { "type_message", "Escribe un mensaje..." },
            { "send", "Enviar" },
            { "listening", "Escuchando..." },
            { "speaking", "Hablando..." },
            { "is_typing", "{0} está escribiendo..." },
            
            // Bible Reader
            { "bible_reader", "Lector Bíblico" },
            { "search_verses", "Buscar versículos..." },
            { "select_book", "Seleccionar Libro" },
            { "select_chapter", "Seleccionar Capítulo" },
            { "previous", "Anterior" },
            { "next", "Siguiente" },
            { "bookmark", "Marcador" },
            { "copy", "Copiar" },
            { "read_aloud", "Leer en Voz Alta" },
            
            // Prayer
            { "generate_prayer", "Generar Oración" },
            { "prayer_topic", "Tema de Oración" },
            { "prayer_style", "Estilo de Oración" },
            { "prayer_mood", "Estado de Ánimo" },
            
            // History
            { "your_journey", "Tu Camino" },
            { "conversations", "Conversaciones" },
            { "prayers", "Oraciones" },
            { "reflections", "Reflexiones" },
            { "days_active", "Días Activos" },
            { "export_pdf", "Exportar PDF" },
            { "export_csv", "Exportar CSV" },
            
            // Accessibility
            { "high_contrast", "Modo Alto Contraste" },
            { "font_size", "Tamaño de Letra" },
            { "screen_reader", "Soporte de Lector de Pantalla" },
            { "voice_control", "Control por Voz" }
        },
        
        ["he"] = new Dictionary<string, string>
        {
            // General (Hebrew - Right-to-Left)
            { "app_title", "אפליקציית תנ\"ך בינה מלאכותית" },
            { "loading", "טוען..." },
            { "error", "שגיאה" },
            { "cancel", "ביטול" },
            { "save", "שמור" },
            { "delete", "מחק" },
            { "share", "שתף" },
            { "close", "סגור" },
            
            // Navigation
            { "nav_home", "בית" },
            { "nav_chat", "צ'אט" },
            { "nav_bible", "תנ\"ך" },
            { "nav_prayer", "תפילה" },
            { "nav_settings", "הגדרות" },
            { "nav_history", "היסטוריה" },
            
            // Characters
            { "select_character", "בחר דמות" },
            { "swipe_hint", "החלק לחקור • לחץ לשוחח" },
            { "tap_to_chat", "לחץ להתחיל שיחה" },
            
            // Chat
            { "type_message", "הקלד הודעה..." },
            { "send", "שלח" },
            { "listening", "מאזין..." },
            { "speaking", "מדבר..." },
            { "is_typing", "{0} מקליד..." },
            
            // Bible Reader
            { "bible_reader", "קורא תנ\"ך" },
            { "search_verses", "חפש פסוקים..." },
            { "select_book", "בחר ספר" },
            { "select_chapter", "בחר פרק" },
            { "previous", "הקודם" },
            { "next", "הבא" },
            { "bookmark", "סימניה" },
            { "copy", "העתק" },
            { "read_aloud", "קרא בקול" },
            
            // Prayer
            { "generate_prayer", "צור תפילה" },
            { "prayer_topic", "נושא תפילה" },
            { "prayer_style", "סגנון תפילה" },
            { "prayer_mood", "מצב רוח" },
            
            // History
            { "your_journey", "המסע שלך" },
            { "conversations", "שיחות" },
            { "prayers", "תפילות" },
            { "reflections", "הרהורים" },
            { "days_active", "ימים פעילים" },
            { "export_pdf", "ייצא PDF" },
            { "export_csv", "ייצא CSV" },
            
            // Accessibility
            { "high_contrast", "מצב ניגודיות גבוהה" },
            { "font_size", "גודל גופן" },
            { "screen_reader", "תמיכה בקורא מסך" },
            { "voice_control", "שליטה קולית" }
        }
    };
    
    public bool IsHighContrastEnabled => _isHighContrastEnabled;
    
    public bool IsScreenReaderActive
    {
        get
        {
            try
            {
                // Check if screen reader is active (platform-specific)
                #if WINDOWS
                return Microsoft.UI.Xaml.Automation.Peers.AutomationPeer.ListenerExists(
                    Microsoft.UI.Xaml.Automation.Peers.AutomationEvents.LiveRegionChanged);
                #else
                return false;
                #endif
            }
            catch
            {
                return false;
            }
        }
    }
    
    public string CurrentLanguage => _currentLanguage;
    
    public AccessibilityService()
    {
        // Load saved preferences
        _isHighContrastEnabled = Preferences.Default.Get(HighContrastKey, false);
        _currentLanguage = Preferences.Default.Get(LanguageKey, "en");
    }
    
    public void SetHighContrastMode(bool enabled)
    {
        _isHighContrastEnabled = enabled;
        Preferences.Default.Set(HighContrastKey, enabled);
        
        // Update app resources for high contrast
        if (Application.Current?.Resources != null)
        {
            if (enabled)
            {
                ApplyHighContrastTheme();
            }
            else
            {
                ResetToDefaultTheme();
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[Accessibility] High contrast mode: {enabled}");
    }
    
    private void ApplyHighContrastTheme()
    {
        var resources = Application.Current?.Resources;
        if (resources == null) return;
        
        // Apply high contrast colors based on current theme
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        
        if (isDark)
        {
            resources["Primary"] = Color.FromArgb("#FFFF00"); // Yellow on black
            resources["Secondary"] = Color.FromArgb("#00FFFF"); // Cyan
            resources["TextColor"] = Color.FromArgb("#FFFFFF");
            resources["PageBackgroundColor"] = Color.FromArgb("#000000");
            resources["CardBackground"] = Color.FromArgb("#1A1A1A");
        }
        else
        {
            resources["Primary"] = Color.FromArgb("#0000FF"); // Blue on white
            resources["Secondary"] = Color.FromArgb("#008000"); // Green
            resources["TextColor"] = Color.FromArgb("#000000");
            resources["PageBackgroundColor"] = Color.FromArgb("#FFFFFF");
            resources["CardBackground"] = Color.FromArgb("#FFFFFF");
        }
    }
    
    private void ResetToDefaultTheme()
    {
        var resources = Application.Current?.Resources;
        if (resources == null) return;
        
        // Reset to default theme colors
        resources["Primary"] = Color.FromArgb("#8B4513"); // Saddle Brown
        resources["Secondary"] = Color.FromArgb("#6B5B95"); // Purple
        // Other colors will use defaults from resource dictionary
    }
    
    public void AnnounceForAccessibility(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        
        try
        {
            // Use MAUI's SemanticScreenReader
            SemanticScreenReader.Announce(message);
            System.Diagnostics.Debug.WriteLine($"[Accessibility] Announced: {message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Accessibility] Announce error: {ex.Message}");
        }
    }
    
    public void SetLanguage(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode)) return;
        
        var normalizedCode = languageCode.ToLowerInvariant();
        if (!LocalizedStrings.ContainsKey(normalizedCode))
        {
            normalizedCode = "en"; // Fallback to English
        }
        
        _currentLanguage = normalizedCode;
        Preferences.Default.Set(LanguageKey, normalizedCode);
        
        // Update culture for formatting
        try
        {
            var culture = new CultureInfo(normalizedCode);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch
        {
            // Use default culture if specified one is invalid
        }
        
        System.Diagnostics.Debug.WriteLine($"[Accessibility] Language set to: {normalizedCode}");
    }
    
    public string GetLocalizedString(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        
        // Try current language first
        if (LocalizedStrings.TryGetValue(_currentLanguage, out var langStrings))
        {
            if (langStrings.TryGetValue(key, out var localizedValue))
            {
                return localizedValue;
            }
        }
        
        // Fallback to English
        if (LocalizedStrings.TryGetValue("en", out var enStrings))
        {
            if (enStrings.TryGetValue(key, out var enValue))
            {
                return enValue;
            }
        }
        
        // Return the key itself as last resort
        return key;
    }
    
    public Task<IEnumerable<string>> GetAvailableLanguagesAsync()
    {
        var languages = new List<string>
        {
            "en", // English
            "es", // Spanish
            "he"  // Hebrew
        };
        
        return Task.FromResult<IEnumerable<string>>(languages);
    }
}

/// <summary>
/// Helper class for adding accessibility properties to views
/// </summary>
public static class AccessibilityHelper
{
    /// <summary>
    /// Sets comprehensive accessibility properties on a view
    /// </summary>
    public static void SetAccessibleView(VisualElement view, string name, string? hint = null, bool isHeader = false)
    {
        if (view == null) return;
        
        AutomationProperties.SetIsInAccessibleTree(view, true);
        AutomationProperties.SetName(view, name);
        
        if (!string.IsNullOrEmpty(hint))
        {
            AutomationProperties.SetHelpText(view, hint);
        }
        
        // For labels that act as headers
        if (isHeader && view is Label)
        {
            SemanticProperties.SetHeadingLevel(view, SemanticHeadingLevel.Level1);
        }
    }
    
    /// <summary>
    /// Creates an accessible description combining multiple elements
    /// </summary>
    public static string CreateAccessibleDescription(params string?[] parts)
    {
        var validParts = parts.Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(". ", validParts);
    }
    
    /// <summary>
    /// Gets the flow direction for the current language
    /// </summary>
    public static FlowDirection GetFlowDirection(string languageCode)
    {
        // Hebrew is right-to-left
        return languageCode.ToLowerInvariant() == "he" 
            ? FlowDirection.RightToLeft 
            : FlowDirection.LeftToRight;
    }
}
