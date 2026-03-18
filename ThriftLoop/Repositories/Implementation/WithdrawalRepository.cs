using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class WithdrawalRepository : IWithdrawalRepository
{
    private readonly ApplicationDbContext _context;

    public WithdrawalRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(Withdrawal withdrawal)
    {
        await _context.Withdrawals.AddAsync(withdrawal);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Withdrawal>> GetByUserIdAsync(int userId)
        => await _context.Withdrawals
                         .AsNoTracking()
                         .Where(w => w.UserId == userId)
                         .OrderByDescending(w => w.RequestedAt)
                         .ToListAsync();

    /// <inheritdoc />
    public async Task<Withdrawal?> GetByIdAsync(int id)
        => await _context.Withdrawals
                         .AsNoTracking()
                         .FirstOrDefaultAsync(w => w.Id == id);

    /// <inheritdoc />
    public async Task UpdateAsync(Withdrawal withdrawal)
    {
        _context.Withdrawals.Update(withdrawal);
        await _context.SaveChangesAsync();
    }
}