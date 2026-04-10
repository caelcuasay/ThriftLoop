using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class RiderRepository : IRiderRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RiderRepository> _logger;

    public RiderRepository(ApplicationDbContext context, ILogger<RiderRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Rider?> GetByEmailAsync(string email)
    {
        try
        {
            // Use AsNoTracking and FirstOrDefault with timeout
            return await _context.Riders
                .AsNoTracking()
                .Where(r => r.Email == email)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rider by email: {Email}", email);
            throw;
        }
    }

    public async Task<Rider?> GetByIdAsync(int id)
    {
        try
        {
            return await _context.Riders
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rider by ID: {Id}", id);
            throw;
        }
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        try
        {
            // Use AnyAsync which is more efficient than Count > 0
            return await _context.Riders
                .AsNoTracking()
                .AnyAsync(r => r.Email == email)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking email existence: {Email}", email);
            throw;
        }
    }

    public async Task<int> CreateAsync(Rider rider)
    {
        try
        {
            _context.Riders.Add(rider);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            return rider.Id;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error creating rider with email: {Email}", rider.Email);
            throw;
        }
    }

    public async Task UpdateAsync(Rider rider)
    {
        try
        {
            _context.Riders.Update(rider);
            await _context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error updating rider ID: {Id}", rider.Id);
            throw;
        }
    }
}