using EquaMeridian.DTOs.Fees;

public interface IDiscountTierRepository
{
    Task<IEnumerable<DiscountTierDto>> GetAllAsync();
    Task<DiscountTierDto> CreateAsync(UpsertDiscountTierDto dto, int adminId);
    Task<DiscountTierDto?> UpdateAsync(int id, UpsertDiscountTierDto dto, int adminId);
    Task<bool> DeleteAsync(int id);
}
