/// <summary>
/// Plant-hire duration is inclusive of both start and end calendar dates
/// (e.g. Mon–Wed = 3 hire days, not 2).
/// </summary>
public static class HireDuration
{
    public static int InclusiveDays(DateTime start, DateTime end)
    {
        var days = (end.Date - start.Date).Days + 1;
        return Math.Max(1, days);
    }
}
