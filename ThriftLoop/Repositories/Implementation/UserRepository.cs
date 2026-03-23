using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
        => await _context.Users
                         .AsNoTracking()
                         .FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User?> GetByIdAsync(int id)
        => await _context.Users
                         .AsNoTracking()
                         .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<bool> EmailExistsAsync(string email)
        => await _context.Users
                         .AsNoTracking()
                         .AnyAsync(u => u.Email == email);

    public async Task<int> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>
    /// Attaches the detached entity (from an AsNoTracking read) and saves all changes.
    /// Safe to call after any AsNoTracking read — EF will mark the entity as Modified.
    /// </summary>
    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
}