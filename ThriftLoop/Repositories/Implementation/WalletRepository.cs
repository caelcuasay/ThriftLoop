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
    public async Task AddAsync(Wallet wallet)
    {
        await _context.Wallets.AddAsync(wallet);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Wallet wallet)
    {
        // GetByUserIdAsync uses AsNoTracking, so each call returns a fresh
        // detached instance. When two wallet updates occur in the same
        // request (e.g. ReleaseEscrowAsync then PayRiderAsync both touch the
        // buyer wallet), the second Update() call finds the first instance
        // still in the DbContext change tracker and throws an identity conflict.
        // Detach any stale tracked instance before attaching the incoming one.
        var existing = _context.ChangeTracker
            .Entries<Wallet>()
            .FirstOrDefault(e => e.Entity.Id == wallet.Id);

        if (existing is not null)
            existing.State = EntityState.Detached;

        _context.Wallets.Update(wallet);
        await _context.SaveChangesAsync();
    }
}