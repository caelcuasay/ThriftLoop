namespace ThriftLoop.Models;

/// <summary>
/// Single-row table for admin-configurable site-wide settings.
/// Contains the GCash QR code image path and other global config.
/// </summary>
public class SiteSettings
{
    public int Id { get; set; }

    /// <summary>
    /// File path to the uploaded GCash QR code image (relative to wwwroot).
    /// Example: "/uploads/qr/gcash-qr.png"
    /// </summary>
    public string? GCashQRCodePath { get; set; }

    /// <summary>
    /// When the QR code was last updated.
    /// </summary>
    public DateTime? QRCodeUpdatedAt { get; set; }

    /// <summary>
    /// Admin who uploaded the QR code.
    /// </summary>
    public int? QRCodeUpdatedBy { get; set; }

    /// <summary>
    /// Expected GCash account number that receipts must match.
    /// Used for validation of screenshot transactions.
    /// </summary>
    public string? GCashAccountNumber { get; set; }

    /// <summary>
    /// When the GCash account number was last updated.
    /// </summary>
    public DateTime? AccountNumberUpdatedAt { get; set; }

    /// <summary>
    /// Admin who updated the GCash account number.
    /// </summary>
    public int? AccountNumberUpdatedBy { get; set; }
}