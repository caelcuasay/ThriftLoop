using ThriftLoop.DTOs.Auth;
using ThriftLoop.Models;
using ThriftLoop.DTOs.Auth;

namespace ThriftLoop.Services.Auth.Interface;

public interface IRiderAuthService
{
    Task<Rider?> RegisterAsync(RiderRegisterDTO dto);
    Task<Rider?> ValidateCredentialsAsync(LoginDTO dto);
    Task<Rider?> GetByIdAsync(int id);

    // NEW methods for editing rejected application
    Task<Rider?> GetRejectedApplicationAsync(int id);
    Task<bool> UpdateApplicationAsync(RiderEditDTO dto);
}