// Services/Interface/IChatNotificationService.cs
namespace ThriftLoop.Services.Interface;

/// <summary>
/// Service for tracking user online status and sending real-time notifications.
/// Used by SignalR hub to manage connection state.
/// </summary>
public interface IChatNotificationService
{
    /// <summary>
    /// Adds a user to the online users collection when they connect.
    /// </summary>
    Task UserConnectedAsync(int userId, string connectionId);

    /// <summary>
    /// Removes a user from the online users collection when they disconnect.
    /// </summary>
    Task UserDisconnectedAsync(int userId, string connectionId);

    /// <summary>
    /// Gets all active connection IDs for a user.
    /// </summary>
    Task<List<string>> GetUserConnectionIdsAsync(int userId);

    /// <summary>
    /// Checks if a user is currently online (has at least one active connection).
    /// </summary>
    Task<bool> IsUserOnlineAsync(int userId);

    /// <summary>
    /// Gets the last active time for a user.
    /// </summary>
    Task<DateTime?> GetLastActiveTimeAsync(int userId);

    /// <summary>
    /// Updates the last active time for a user.
    /// </summary>
    Task UpdateLastActiveTimeAsync(int userId);

    /// <summary>
    /// Gets a dictionary of online user IDs and their last active times.
    /// </summary>
    Task<Dictionary<int, DateTime>> GetOnlineUsersAsync();

    /// <summary>
    /// Removes all connections for a user (cleanup).
    /// </summary>
    Task RemoveUserConnectionsAsync(int userId);
}