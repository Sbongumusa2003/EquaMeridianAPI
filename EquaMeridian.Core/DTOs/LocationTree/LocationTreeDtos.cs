namespace EquaMeridian.DTOs.LocationTree
{
    public class SuburbDto
    {
        public int SuburbID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CityDto
    {
        public int CityID { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<SuburbDto> Suburbs { get; set; } = new();
    }

    public class ProvinceDto
    {
        public int ProvinceID { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<CityDto> Cities { get; set; } = new();
    }

    public class UpsertNodeDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class CreateCityDto
    {
        public int ProvinceID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CreateSuburbDto
    {
        public int CityID { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
