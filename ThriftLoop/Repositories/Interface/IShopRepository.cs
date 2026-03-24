using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IShopRepository
{
    /// <summary>
    /// Returns the SellerProfile for the given user, or null if one does not exist.
    /// </summary>
    Task<SellerProfile?> GetByUserIdAsync(int userId);

    /// <summary>Returns the SellerProfile by its own primary key, or null if not found.</summary>
    Task<SellerProfile?> GetByIdAsync(int id);

    /// <summary>
    /// Returns all approved SellerProfiles ordered by shop name.
    /// Used by the Sellers discovery page to populate the shop suggestion row.
    /// </summary>
    Task<IReadOnlyList<SellerProfile>> GetAllApprovedAsync();

    /// <summary>Persists a brand-new SellerProfile row.</summary>
    Task CreateAsync(SellerProfile shop);

    /// <summary>Saves changes to an existing SellerProfile row.</summary>
    Task UpdateAsync(SellerProfile shop);
}