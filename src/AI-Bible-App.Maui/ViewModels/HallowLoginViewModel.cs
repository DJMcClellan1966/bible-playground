using AI_Bible_App.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_Bible_App.Maui.ViewModels;

public partial class HallowLoginViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authService;
    private readonly IUserService _userService;
    
    [ObservableProperty]
    private string errorMessage = string.Empty;
    
    [ObservableProperty]
    private bool hasError;
    
    public HallowLoginViewModel(IAuthenticationService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }
    
    public async Task CheckExistingSessionAsync()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            
            // Try to restore existing session if available
            var restored = await _authService.TryRestoreSessionAsync();
            
            if (restored && _authService.IsAuthenticated)
            {
                // Session restored - go to home
                await Shell.Current.GoToAsync("//home");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HallowLogin] Error checking session: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    /// <summary>
    /// Navigate to the onboarding flow for new users
    /// </summary>
    [RelayCommand]
    private async Task TryForFree()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            await Shell.Current.GoToAsync("//onboarding");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An error occurred: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    /// <summary>
    /// Show login options for existing users
    /// </summary>
    [RelayCommand]
    private async Task ShowLogin()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            await Shell.Current.GoToAsync("//existinglogin");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"An error occurred: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    private async Task OpenTerms()
    {
        try
        {
            await Browser.OpenAsync("https://voicesofscripture.com/terms", BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            // Fallback - do nothing
        }
    }
    
    [RelayCommand]
    private async Task OpenPrivacy()
    {
        try
        {
            await Browser.OpenAsync("https://voicesofscripture.com/privacy", BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            // Fallback - do nothing
        }
    }
}
