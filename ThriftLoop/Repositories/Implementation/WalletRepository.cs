using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class WalletRepository : IWalletRepository
{
    private readonly ApplicationDbContext _context;

    public WalletRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Wallet?> GetByUserIdAsync(int userId)
        => await _context.Wallets
                         .AsNoTracking()
                         .FirstOrDefaultAsync(w => w.UserId == userId);

    /// <inheritdoc />
    public async Task<Wallet?> GetByRiderIdAsync(int riderId)
        => await _context.Wallets
                         .AsNoTracking()
                         .FirstOrDefaultAsync(w => w.RiderId == riderId);

    /// <inheritdoc />
    public async Task AddAsync(Wallet wallet)
    {
        // Guard: ensure the referenced User OR Rider actually exists before
        // inserting to prevent FK violations (FK_Wallets_Users_UserId or
        // FK_Wallets_Riders_RiderId) for accounts that haven't fully committed yet.
        if (wallet.UserId.HasValue)
        {
            bool userExists = await _context.Users.AnyAsync(u => u.Id == wallet.UserId.Value);
            if (!userExists)
                throw new InvalidOperationException(
                    $"Cannot create wallet: User with Id={wallet.UserId} does not exist.");
        }
        else if (wallet.RiderId.HasValue)
        {
            bool riderExists = await _context.Riders.AnyAsync(r => r.Id == wallet.RiderId.Value);
            if (!riderExists)
                throw new InvalidOperationException(
                    $"Cannot create wallet: Rider with Id={wallet.RiderId} does not exist.");
        }
        else
        {
            throw new InvalidOperationException(
                "Cannot create wallet: neither UserId nor RiderId is set.");
        }

        await _context.Wallets.AddAsync(wallet);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Wallet wallet)
    {
        // GetByUserIdAsync / GetByRiderIdAsync use AsNoTracking, so each call
        // returns a fresh detached instance. When two wallet updates occur in
        // the same request (e.g. ReleaseEscrowAsync then PayRiderAsync both
        // touch the buyer wallet), the second Update() call finds the first
        // instance still in the DbContext change tracker and throws an identity
        // conflict. Detach any stale tracked instance before attaching the new one.
        var existing = _context.ChangeTracker
            .Entries<Wallet>()
            .FirstOrDefault(e => e.Entity.Id == wallet.Id);

        if (existing is not null)
            existing.State = EntityState.Detached;

        _context.Wallets.Update(wallet);
        await _context.SaveChangesAsync();
    }
}