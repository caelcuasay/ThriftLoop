using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class RiderRepository : IRiderRepository
{
    private readonly ApplicationDbContext _context;

    public RiderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Rider?> GetByEmailAsync(string email)
        => await _context.Riders
                         .AsNoTracking()
                         .FirstOrDefaultAsync(r => r.Email == email);

    public async Task<Rider?> GetByIdAsync(int id)
        => await _context.Riders
                         .AsNoTracking()
                         .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<bool> EmailExistsAsync(string email)
        => await _context.Riders
                         .AsNoTracking()
                         .AnyAsync(r => r.Email == email);

    public async Task<int> CreateAsync(Rider rider)
    {
        _context.Riders.Add(rider);
        await _context.SaveChangesAsync();
        return rider.Id;
    }

    public async Task UpdateAsync(Rider rider)
    {
        _context.Riders.Update(rider);
        await _context.SaveChangesAsync();
    }
}