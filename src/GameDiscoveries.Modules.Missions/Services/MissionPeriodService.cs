using GameDiscoveries.Modules.Missions.Models;
using GameDiscoveries.Modules.Missions.Options;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Missions.Services;

public interface IMissionPeriodService
{
    TimeZoneInfo TimeZone { get; }

    MissionPeriod GetDailyPeriod(DateTimeOffset utcNow);

    MissionPeriod GetWeeklyPeriod(DateTimeOffset utcNow);

    MissionPeriod GetPeriod(string missionType, DateTimeOffset utcNow);
}

public sealed class MissionPeriodService(IOptions<MissionsOptions> options) : IMissionPeriodService
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
                // Windows may use different IDs; try conversion.
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.Utc;
                }
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
        }
    }

    public MissionPeriod GetDailyPeriod(DateTimeOffset utcNow)
    {
        var local = TimeZoneInfo.ConvertTime(utcNow, TimeZone);
        var startLocal = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1).AddTicks(-1);
        return ToUtcPeriod(startLocal, endLocal);
    }

    public MissionPeriod GetWeeklyPeriod(DateTimeOffset utcNow)
    {
        var local = TimeZoneInfo.ConvertTime(utcNow, TimeZone);
        var daysFromMonday = ((int)local.DayOfWeek + 6) % 7; // Monday=0
        var monday = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified)
            .AddDays(-daysFromMonday);
        var sundayEnd = monday.AddDays(7).AddTicks(-1);
        return ToUtcPeriod(monday, sundayEnd);
    }

    public MissionPeriod GetPeriod(string missionType, DateTimeOffset utcNow) =>
        string.Equals(missionType, Domain.MissionTypes.Weekly, StringComparison.OrdinalIgnoreCase)
            ? GetWeeklyPeriod(utcNow)
            : GetDailyPeriod(utcNow);

    private MissionPeriod ToUtcPeriod(DateTime startLocal, DateTime endLocal)
    {
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, TimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, TimeZone);
        return new MissionPeriod(
            new DateTimeOffset(startUtc, TimeSpan.Zero),
            new DateTimeOffset(endUtc, TimeSpan.Zero));
    }
}

/// <summary>Pure helpers for unit tests.</summary>
public static class MissionPeriodCalculator
{
    public static MissionPeriod Daily(DateTimeOffset utcNow, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        var startLocal = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1).AddTicks(-1);
        return new MissionPeriod(
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, tz), TimeSpan.Zero),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, tz), TimeSpan.Zero));
    }

    public static MissionPeriod Weekly(DateTimeOffset utcNow, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);
        var daysFromMonday = ((int)local.DayOfWeek + 6) % 7;
        var monday = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified)
            .AddDays(-daysFromMonday);
        var sundayEnd = monday.AddDays(7).AddTicks(-1);
        return new MissionPeriod(
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(monday, tz), TimeSpan.Zero),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(sundayEnd, tz), TimeSpan.Zero));
    }
}
