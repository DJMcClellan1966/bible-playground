using AI_Bible_App.Core.Models;
using AI_Bible_App.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

#pragma warning disable MVVMTK0045

namespace AI_Bible_App.Maui.ViewModels;

public partial class CustomCharacterViewModel : BaseViewModel
{
    private readonly ICustomCharacterRepository _repository;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<CustomCharacter> characters = new();

    [ObservableProperty]
    private CustomCharacter? selectedCharacter;

    [ObservableProperty]
    private bool isEditing;

    // Edit form fields
    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string editTitle = string.Empty;

    [ObservableProperty]
    private string editDescription = string.Empty;

    [ObservableProperty]
    private string editEra = string.Empty;

    [ObservableProperty]
    private string editBiblicalReferences = string.Empty;

    [ObservableProperty]
    private string editLifeExperiences = string.Empty;

    [ObservableProperty]
    private string editPersonalityTraits = string.Empty;

    [ObservableProperty]
    private string editKeyVirtues = string.Empty;

    [ObservableProperty]
    private string editKnownFor = string.Empty;

    [ObservableProperty]
    private string editCustomPrompt = string.Empty;

    [ObservableProperty]
    private bool useCustomPrompt;

    private string? _editingCharacterId;

    public CustomCharacterViewModel(ICustomCharacterRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        Title = "Custom Characters";
    }

    public async Task InitializeAsync()
    {
        await LoadCharactersAsync();
    }

    public async Task LoadCharactersAsync()
    {
        try
        {
            IsBusy = true;
            var chars = await _repository.GetAllAsync();
            Characters = new ObservableCollection<CustomCharacter>(chars.OrderBy(c => c.Name));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomChar] Load error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to load custom characters.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewCharacter()
    {
        _editingCharacterId = null;
        ClearForm();
        IsEditing = true;
    }

    [RelayCommand]
    private void EditCharacter(CustomCharacter? character)
    {
        if (character == null) return;

        _editingCharacterId = character.Id;
        EditName = character.Name;
        EditTitle = character.Title;
        EditDescription = character.Description;
        EditEra = character.Era;
        EditBiblicalReferences = string.Join("\n", character.BiblicalReferences);
        EditLifeExperiences = string.Join("\n", character.LifeExperiences);
        EditPersonalityTraits = string.Join(", ", character.PersonalityTraits);
        EditKeyVirtues = string.Join(", ", character.KeyVirtues);
        EditKnownFor = character.KnownFor;
        EditCustomPrompt = character.CustomSystemPrompt ?? string.Empty;
        UseCustomPrompt = !string.IsNullOrEmpty(character.CustomSystemPrompt);
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveCharacterAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            await _dialogService.ShowAlertAsync("Validation", "Name is required.", "OK");
            return;
        }

        try
        {
            var character = new CustomCharacter
            {
                Id = _editingCharacterId ?? Guid.NewGuid().ToString(),
                Name = EditName.Trim(),
                Title = EditTitle.Trim(),
                Description = EditDescription.Trim(),
                Era = EditEra.Trim(),
                BiblicalReferences = ParseLines(EditBiblicalReferences),
                LifeExperiences = ParseLines(EditLifeExperiences),
                PersonalityTraits = ParseCommaList(EditPersonalityTraits),
                KeyVirtues = ParseCommaList(EditKeyVirtues),
                KnownFor = EditKnownFor.Trim(),
                CustomSystemPrompt = UseCustomPrompt ? EditCustomPrompt.Trim() : null
            };

            await _repository.SaveAsync(character);
            await LoadCharactersAsync();
            
            ClearForm();
            IsEditing = false;
            
            await _dialogService.ShowAlertAsync("Saved", $"{character.Name} has been saved.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomChar] Save error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to save character.", "OK");
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ClearForm();
        IsEditing = false;
    }

    [RelayCommand]
    private async Task DeleteCharacterAsync(CustomCharacter? character)
    {
        if (character == null) return;

        var confirm = await _dialogService.ShowConfirmAsync(
            "Delete Character",
            $"Are you sure you want to delete {character.Name}?",
            "Delete", "Cancel");

        if (confirm)
        {
            try
            {
                await _repository.DeleteAsync(character.Id);
                await LoadCharactersAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CustomChar] Delete error: {ex.Message}");
                await _dialogService.ShowAlertAsync("Error", "Failed to delete character.", "OK");
            }
        }
    }

    [RelayCommand]
    private async Task ExportCharactersAsync()
    {
        try
        {
            var json = await _repository.ExportToJsonAsync();
            await Clipboard.SetTextAsync(json);
            await _dialogService.ShowAlertAsync("Exported", "Characters copied to clipboard as JSON.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomChar] Export error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to export characters.", "OK");
        }
    }

    [RelayCommand]
    private async Task ImportCharactersAsync()
    {
        try
        {
            var json = await Clipboard.GetTextAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                await _dialogService.ShowAlertAsync("Import", "Clipboard is empty. Copy JSON data first.", "OK");
                return;
            }

            await _repository.ImportFromJsonAsync(json);
            await LoadCharactersAsync();
            await _dialogService.ShowAlertAsync("Imported", "Characters imported successfully.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomChar] Import error: {ex.Message}");
            await _dialogService.ShowAlertAsync("Error", "Failed to import characters. Make sure the clipboard contains valid JSON.", "OK");
        }
    }

    private void ClearForm()
    {
        _editingCharacterId = null;
        EditName = string.Empty;
        EditTitle = string.Empty;
        EditDescription = string.Empty;
        EditEra = string.Empty;
        EditBiblicalReferences = string.Empty;
        EditLifeExperiences = string.Empty;
        EditPersonalityTraits = string.Empty;
        EditKeyVirtues = string.Empty;
        EditKnownFor = string.Empty;
        EditCustomPrompt = string.Empty;
        UseCustomPrompt = false;
    }

    private static List<string> ParseLines(string text)
    {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private static List<string> ParseCommaList(string text)
    {
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }
}
