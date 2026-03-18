using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IWithdrawalRepository
{
    /// <summary>Persists a new Withdrawal request and populates withdrawal.Id.</summary>
    Task AddAsync(Withdrawal withdrawal);

    /// <summary>
    /// Returns all withdrawal requests for a user, ordered most-recent first.
    /// </summary>
    Task<IReadOnlyList<Withdrawal>> GetByUserIdAsync(int userId);

    /// <summary>Returns a single withdrawal by primary key, or null if not found.</summary>
    Task<Withdrawal?> GetByIdAsync(int id);

    /// <summary>Saves status or completion date changes to an existing Withdrawal row.</summary>
    Task UpdateAsync(Withdrawal withdrawal);
}