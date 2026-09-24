using EquaMeridian.DTOs.Auth;

public interface IAuthService
{
    Task<LoginOutcome?> LoginAsync(LoginRequest dto, string ipAddress);
    Task<LoginResponse?> VerifyOtpAsync(VerifyOtpRequest dto, string ipAddress);
    Task<ResendOtpOutcome> ResendOtpAsync(ResendOtpRequest dto, string ipAddress);
    Task<(bool Success, string Message)> SetTwoFactorEnabledAsync(int userId, bool enabled, string password);
    Task LogoutAsync(int userId, string token);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(string email, string ipAddress);
    Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordRequest dto, string ipAddress);
    Task<(bool Success, string Message)> RegisterAsync(RegisterRequest dto, string ipAddress);
    Task<(bool Success, string Message)> RegisterSupplierAsync(RegisterSupplierRequest dto, string ipAddress);
}