namespace FF14RisingstoneCheckIn.Utils;

public static class DateTimeHelper
{
    public static string GetCurrentMonth()
    {
        var chinaTime = GetChinaTime();
        return chinaTime.ToString("yyyy-MM");
    }

    public static DateTime GetChinaTime()
    {
        var utcNow = DateTime.UtcNow;
        var chinaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        return TimeZoneInfo.ConvertTimeFromUtc(utcNow, chinaTimeZone);
    }

    public static bool ShouldSignInToday(DateTime? lastSignInTime)
    {
        if (!lastSignInTime.HasValue)
            return true;

        var chinaTime = GetChinaTime();
        var today = chinaTime.Date;
        return lastSignInTime.Value.Date < today;
    }
}
