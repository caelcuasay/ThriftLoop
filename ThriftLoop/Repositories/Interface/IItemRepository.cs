using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IItemRepository
{
    /// <summary>Persists a new Item row and populates item.Id via EF Core.</summary>
    Task AddAsync(Item item);
}