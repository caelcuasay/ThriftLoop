using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface ITransactionRepository
{
    /// <summary>Persists a new Transaction row and populates transaction.Id.</summary>
    Task AddAsync(Transaction transaction);

    /// <summary>
    /// Returns all transactions where the user is either the sender or receiver,
    /// ordered most-recent first.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetByUserIdAsync(int userId, int take = 50);

    /// <summary>
    /// Returns all transactions where the rider is the receiver (ToRiderId),
    /// ordered most-recent first.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetByRiderIdAsync(int riderId, int take = 50);

    /// <summary>
    /// Returns all transactions linked to a specific order, ordered by creation time.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetByOrderIdAsync(int orderId);
}