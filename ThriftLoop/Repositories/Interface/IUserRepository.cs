using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<bool> EmailExistsAsync(string email);
    Task<int> CreateAsync(User user);
    Task UpdateAsync(User user);
}