using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class ShopRepository : IShopRepository
{
    private readonly ApplicationDbContext _context;

    public ShopRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SellerProfile?> GetByUserIdAsync(int userId)
        => await _context.SellerProfiles
                         .AsNoTracking()  // This is fine for read-only operations
                         .FirstOrDefaultAsync(sp => sp.UserId == userId);

    public async Task<SellerProfile?> GetByIdAsync(int id)
    {
        // Remove AsNoTracking() so EF tracks changes
        return await _context.SellerProfiles
                             .FirstOrDefaultAsync(sp => sp.Id == id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SellerProfile>> GetAllApprovedAsync()
        => await _context.SellerProfiles
                         .AsNoTracking()  // Read-only is fine
                         .Where(sp => sp.ApplicationStatus == ApplicationStatus.Approved)
                         .OrderBy(sp => sp.ShopName)
                         .ToListAsync();

    public async Task CreateAsync(SellerProfile shop)
    {
        _context.SellerProfiles.Add(shop);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SellerProfile shop)
    {
        // No need to call Update() if the entity is already tracked
        // Just save changes
        await _context.SaveChangesAsync();
    }
}