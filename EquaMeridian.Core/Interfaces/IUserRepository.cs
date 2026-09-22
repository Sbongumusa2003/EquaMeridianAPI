using EquaMeridian.DTOs.User;

public interface IUserRepository
{
    Task<(IEnumerable<UserDto> Users, int TotalCount)> GetAllAsync(
        string? search, string? role, string? status, int page, int pageSize);
    Task<User?> GetByIdAsync(int userId);
    Task UpdateStatusAsync(int userId, string newStatus);
    Task<(bool Success, string Message)> UpdateProfileAsync(int userId, UpdateProfileDto dto);
    Task<(bool Success, string Message)> DeactivateAsync(int userId);
    Task<bool> HasOpenBookingProcessAsync(int userId, string role);
    Task<(bool Success, string Message, User? User)> CreateInternalUserAsync(
        string fullName, string email, string passwordHash, string role);
    Task<(bool Success, string Message)> UpdateRoleAsync(int userId, string newRole);
}