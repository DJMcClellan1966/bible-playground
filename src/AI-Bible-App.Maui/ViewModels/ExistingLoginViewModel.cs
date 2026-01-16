using AI_Bible_App.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AI_Bible_App.Maui.ViewModels;

/// <summary>
/// ViewModel for existing user login.
/// Provides Google, Apple, and Email sign-in options for returning users.
/// </summary>
public partial class ExistingLoginViewModel : BaseViewModel
{
    private readonly IAuthenticationService _authService;
    
    [ObservableProperty]
    private string errorMessage = string.Empty;
    
    [ObservableProperty]
    private bool hasError;
    
    public ExistingLoginViewModel(IAuthenticationService authService)
    {
        _authService = authService;
        Title = "Welcome Back";
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
    private async Task ShowEmailSignIn()
    {
        await Shell.Current.GoToAsync("emailsignin?mode=signin");
    }
    
    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.GoToAsync("//login");
    }
}
