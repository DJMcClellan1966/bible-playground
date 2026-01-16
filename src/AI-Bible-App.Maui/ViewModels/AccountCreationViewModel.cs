using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;

namespace AI_Bible_App.Maui.ViewModels;

/// <summary>
/// ViewModel for account creation after onboarding.
/// Allows users to create accounts via Google, Apple, or Email.
/// </summary>
public partial class AccountCreationViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authService;
    private readonly IUserService _userService;
    
    [ObservableProperty]
    private string welcomeMessage = "Create your account";
    
    [ObservableProperty]
    private string welcomeSubtitle = "Sign up to save your journey";
    
    [ObservableProperty]
    private bool hasError;
    
    [ObservableProperty]
    private string errorMessage = string.Empty;
    
    [ObservableProperty]
    private OnboardingProfile? onboardingProfile;
    
    public AccountCreationViewModel(IAuthenticationService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
        Title = "Create Account";
        
        LoadOnboardingProfile();
    }
    
    private void LoadOnboardingProfile()
    {
        try
        {
            var json = Preferences.Get("onboarding_profile", string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                OnboardingProfile = JsonSerializer.Deserialize<OnboardingProfile>(json);
                
                // Personalize welcome message based on onboarding
                if (OnboardingProfile != null && !string.IsNullOrEmpty(OnboardingProfile.PreferredName))
                {
                    WelcomeMessage = $"Welcome, {OnboardingProfile.PreferredName}!";
                    WelcomeSubtitle = "Let's create your account";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountCreation] Error loading profile: {ex.Message}");
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
                await ApplyOnboardingProfile();
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Google sign in failed. Please try again.";
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
                await ApplyOnboardingProfile();
                await Shell.Current.GoToAsync("//home");
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Apple sign in failed. Please try again.";
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
    private async Task ShowEmailSignUp()
    {
        await Shell.Current.GoToAsync("emailsignin?mode=signup");
    }
    
    [RelayCommand]
    private async Task ContinueAsGuest()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            
            // Create a guest user
            var guestName = OnboardingProfile?.PreferredName ?? $"Guest {Random.Shared.Next(1000, 9999)}";
            var user = await _userService.CreateUserAsync(guestName);
            
            await _userService.UpdateCurrentUserAsync(u =>
            {
                u.AuthProvider = "Anonymous";
                u.AvatarEmoji = "👤";
            });
            
            await ApplyOnboardingProfile();

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
    
    private async Task ApplyOnboardingProfile()
    {
        if (OnboardingProfile == null || _userService.CurrentUser == null)
            return;
            
        try
        {
            await _userService.UpdateCurrentUserAsync(user =>
            {
                // Apply personalization from onboarding
                if (!string.IsNullOrEmpty(OnboardingProfile.PreferredName))
                {
                    user.Name = OnboardingProfile.PreferredName;
                }
                
                // Store onboarding data in user settings for personalization
                // This could be extended to store in a dedicated OnboardingProfile property
            });
            
            // Clear the temporary onboarding profile
            Preferences.Remove("onboarding_profile");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountCreation] Error applying profile: {ex.Message}");
        }
    }
    
    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.GoToAsync("//onboarding");
    }
}
