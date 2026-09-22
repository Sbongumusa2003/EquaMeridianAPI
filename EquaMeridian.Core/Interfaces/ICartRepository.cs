using EquaMeridian.DTOs.Cart;

public interface ICartRepository
{
    Task<CartDto> GetCartAsync(int contractorId, string? promoCode = null);
    Task<(bool Success, string? Error)> AddItemAsync(int contractorId, AddCartItemDto dto);
    Task<(bool Success, string? Error)> UpdateItemAsync(int contractorId, int cartItemId, UpdateCartItemDto dto);
    Task<bool> RemoveItemAsync(int contractorId, int cartItemId);
    Task<CheckoutResult> CheckoutAsync(int contractorId, string? promoCode = null);
}
