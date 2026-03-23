namespace ThriftLoop.Enums;

/// <summary>
/// The role this account holds in the system.
/// A single User row can be elevated to Seller after admin approval.
/// Rider accounts are strictly isolated — they cannot buy or sell.
/// </summary>
public enum UserRole
{
    /// <summary>Default on registration. Can browse and buy items (P2P).</summary>
    User,

    /// <summary>
    /// Elevated from User after admin approves their SellerProfile application.
    /// Retains all User capabilities plus shop management and bulk listing.
    /// </summary>
    Seller,

    /// <summary>
    /// Handles deliveries only. Cannot buy, sell, or apply as a seller.
    /// Registered separately and approved by admin before activation.
    /// </summary>
    Rider,

    /// <summary>Platform administrator. Approves Sellers and Riders, handles disputes.</summary>
    Admin
}

/// <summary>
/// Tracks where a Seller application is in the admin review pipeline.
/// User.Role is only elevated to Seller once this reaches Approved.
/// </summary>
public enum ApplicationStatus
{
    /// <summary>Application submitted, awaiting admin review.</summary>
    Pending,

    /// <summary>Admin approved — User.Role has been set to Seller.</summary>
    Approved,

    /// <summary>Admin rejected. User remains a regular User and may re-apply.</summary>
    Rejected
}