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
                         .AsNoTracking()
                         .FirstOrDefaultAsync(sp => sp.UserId == userId);

    public async Task<SellerProfile?> GetByIdAsync(int id)
        => await _context.SellerProfiles
                         .AsNoTracking()
                         .FirstOrDefaultAsync(sp => sp.Id == id);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SellerProfile>> GetAllApprovedAsync()
        => await _context.SellerProfiles
                         .AsNoTracking()
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
        _context.SellerProfiles.Update(shop);
        await _context.SaveChangesAsync();
    }
}