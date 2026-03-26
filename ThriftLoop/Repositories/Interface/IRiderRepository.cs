using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IRiderRepository
{
    Task<Rider?> GetByEmailAsync(string email);
    Task<Rider?> GetByIdAsync(int id);
    Task<bool> EmailExistsAsync(string email);
    Task<int> CreateAsync(Rider rider);
    Task UpdateAsync(Rider rider);
}