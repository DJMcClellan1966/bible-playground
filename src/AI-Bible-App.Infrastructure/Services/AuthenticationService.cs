using AI_Bible_App.Core.Interfaces;
using AI_Bible_App.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AI_Bible_App.Infrastructure.Services;

/// <summary>
/// Authentication service supporting Email, Google, and Apple sign-in
/// Uses local storage for demo/offline mode - can be extended with Firebase/Azure AD B2C
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IUserService _userService;
    private readonly string _authStorePath;
    private AuthenticationState _currentState = AuthenticationState.Unknown;
    
    public event EventHandler<AuthenticationState>? StateChanged;
    
    public AuthenticationState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState != value)
            {
                _currentState = value;
                StateChanged?.Invoke(this, value);
            }
        }
    }
    
    public bool IsAuthenticated => CurrentState == AuthenticationState.Authenticated;
    
    public AuthenticationService(IUserService userService)
    {
        _userService = userService;
        _authStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AI-Bible-App", "auth_session.json");
    }
    
    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            CurrentState = AuthenticationState.Authenticating;
            
            if (File.Exists(_authStorePath))
            {
                var json = await File.ReadAllTextAsync(_authStorePath);
                var session = JsonSerializer.Deserialize<AuthSession>(json);
                
                if (session != null && !string.IsNullOrEmpty(session.UserId) && session.ExpiresAt > DateTime.UtcNow)
                {
                    await _userService.SwitchUserAsync(session.UserId);
                    
                    if (_userService.CurrentUser != null)
                    {
                        CurrentState = AuthenticationState.Authenticated;
                        return true;
                    }
                }
            }
            
            CurrentState = AuthenticationState.Unauthenticated;
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Error restoring session: {ex.Message}");
            CurrentState = AuthenticationState.Unauthenticated;
            return false;
        }
    }
    
    public async Task<AuthResult> SignInWithEmailAsync(string email, string password)
    {
        try
        {
            CurrentState = AuthenticationState.Authenticating;
            
            // Normalize email
            email = email.Trim().ToLowerInvariant();
            
            // Find user by email
            var users = await _userService.GetAllUsersAsync();
            var existingUser = users.FirstOrDefault(u => 
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                u.AuthProvider == "Email");
            
            if (existingUser == null)
            {
                CurrentState = AuthenticationState.Unauthenticated;
                return AuthResult.Failed("No account found with this email. Please sign up first.");
            }
            
            // Verify password
            var passwordHash = HashPassword(password);
            if (existingUser.PasswordHash != passwordHash)
            {
                CurrentState = AuthenticationState.Unauthenticated;
                return AuthResult.Failed("Incorrect password. Please try again.");
            }
            
            // Success - switch to user and save session
            await _userService.SwitchUserAsync(existingUser.Id);
            await SaveSessionAsync(existingUser.Id, AuthProvider.Email);
            
            CurrentState = AuthenticationState.Authenticated;
            return AuthResult.Succeeded(existingUser, AuthProvider.Email);
        }
        catch (Exception ex)
        {
            CurrentState = AuthenticationState.Unauthenticated;
            return AuthResult.Failed($"Sign in failed: {ex.Message}");
        }
    }
    
    public async Task<AuthResult> SignUpWithEmailAsync(string email, string password, string displayName)
    {
        try
        {
            CurrentState = AuthenticationState.Authenticating;
            
            // Validate inputs
            email = email.Trim().ToLowerInvariant();
            
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                CurrentState = AuthenticationState.Unauthenticated;
                return AuthResult.Failed("Please enter a valid email address.");
            }
            
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                CurrentState = AuthenticationState.Unauthenticated;
                return AuthResult.Failed("Password must be at least 6 characters.");
            }
            
            if (string.IsNullOrWhiteSpace(displayName))
            {
                CurrentState = AuthenticationState.Unauthenticated;
                return AuthResult.Failed("Please enter your name.");
            }
            
            // Check if email already exists
            var users = await _userService.GetAllUsersAsync();
            if (users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                CurrentState = AuthenticationState.Unauthenticated;
                return AuthResult.Failed("An account with this email already exists. Please sign in instead.");
            }
            
            // Create new user
            var newUser = await _userService.CreateUserAsync(displayName);
            
            // Update with email auth details
            await _userService.UpdateCurrentUserAsync(user =>
            {
                user.Email = email;
                user.AuthProvider = "Email";
                user.PasswordHash = HashPassword(password);
                user.AvatarEmoji = GetRandomAvatar();
            });
            
            await SaveSessionAsync(newUser.Id, AuthProvider.Email);
            
            CurrentState = AuthenticationState.Authenticated;
            return AuthResult.Succeeded(_userService.CurrentUser!, AuthProvider.Email, isNew: true);
        }
        catch (Exception ex)
        {
            CurrentState = AuthenticationState.Unauthenticated;
            return AuthResult.Failed($"Sign up failed: {ex.Message}");
        }
    }
    
    public async Task<AuthResult> SignInWithGoogleAsync()
    {
        try
        {
            CurrentState = AuthenticationState.Authenticating;
            
            // For demo/development, simulate Google sign in
            // In production, configure actual Google OAuth credentials
            await Task.Delay(800); // Simulate network delay
            
            var googleEmail = "demo.google@gmail.com";
            var googleName = "Google User";
            
            return await CreateOrSignInExternalUserAsync(googleEmail, googleName, AuthProvider.Google, $"google_{Guid.NewGuid():N}");
        }
        catch (Exception ex)
        {
            CurrentState = AuthenticationState.Unauthenticated;
            return AuthResult.Failed($"Google sign in failed: {ex.Message}");
        }
    }
    
    public async Task<AuthResult> SignInWithAppleAsync()
    {
        try
        {
            CurrentState = AuthenticationState.Authenticating;
            
#if IOS || MACCATALYST
            // Use native Apple Sign In on iOS/Mac
            // Requires AppleSignInNative implementation
            await Task.Delay(500);
            var appleEmail = $"user_{Guid.NewGuid():N}@privaterelay.appleid.com";
            var appleName = "Apple User";
            return await CreateOrSignInExternalUserAsync(appleEmail, appleName, AuthProvider.Apple, Guid.NewGuid().ToString());
#else
            // Apple Sign In not available on this platform
            CurrentState = AuthenticationState.Unauthenticated;
            return AuthResult.Failed("Apple Sign In is only available on iOS and macOS devices.");
#endif
        }
        catch (Exception ex)
        {
            CurrentState = AuthenticationState.Unauthenticated;
            return AuthResult.Failed($"Apple sign in failed: {ex.Message}");
        }
    }
    
    public async Task SignOutAsync()
    {
        try
        {
            // Clear saved session
            if (File.Exists(_authStorePath))
            {
                File.Delete(_authStorePath);
            }
            
            await _userService.LogoutAsync();
            CurrentState = AuthenticationState.Unauthenticated;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Auth] Error signing out: {ex.Message}");
        }
    }
    
    public async Task<bool> SendPasswordResetAsync(string email)
    {
        try
        {
            email = email.Trim().ToLowerInvariant();
            
            var users = await _userService.GetAllUsersAsync();
            var user = users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            
            if (user == null)
            {
                // Don't reveal if email exists
                return true;
            }
            
            // In production, send actual password reset email
            // For demo, just log it
            System.Diagnostics.Debug.WriteLine($"[Auth] Password reset requested for: {email}");
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<AuthResult> CreateOrSignInExternalUserAsync(string email, string displayName, AuthProvider provider, string externalId)
    {
        var users = await _userService.GetAllUsersAsync();
        var providerName = provider.ToString();
        
        // Check if user already exists with this external ID
        var existingUser = users.FirstOrDefault(u => 
            u.ExternalAuthId == externalId ||
            (u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && u.AuthProvider == providerName));
        
        if (existingUser != null)
        {
            // Existing user - sign in
            await _userService.SwitchUserAsync(existingUser.Id);
            await SaveSessionAsync(existingUser.Id, provider);
            
            CurrentState = AuthenticationState.Authenticated;
            return AuthResult.Succeeded(existingUser, provider);
        }
        
        // New user - create account
        var newUser = await _userService.CreateUserAsync(displayName);
        
        await _userService.UpdateCurrentUserAsync(user =>
        {
            user.Email = email;
            user.AuthProvider = providerName;
            user.ExternalAuthId = externalId;
            user.AvatarEmoji = GetRandomAvatar();
        });
        
        await SaveSessionAsync(newUser.Id, provider);
        
        CurrentState = AuthenticationState.Authenticated;
        return AuthResult.Succeeded(_userService.CurrentUser!, provider, isNew: true);
    }
    
    private async Task SaveSessionAsync(string userId, AuthProvider provider)
    {
        var session = new AuthSession
        {
            UserId = userId,
            Provider = provider.ToString(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30) // 30-day session
        };
        
        var directory = Path.GetDirectoryName(_authStorePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_authStorePath, json);
    }
    
    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "VoicesOfScripture_Salt_2024"));
        return Convert.ToBase64String(bytes);
    }
    
    private static string GetRandomAvatar()
    {
        var avatars = new[] { "😇", "🙏", "✝️", "📖", "🕊️", "💜", "🌟", "☀️", "🌸", "💫" };
        return avatars[Random.Shared.Next(avatars.Length)];
    }
    
    private class AuthSession
    {
        public string UserId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
