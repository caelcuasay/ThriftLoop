// Services/Implementation/ChatNotificationService.cs
using System.Collections.Concurrent;
using ThriftLoop.Services.Interface;

namespace ThriftLoop.Services.Implementation;

/// <summary>
/// In-memory service for tracking user online status and connection mappings.
/// Uses ConcurrentDictionary for thread-safe operations.
/// </summary>
public class ChatNotificationService : IChatNotificationService
{
    private readonly ILogger<ChatNotificationService> _logger;

    // Maps UserId -> List of ConnectionIds (a user can be connected from multiple tabs/devices)
    private readonly ConcurrentDictionary<int, List<string>> _userConnections = new();

    // Maps ConnectionId -> UserId for quick lookup during disconnect
    private readonly ConcurrentDictionary<string, int> _connectionToUser = new();

    // Maps UserId -> LastActiveTime (updated on activity)
    private readonly ConcurrentDictionary<int, DateTime> _lastActiveTimes = new();

    public ChatNotificationService(ILogger<ChatNotificationService> logger)
    {
        _logger = logger;
    }

    public Task UserConnectedAsync(int userId, string connectionId)
    {
        // Add to user connections
        _userConnections.AddOrUpdate(
            userId,
            new List<string> { connectionId },
            (key, existingList) =>
            {
                lock (existingList)
                {
                    if (!existingList.Contains(connectionId))
                        existingList.Add(connectionId);
                }
                return existingList;
            });

        // Add to connection mapping
        _connectionToUser[connectionId] = userId;

        // Update last active time
        _lastActiveTimes[userId] = DateTime.UtcNow;

        _logger.LogDebug("User {UserId} connected with connection {ConnectionId}. Total connections: {ConnectionCount}",
            userId, connectionId, _userConnections[userId].Count);

        return Task.CompletedTask;
    }

    public Task UserDisconnectedAsync(int userId, string connectionId)
    {
        // Remove from connection mapping
        _connectionToUser.TryRemove(connectionId, out _);

        // Remove from user connections
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _userConnections.TryRemove(userId, out _);
                }
            }
        }

        // Update last active time when user disconnects
        _lastActiveTimes[userId] = DateTime.UtcNow;

        _logger.LogDebug("User {UserId} disconnected with connection {ConnectionId}. Remaining connections: {RemainingCount}",
            userId, connectionId, _userConnections.ContainsKey(userId) ? _userConnections[userId].Count : 0);

        return Task.CompletedTask;
    }

    public Task<List<string>> GetUserConnectionIdsAsync(int userId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                return Task.FromResult(connections.ToList());
            }
        }

        return Task.FromResult(new List<string>());
    }

    public Task<bool> IsUserOnlineAsync(int userId)
    {
        var isOnline = _userConnections.ContainsKey(userId) && _userConnections[userId].Count > 0;
        return Task.FromResult(isOnline);
    }

    public Task<DateTime?> GetLastActiveTimeAsync(int userId)
    {
        if (_lastActiveTimes.TryGetValue(userId, out var lastActive))
            return Task.FromResult<DateTime?>(lastActive);

        return Task.FromResult<DateTime?>(null);
    }

    public Task UpdateLastActiveTimeAsync(int userId)
    {
        _lastActiveTimes[userId] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<Dictionary<int, DateTime>> GetOnlineUsersAsync()
    {
        var onlineUsers = new Dictionary<int, DateTime>();

        foreach (var userId in _userConnections.Keys)
        {
            if (_lastActiveTimes.TryGetValue(userId, out var lastActive))
                onlineUsers[userId] = lastActive;
            else
                onlineUsers[userId] = DateTime.UtcNow;
        }

        return Task.FromResult(onlineUsers);
    }

    public Task RemoveUserConnectionsAsync(int userId)
    {
        if (_userConnections.TryRemove(userId, out var connections))
        {
            lock (connections)
            {
                foreach (var connectionId in connections)
                {
                    _connectionToUser.TryRemove(connectionId, out _);
                }
            }
        }

        _lastActiveTimes.TryRemove(userId, out _);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the user ID associated with a connection ID.
    /// Helper method for hub to identify the user during disconnect.
    /// </summary>
    public int? GetUserIdByConnection(string connectionId)
    {
        if (_connectionToUser.TryGetValue(connectionId, out var userId))
            return userId;

        return null;
    }

    /// <summary>
    /// Cleans up stale connections (can be called periodically by a background service).
    /// </summary>
    public Task CleanupStaleConnectionsAsync(TimeSpan maxInactiveTime)
    {
        var cutoff = DateTime.UtcNow - maxInactiveTime;
        var usersToRemove = new List<int>();

        foreach (var kvp in _lastActiveTimes)
        {
            if (kvp.Value < cutoff && !_userConnections.ContainsKey(kvp.Key))
            {
                usersToRemove.Add(kvp.Key);
            }
        }

        foreach (var userId in usersToRemove)
        {
            _lastActiveTimes.TryRemove(userId, out _);
        }

        if (usersToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} stale user last active records", usersToRemove.Count);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the total number of online users.
    /// </summary>
    public int GetOnlineUserCount()
    {
        return _userConnections.Count;
    }

    /// <summary>
    /// Gets the total number of active connections.
    /// </summary>
    public int GetTotalConnectionCount()
    {
        return _connectionToUser.Count;
    }
}