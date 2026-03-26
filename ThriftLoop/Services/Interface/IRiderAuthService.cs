using ThriftLoop.DTOs.Auth;
using ThriftLoop.Models;

namespace ThriftLoop.Services.Auth.Interface;

public interface IRiderAuthService
{
    Task<Rider?> RegisterAsync(RiderRegisterDTO dto);
    Task<Rider?> ValidateCredentialsAsync(LoginDTO dto);
    Task<Rider?> GetByIdAsync(int id);
}