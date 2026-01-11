using AI_Bible_App.Core.Models;

namespace AI_Bible_App.Core.Interfaces;

/// <summary>
/// Repository for managing Bible reading plans and user progress
/// </summary>
public interface IReadingPlanRepository
{
    /// <summary>
    /// Get all available reading plans
    /// </summary>
    Task<List<ReadingPlan>> GetAllPlansAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific reading plan by ID
    /// </summary>
    Task<ReadingPlan?> GetPlanByIdAsync(string planId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get plans filtered by type
    /// </summary>
    Task<List<ReadingPlan>> GetPlansByTypeAsync(ReadingPlanType type, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the user's active reading progress (if any)
    /// </summary>
    Task<UserReadingProgress?> GetActiveProgressAsync(string userId = "default", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all progress records for a user (current and completed)
    /// </summary>
    Task<List<UserReadingProgress>> GetAllProgressAsync(string userId = "default", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Start a new reading plan for a user
    /// </summary>
    Task<UserReadingProgress> StartPlanAsync(string planId, string userId = "default", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Mark a day as completed
    /// </summary>
    Task<UserReadingProgress> MarkDayCompletedAsync(string progressId, int dayNumber, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Unmark a day (if user wants to re-read)
    /// </summary>
    Task<UserReadingProgress> UnmarkDayAsync(string progressId, int dayNumber, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save a note for a specific day
    /// </summary>
    Task SaveDayNoteAsync(string progressId, int dayNumber, string note, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update the current day position
    /// </summary>
    Task UpdateCurrentDayAsync(string progressId, int dayNumber, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Abandon/delete a reading plan progress
    /// </summary>
    Task DeleteProgressAsync(string progressId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get today's reading for active plan
    /// </summary>
    Task<ReadingPlanDay?> GetTodaysReadingAsync(string userId = "default", CancellationToken cancellationToken = default);
}
