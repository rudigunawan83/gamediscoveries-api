using GameDiscoveries.Modules.Streaks.Options;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Streaks.Services;

public interface IStreakTimeService
{
    TimeZoneInfo TimeZone { get; }

    DateOnly GetLocalDate(DateTimeOffset utcMoment);

    DateOnly TodayLocal();
}

public sealed class StreakTimeService(IOptions<StreakOptions> options) : IStreakTimeService
{
    public TimeZoneInfo TimeZone
    {
        get
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
        }
    }

    public DateOnly GetLocalDate(DateTimeOffset utcMoment)
    {
        var local = TimeZoneInfo.ConvertTime(utcMoment, TimeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public DateOnly TodayLocal() => GetLocalDate(DateTimeOffset.UtcNow);
}

/// <summary>Pure helpers for unit tests.</summary>
public static class StreakDateCalculator
{
    public static DateOnly ToLocalDate(DateTimeOffset utc, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(utc, tz);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static int DayGap(DateOnly earlier, DateOnly later) => later.DayNumber - earlier.DayNumber;
}
