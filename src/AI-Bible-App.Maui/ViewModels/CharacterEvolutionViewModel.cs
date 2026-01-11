using System.Collections.ObjectModel;
using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_Bible_App.Maui.ViewModels;

/// <summary>
/// ViewModel for viewing and managing character evolution/growth
/// Shows how characters have learned and grown from roundtable discussions
/// </summary>
public partial class CharacterEvolutionViewModel : BaseViewModel
{
    private readonly ICharacterRepository _characterRepository;
    private readonly ICrossCharacterLearningService _learningService;

    [ObservableProperty]
    private ObservableCollection<BiblicalCharacter> _characters = new();

    [ObservableProperty]
    private BiblicalCharacter? _selectedCharacter;

    [ObservableProperty]
    private CharacterEvolutionSummary? _evolutionSummary;

    [ObservableProperty]
    private bool _hasEvolution;

    [ObservableProperty]
    private string _evolutionDescription = string.Empty;

    public CharacterEvolutionViewModel(
        ICharacterRepository characterRepository,
        ICrossCharacterLearningService learningService)
    {
        _characterRepository = characterRepository;
        _learningService = learningService;
        
        Title = "Character Growth";
    }

    public async Task InitializeAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            var allCharacters = await _characterRepository.GetAllCharactersAsync();
            Characters = new ObservableCollection<BiblicalCharacter>(allCharacters);

            // Select first character by default
            if (Characters.Any())
            {
                SelectedCharacter = Characters.First();
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to load characters: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedCharacterChanged(BiblicalCharacter? value)
    {
        if (value != null)
        {
            _ = LoadEvolutionAsync(value);
        }
    }

    private async Task LoadEvolutionAsync(BiblicalCharacter character)
    {
        try
        {
            IsBusy = true;
            
            EvolutionSummary = await _learningService.GetEvolutionSummaryAsync(character);
            HasEvolution = EvolutionSummary.TotalRoundtables > 0;

            if (HasEvolution)
            {
                EvolutionDescription = BuildEvolutionDescription(EvolutionSummary);
            }
            else
            {
                EvolutionDescription = $"{character.Name} has not participated in any roundtable discussions yet. " +
                    "Roundtables help characters learn and grow from each other's perspectives.";
            }
        }
        catch (Exception ex)
        {
            EvolutionDescription = $"Could not load evolution data: {ex.Message}";
            HasEvolution = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string BuildEvolutionDescription(CharacterEvolutionSummary summary)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"📊 **Evolution Summary for {summary.CharacterName}**");
        sb.AppendLine();
        sb.AppendLine($"🎯 Participated in **{summary.TotalRoundtables}** roundtable discussions");
        sb.AppendLine($"💡 Gained **{summary.TotalInsightsGained}** insights from others");
        sb.AppendLine($"📚 Learned **{summary.TotalTeachingsLearned}** teachings");
        sb.AppendLine($"✨ Synthesized **{summary.SynthesizedWisdomCount}** wisdom pieces");
        sb.AppendLine();

        if (summary.TopInfluencers.Any())
        {
            sb.AppendLine("**Top Influences:**");
            foreach (var influencer in summary.TopInfluencers)
            {
                var stars = new string('⭐', Math.Min((int)(influencer.InfluenceScore * 5) + 1, 5));
                sb.AppendLine($"  • {influencer.CharacterName} {stars} ({influencer.TeachingsLearned} teachings)");
            }
            sb.AppendLine();
        }

        if (summary.RecentGrowthEvents.Any())
        {
            sb.AppendLine("**Recent Growth Events:**");
            foreach (var evt in summary.RecentGrowthEvents.Take(3))
            {
                var icon = evt.Type switch
                {
                    GrowthEventType.PerspectiveShift => "🔄",
                    GrowthEventType.NewInsight => "💡",
                    GrowthEventType.DeepAgreement => "🤝",
                    GrowthEventType.ProductiveConflict => "⚡",
                    GrowthEventType.SynthesizedWisdom => "✨",
                    GrowthEventType.ScripturalRevelation => "📖",
                    GrowthEventType.RelationshipGrowth => "❤️",
                    _ => "📍"
                };
                sb.AppendLine($"  {icon} {evt.Description}");
            }
        }

        return sb.ToString();
    }

    [RelayCommand]
    private async Task RefreshEvolution()
    {
        if (SelectedCharacter != null)
        {
            await LoadEvolutionAsync(SelectedCharacter);
        }
    }
}
