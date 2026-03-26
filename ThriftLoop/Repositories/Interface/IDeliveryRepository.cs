using ThriftLoop.Models;
using ThriftLoop.Enums;

namespace ThriftLoop.Repositories.Interface;

public interface IDeliveryRepository
{
    /// <summary>Creates a new delivery for an order at checkout time.</summary>
    Task<Delivery> CreateForOrderAsync(int orderId);

    /// <summary>Gets all available deliveries (Status = Available).</summary>
    Task<IReadOnlyList<Delivery>> GetAvailableDeliveriesAsync();

    /// <summary>Gets a delivery by ID with Order and Rider loaded.</summary>
    Task<Delivery?> GetByIdWithDetailsAsync(int deliveryId);

    /// <summary>Gets the current active delivery for a rider, if any.</summary>
    Task<Delivery?> GetActiveDeliveryByRiderIdAsync(int riderId);

    /// <summary>Gets all deliveries assigned to a rider (history).</summary>
    Task<IReadOnlyList<Delivery>> GetDeliveriesByRiderIdAsync(int riderId);

    /// <summary>Gets the delivery for a specific order.</summary>
    Task<Delivery?> GetByOrderIdAsync(int orderId);

    /// <summary>Updates a delivery's status and timestamps.</summary>
    Task UpdateAsync(Delivery delivery);

    /// <summary>Accepts a delivery by a rider (status → Accepted).</summary>
    Task<bool> AcceptDeliveryAsync(int deliveryId, int riderId);

    /// <summary>Marks a delivery as picked up (status → PickedUp).</summary>
    Task<bool> MarkPickedUpAsync(int deliveryId, int riderId);

    /// <summary>Marks a delivery as delivered (status → Delivered).</summary>
    Task<bool> MarkDeliveredAsync(int deliveryId, int riderId);

    /// <summary>Marks a delivery as completed by buyer (status → Completed).</summary>
    Task<bool> ConfirmByBuyerAsync(int deliveryId, int buyerId);
}