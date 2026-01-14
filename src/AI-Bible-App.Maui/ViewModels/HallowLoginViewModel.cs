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
            
            // Try to restore existing session
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
    
    [RelayCommand]
    private async Task SignInWithGoogle()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            
            var result = await _authService.SignInWithGoogleAsync();
            
            if (result.Success)
            {
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Google sign in failed";
                HasError = true;
            }
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
    private async Task SignInWithApple()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            
            var result = await _authService.SignInWithAppleAsync();
            
            if (result.Success)
            {
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Apple sign in failed";
                HasError = true;
            }
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
    private async Task ShowEmailSignIn()
    {
        await Shell.Current.GoToAsync("emailsignin");
    }
    
    [RelayCommand]
    private async Task ContinueAsGuest()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            
            // Create an anonymous guest user
            var guestName = $"Guest {Random.Shared.Next(1000, 9999)}";
            var user = await _userService.CreateUserAsync(guestName);
            
            await _userService.UpdateCurrentUserAsync(u =>
            {
                u.AuthProvider = "Anonymous";
                u.AvatarEmoji = "👤";
            });
            
            await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not continue as guest: {ex.Message}";
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
