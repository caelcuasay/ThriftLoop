// DTOs/User/AddressCoordinatesDTO.cs
namespace ThriftLoop.DTOs.User;

/// <summary>
/// Represents address coordinates for geolocation.
/// Used when updating or retrieving address coordinates.
/// </summary>
public class AddressCoordinatesDTO
{
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}