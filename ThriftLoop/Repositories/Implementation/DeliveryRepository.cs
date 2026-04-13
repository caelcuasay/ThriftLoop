// Repositories/Implementation/DeliveryRepository.cs
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Enums;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class DeliveryRepository : IDeliveryRepository
{
    private readonly ApplicationDbContext _context;

    public DeliveryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Delivery> CreateForOrderAsync(int orderId)
    {
        var delivery = new Delivery
        {
            OrderId = orderId,
            Status = DeliveryStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        _context.Deliveries.Add(delivery);
        await _context.SaveChangesAsync();
        return delivery;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Delivery>> GetAvailableDeliveriesAsync()
    {
        return await _context.Deliveries
            .AsNoTracking()
            .Include(d => d.Order)
                .ThenInclude(o => o.Item)
                    .ThenInclude(i => i.Shop)           // For shop items - get shop location
            .Include(d => d.Order)
                .ThenInclude(o => o.Item)
                    .ThenInclude(i => i.User)           // For P2P items - get user location
            .Include(d => d.Order)
                .ThenInclude(o => o.Buyer)              // Buyer location
            .Include(d => d.Order)
                .ThenInclude(o => o.Seller)             // Seller user (P2P seller)
            .Where(d => d.Status == DeliveryStatus.Available)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Delivery?> GetByIdWithDetailsAsync(int deliveryId)
    {
        return await _context.Deliveries
            .Include(d => d.Order)
                .ThenInclude(o => o.Item)
                    .ThenInclude(i => i.Shop)
            .Include(d => d.Order)
                .ThenInclude(o => o.Item)
                    .ThenInclude(i => i.User)
            .Include(d => d.Order)
                .ThenInclude(o => o.Buyer)
            .Include(d => d.Order)
                .ThenInclude(o => o.Seller)
            .Include(d => d.Rider)
            .FirstOrDefaultAsync(d => d.Id == deliveryId);
    }

    /// <inheritdoc />
    public async Task<Delivery?> GetActiveDeliveryByRiderIdAsync(int riderId)
    {
        return await _context.Deliveries
            .Include(d => d.Order)
                .ThenInclude(o => o.Item)
                    .ThenInclude(i => i.Shop)           // For shop items - get shop location
            .Include(d => d.Order)
                .ThenInclude(o => o.Item)
                    .ThenInclude(i => i.User)           // For P2P items - get user location
            .Include(d => d.Order)
                .ThenInclude(o => o.Buyer)              // Buyer location
            .Include(d => d.Order)
                .ThenInclude(o => o.Seller)             // Seller user (P2P seller)
            .FirstOrDefaultAsync(d => d.RiderId == riderId
                && d.Status != DeliveryStatus.Completed
                && d.Status != DeliveryStatus.Cancelled);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Delivery>> GetDeliveriesByRiderIdAsync(int riderId)
    {
        return await _context.Deliveries
            .AsNoTracking()
            .Include(d => d.Order)
                .ThenInclude(o => o.Item)
            .Where(d => d.RiderId == riderId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Delivery?> GetByOrderIdAsync(int orderId)
    {
        return await _context.Deliveries
            .Include(d => d.Order)
                .ThenInclude(o => o.Seller)
            .Include(d => d.Rider)
            .FirstOrDefaultAsync(d => d.OrderId == orderId);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Delivery delivery)
    {
        _context.Deliveries.Update(delivery);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<bool> AcceptDeliveryAsync(int deliveryId, int riderId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var delivery = await _context.Deliveries
                    .FirstOrDefaultAsync(d => d.Id == deliveryId);

                if (delivery == null || delivery.Status != DeliveryStatus.Available)
                    return false;

                var rider = await _context.Riders.FindAsync(riderId);
                if (rider == null)
                    return false;

                if (rider.ActiveDeliveryId.HasValue)
                    return false;

                delivery.RiderId = riderId;
                delivery.Status = DeliveryStatus.Accepted;
                delivery.AcceptedAt = DateTime.UtcNow;

                rider.ActiveDeliveryId = deliveryId;
                rider.ActiveDeliveryStartedAt = DateTime.UtcNow;

                _context.Deliveries.Update(delivery);
                _context.Riders.Update(rider);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<bool> MarkPickedUpAsync(int deliveryId, int riderId)
    {
        var delivery = await _context.Deliveries
            .FirstOrDefaultAsync(d => d.Id == deliveryId && d.RiderId == riderId);

        if (delivery == null || delivery.Status != DeliveryStatus.Accepted)
            return false;

        delivery.Status = DeliveryStatus.PickedUp;
        delivery.PickedUpAt = DateTime.UtcNow;

        _context.Deliveries.Update(delivery);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> MarkDeliveredAsync(int deliveryId, int riderId)
    {
        var delivery = await _context.Deliveries
            .FirstOrDefaultAsync(d => d.Id == deliveryId && d.RiderId == riderId);

        if (delivery == null || delivery.Status != DeliveryStatus.PickedUp)
            return false;

        delivery.Status = DeliveryStatus.Delivered;
        delivery.DeliveredAt = DateTime.UtcNow;

        _context.Deliveries.Update(delivery);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmByBuyerAsync(int deliveryId, int buyerId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var delivery = await _context.Deliveries
                    .Include(d => d.Order)
                    .FirstOrDefaultAsync(d => d.Id == deliveryId);

                if (delivery == null || delivery.Status != DeliveryStatus.Delivered)
                    return false;

                if (delivery.Order?.BuyerId != buyerId)
                    return false;

                delivery.Status = DeliveryStatus.Completed;
                delivery.ConfirmedByBuyerAt = DateTime.UtcNow;

                if (delivery.Order != null)
                {
                    delivery.Order.Status = OrderStatus.Completed;

                    if (delivery.Order.PaymentMethod == PaymentMethod.Cash && !delivery.Order.CashCollectedByRider)
                    {
                        delivery.Order.CashCollectedByRider = true;
                    }

                    _context.Orders.Update(delivery.Order);
                }

                if (delivery.RiderId.HasValue)
                {
                    var rider = await _context.Riders.FindAsync(delivery.RiderId.Value);
                    if (rider != null)
                    {
                        rider.ActiveDeliveryId = null;
                        rider.ActiveDeliveryStartedAt = null;
                        _context.Riders.Update(rider);
                    }
                }

                _context.Deliveries.Update(delivery);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
}