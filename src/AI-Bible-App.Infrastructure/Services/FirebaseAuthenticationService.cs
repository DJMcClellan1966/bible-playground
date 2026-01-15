using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace AI_Bible_App.Infrastructure.Services;

public class FirebaseAuthenticationService : IAuthenticationService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private string _apiKey;
    public event EventHandler<AuthenticationState>? StateChanged;
    public AuthenticationState CurrentState { get; private set; } = AuthenticationState.Unknown;
    public bool IsAuthenticated => CurrentState == AuthenticationState.Authenticated;

    public FirebaseAuthenticationService(IConfiguration config)
    {
        _config = config;
        _httpClient = new HttpClient();
        _apiKey = _config["Firebase:ApiKey"] ?? "";
    }

    public Task<AuthResult> SignInWithGoogleAsync() => Task.FromResult(AuthResult.Failed("Google sign-in not implemented yet."));
    public Task<AuthResult> SignInWithAppleAsync() => Task.FromResult(AuthResult.Failed("Apple sign-in not implemented yet."));
    public Task SignOutAsync() => Task.CompletedTask;
    public async Task<bool> SendPasswordResetAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return false;

        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_apiKey}";
        var payload = new
        {
            requestType = "PASSWORD_RESET",
            email
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload);
        if (response.IsSuccessStatusCode)
            return true;

        var error = await response.Content.ReadAsStringAsync();
        var errorCode = ParseFirebaseErrorCode(error);
        System.Diagnostics.Debug.WriteLine($"[Auth] Password reset failed: {errorCode ?? "Unknown"}");
        System.Diagnostics.Debug.WriteLine($"[Auth] Password reset raw response: {error}");

        // Don't reveal if the email exists.
        if (errorCode == "EMAIL_NOT_FOUND")
            return true;

        return false;
    }
    public Task<bool> TryRestoreSessionAsync() => Task.FromResult(false); // Not implemented yet

    public async Task<AuthResult> SignInWithEmailAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return AuthResult.Failed("Firebase ApiKey not configured. Set Firebase:ApiKey in appsettings.json.");

        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";
        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };
        var response = await _httpClient.PostAsJsonAsync(url, payload);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json).RootElement;
            var user = new AppUser
            {
                Email = email,
                Name = data.GetPropertyOrDefault("displayName") ?? string.Empty,
                Id = data.GetPropertyOrDefault("localId") ?? string.Empty
            };
            CurrentState = AuthenticationState.Authenticated;
            StateChanged?.Invoke(this, CurrentState);
            return AuthResult.Succeeded(user, AuthProvider.Email);
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            return AuthResult.Failed(MapFirebaseError(error, isSignUp: false));
        }
    }

    public async Task<AuthResult> SignUpWithEmailAsync(string email, string password, string displayName)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return AuthResult.Failed("Firebase ApiKey not configured. Set Firebase:ApiKey in appsettings.json.");

        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_apiKey}";
        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };
        var response = await _httpClient.PostAsJsonAsync(url, payload);
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json).RootElement;
            // Optionally update displayName
            await UpdateUserProfile(data.GetPropertyOrDefault("idToken") ?? string.Empty, displayName);
            var user = new AppUser
            {
                Email = email,
                Name = displayName,
                Id = data.GetPropertyOrDefault("localId") ?? string.Empty
            };
            CurrentState = AuthenticationState.Authenticated;
            StateChanged?.Invoke(this, CurrentState);
            return AuthResult.Succeeded(user, AuthProvider.Email, isNew: true);
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            return AuthResult.Failed(MapFirebaseError(error, isSignUp: true));
        }
    }

    private async Task UpdateUserProfile(string idToken, string displayName)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={_apiKey}";
        var payload = new
        {
            idToken,
            displayName,
            returnSecureToken = true
        };
        await _httpClient.PostAsJsonAsync(url, payload);
    }

    private static string MapFirebaseError(string rawError, bool isSignUp)
    {
        var errorCode = ParseFirebaseErrorCode(rawError);
        return errorCode switch
        {
            "EMAIL_NOT_FOUND" => "Invalid email or password.",
            "INVALID_PASSWORD" => "Invalid email or password.",
            "INVALID_LOGIN_CREDENTIALS" => "Invalid email or password.",
            "USER_DISABLED" => "This account has been disabled.",
            "EMAIL_EXISTS" => "An account already exists with this email.",
            "OPERATION_NOT_ALLOWED" => "Email sign-in is not enabled for this project.",
            "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many attempts. Please try again later.",
            "WEAK_PASSWORD" => "Password is too weak. Use at least 6 characters.",
            "INVALID_EMAIL" => "Please enter a valid email address.",
            _ => isSignUp ? "Sign up failed. Please check your details and try again."
                : "Sign in failed. Please check your email and password."
        };
    }

    private static string? ParseFirebaseErrorCode(string rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError))
            return null;

        try
        {
            var root = JsonDocument.Parse(rawError).RootElement;
            if (root.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch
        {
            // Ignore parse errors and fall back to generic message.
        }

        return null;
    }
}

public static class JsonElementExtensions
{
    public static string? GetPropertyOrDefault(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }
}
