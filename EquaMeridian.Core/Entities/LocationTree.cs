// A three-level tree (Province -> City -> Suburb) stored relationally in the database, per the
// brief's example. Displayed and fully editable (add/update/delete at any level) from the admin
// frontend — see LocationTreeController.
public class Province
{
    public int ProvinceID { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<City> Cities { get; set; } = new();
}

public class City
{
    public int CityID { get; set; }
    public string Name { get; set; } = string.Empty;

    public int ProvinceID { get; set; }
    public Province? Province { get; set; }

    public List<Suburb> Suburbs { get; set; } = new();
}

public class Suburb
{
    public int SuburbID { get; set; }
    public string Name { get; set; } = string.Empty;

    public int CityID { get; set; }
    public City? City { get; set; }
}
