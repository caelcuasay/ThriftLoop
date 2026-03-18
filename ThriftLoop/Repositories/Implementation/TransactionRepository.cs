using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class TransactionRepository : ITransactionRepository
{
    private readonly ApplicationDbContext _context;

    public TransactionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(Transaction transaction)
    {
        await _context.Transactions.AddAsync(transaction);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> GetByUserIdAsync(int userId, int take = 50)
        => await _context.Transactions
                         .AsNoTracking()
                         .Include(t => t.Order)
                             .ThenInclude(o => o!.Item)
                         .Where(t => t.FromUserId == userId || t.ToUserId == userId)
                         .OrderByDescending(t => t.CreatedAt)
                         .Take(take)
                         .ToListAsync();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Transaction>> GetByOrderIdAsync(int orderId)
        => await _context.Transactions
                         .AsNoTracking()
                         .Where(t => t.OrderId == orderId)
                         .OrderBy(t => t.CreatedAt)
                         .ToListAsync();
}