using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Authentication service for email, Google, and Apple sign-in (Hallow-style)
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Current authentication state
    /// </summary>
    AuthenticationState CurrentState { get; }
    
    /// <summary>
    /// Event fired when authentication state changes
    /// </summary>
    event EventHandler<AuthenticationState>? StateChanged;
    
    /// <summary>
    /// Sign in with email and password
    /// </summary>
    Task<AuthResult> SignInWithEmailAsync(string email, string password);
    
    /// <summary>
    /// Sign up with email and password
    /// </summary>
    Task<AuthResult> SignUpWithEmailAsync(string email, string password, string displayName);
    
    /// <summary>
    /// Sign in with Google OAuth
    /// </summary>
    Task<AuthResult> SignInWithGoogleAsync();
    
    /// <summary>
    /// Sign in with Apple
    /// </summary>
    Task<AuthResult> SignInWithAppleAsync();
    
    /// <summary>
    /// Sign out the current user
    /// </summary>
    Task SignOutAsync();
    
    /// <summary>
    /// Send password reset email
    /// </summary>
    Task<bool> SendPasswordResetAsync(string email);
    
    /// <summary>
    /// Check if user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Try to restore session from stored tokens
    /// </summary>
    Task<bool> TryRestoreSessionAsync();
}

/// <summary>
/// Authentication state
/// </summary>
public enum AuthenticationState
{
    Unknown,
    Unauthenticated,
    Authenticating,
    Authenticated
}

/// <summary>
/// Result of an authentication operation
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public AppUser? User { get; set; }
    public string? ErrorMessage { get; set; }
    public AuthProvider Provider { get; set; }
    public bool IsNewUser { get; set; }
    
    public static AuthResult Succeeded(AppUser user, AuthProvider provider, bool isNew = false) => new()
    {
        Success = true,
        User = user,
        Provider = provider,
        IsNewUser = isNew
    };
    
    public static AuthResult Failed(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}

/// <summary>
/// Authentication provider type
/// </summary>
public enum AuthProvider
{
    Email,
    Google,
    Apple,
    Anonymous
}
