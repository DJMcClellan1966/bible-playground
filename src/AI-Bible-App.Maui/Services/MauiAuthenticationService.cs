using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using AI_Bible_App.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Storage;
using System.Web;
#if WINDOWS
using Windows.ApplicationModel;
using Windows.Security.Authentication.Web;
#endif
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AI_Bible_App.Maui.Services;

public class MauiAuthenticationService : IAuthenticationService
{
    private readonly FirebaseAuthenticationService _firebaseAuth;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient = new();

    public AuthenticationState CurrentState { get; private set; } = AuthenticationState.Unknown;
    public event EventHandler<AuthenticationState>? StateChanged;
    public bool IsAuthenticated => CurrentState == AuthenticationState.Authenticated;

    public MauiAuthenticationService(FirebaseAuthenticationService firebaseAuth, IConfiguration configuration)
    {
        _firebaseAuth = firebaseAuth;
        _configuration = configuration;
        _firebaseAuth.StateChanged += (_, state) =>
        {
            CurrentState = state;
            StateChanged?.Invoke(this, state);
        };
    }

    public Task<AuthResult> SignInWithEmailAsync(string email, string password)
        => _firebaseAuth.SignInWithEmailAsync(email, password);

    public Task<AuthResult> SignUpWithEmailAsync(string email, string password, string displayName)
        => _firebaseAuth.SignUpWithEmailAsync(email, password, displayName);

    public async Task<AuthResult> SignInWithGoogleAsync()
    {
        var clientId = _configuration["Google:ClientId"];
        var redirectUri = OperatingSystem.IsWindows()
            ? (_configuration["Google:WindowsRedirectUri"] ?? _configuration["Google:RedirectUri"] ?? GetWindowsRedirectUriFallback())
            : _configuration["Google:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
        {
            return AuthResult.Failed("Google client ID or redirect URI not configured.");
        }

        if (OperatingSystem.IsWindows() && redirectUri.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
        {
            var fallback = GetWindowsRedirectUriFallback();
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                redirectUri = fallback;
            }
            else
            {
                return AuthResult.Failed("Google sign-in on Windows requires a ms-app:// redirect URI.");
            }
        }

        try
        {
            var codeVerifier = CreateCodeVerifier();
            var codeChallenge = CreateCodeChallenge(codeVerifier);

            var authUrl = new Uri(
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&response_type=code" +
                "&scope=openid%20email%20profile" +
                $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
                "&code_challenge_method=S256" +
                "&prompt=select_account");
            string? code = null;

            if (OperatingSystem.IsWindows())
            {
#if WINDOWS
                var result = await WebAuthenticationBroker.AuthenticateAsync(
                    WebAuthenticationOptions.None,
                    authUrl,
                    new Uri(redirectUri));

                if (result.ResponseStatus == WebAuthenticationStatus.Success)
                {
                    code = GetQueryParam(result.ResponseData, "code");
                }
                else if (result.ResponseStatus == WebAuthenticationStatus.UserCancel)
                {
                    return AuthResult.Failed("Google sign-in was cancelled.");
                }
                else
                {
                    return AuthResult.Failed("Google sign-in failed to complete on Windows.");
                }
#else
                return AuthResult.Failed("Google sign-in isn't supported on Windows in this build.");
#endif
            }
            else
            {
                var authResult = await WebAuthenticator.AuthenticateAsync(authUrl, new Uri(redirectUri));
                authResult.Properties.TryGetValue("code", out code);
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return AuthResult.Failed("Google sign-in did not return a code.");
            }

            var tokenResponse = await ExchangeCodeForTokensAsync(code, codeVerifier, clientId, redirectUri);
            if (string.IsNullOrWhiteSpace(tokenResponse.IdToken))
            {
                return AuthResult.Failed("Google sign-in did not return an ID token.");
            }

            var firebaseKey = _configuration["Firebase:ApiKey"];
            if (string.IsNullOrWhiteSpace(firebaseKey))
            {
                return AuthResult.Failed("Firebase ApiKey not configured. Set Firebase:ApiKey in appsettings.json.");
            }

            var firebaseResult = await SignInWithFirebaseIdpAsync(firebaseKey, tokenResponse.IdToken);
            if (firebaseResult.Success)
            {
                CurrentState = AuthenticationState.Authenticated;
                StateChanged?.Invoke(this, CurrentState);
            }

            return firebaseResult;
        }
        catch (TaskCanceledException)
        {
            return AuthResult.Failed("Google sign-in was cancelled.");
        }
        catch (Exception ex)
        {
            return AuthResult.Failed($"Google sign-in failed: {ex.Message}");
        }
    }

    private static string? GetQueryParam(string url, string key)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var uri = new Uri(url);
            var query = uri.Query;
            var fragment = uri.Fragment;
            if (!string.IsNullOrWhiteSpace(query))
            {
                var parsedQuery = HttpUtility.ParseQueryString(query);
                var value = parsedQuery[key];
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            if (!string.IsNullOrWhiteSpace(fragment))
            {
                var fragmentQuery = fragment.StartsWith("#", StringComparison.Ordinal)
                    ? fragment[1..]
                    : fragment;
                var parsedFragment = HttpUtility.ParseQueryString(fragmentQuery);
                return parsedFragment[key];
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? GetWindowsRedirectUriFallback()
    {
#if WINDOWS
        try
        {
            var familyName = Package.Current?.Id?.FamilyName;
            if (!string.IsNullOrWhiteSpace(familyName))
                return $"ms-app://{familyName}";
        }
        catch
        {
            // Ignore fallback errors
        }
#endif
        return null;
    }

    public Task<AuthResult> SignInWithAppleAsync() => _firebaseAuth.SignInWithAppleAsync();

    public async Task SignOutAsync()
    {
        await _firebaseAuth.SignOutAsync();
        CurrentState = AuthenticationState.Unauthenticated;
        StateChanged?.Invoke(this, CurrentState);
    }

    public Task<bool> SendPasswordResetAsync(string email)
        => _firebaseAuth.SendPasswordResetAsync(email);

    public Task<bool> TryRestoreSessionAsync()
        => _firebaseAuth.TryRestoreSessionAsync();

    private async Task<AuthResult> SignInWithFirebaseIdpAsync(string firebaseApiKey, string idToken)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={firebaseApiKey}";
        var payload = new
        {
            postBody = $"id_token={idToken}&providerId=google.com",
            requestUri = "http://localhost",
            returnIdpCredential = true,
            returnSecureToken = true
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return AuthResult.Failed(ParseFirebaseError(error));
        }

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonDocument.Parse(json).RootElement;
        var user = new AppUser
        {
            Email = data.GetPropertyOrDefault("email") ?? string.Empty,
            Name = data.GetPropertyOrDefault("displayName") ?? string.Empty,
            Id = data.GetPropertyOrDefault("localId") ?? string.Empty
        };

        var isNewUser = data.TryGetProperty("isNewUser", out var isNew) && isNew.GetBoolean();
        return AuthResult.Succeeded(user, AuthProvider.Google, isNewUser);
    }

    private static string ParseFirebaseError(string rawError)
    {
        try
        {
            var root = JsonDocument.Parse(rawError).RootElement;
            if (root.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "Google sign-in failed.";
            }
        }
        catch
        {
            // ignore
        }

        return "Google sign-in failed.";
    }

    private static string CreateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private async Task<(string IdToken, string AccessToken)> ExchangeCodeForTokensAsync(
        string code,
        string codeVerifier,
        string clientId,
        string redirectUri)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Token exchange failed: {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(json).RootElement;
        var idToken = root.GetPropertyOrDefault("id_token") ?? string.Empty;
        var accessToken = root.GetPropertyOrDefault("access_token") ?? string.Empty;
        return (idToken, accessToken);
    }
}
