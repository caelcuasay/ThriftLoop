using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IWalletRepository
{
    /// <summary>
    /// Returns the wallet for <paramref name="userId"/>, or null if none exists yet.
    /// Use <see cref="IWalletService.GetOrCreateWalletAsync"/> for consumer code
    /// that should never see a null result.
    /// </summary>
    Task<Wallet?> GetByUserIdAsync(int userId);

    /// <summary>
    /// Returns the wallet for <paramref name="riderId"/>, or null if none exists yet.
    /// Queries by Wallets.RiderId — not UserId. These are separate tables.
    /// Use <see cref="IWalletService.GetOrCreateRiderWalletAsync"/> for consumer code
    /// that should never see a null result.
    /// </summary>
    Task<Wallet?> GetByRiderIdAsync(int riderId);

    /// <summary>Persists a brand-new Wallet row and populates wallet.Id.</summary>
    Task AddAsync(Wallet wallet);

    /// <summary>
    /// Saves all pending changes to an existing Wallet row.
    /// The caller must update <see cref="Wallet.UpdatedAt"/> before calling.
    /// </summary>
    Task UpdateAsync(Wallet wallet);
}