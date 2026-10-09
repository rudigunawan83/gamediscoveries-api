using System.Data;
using Dapper;
using FluentAssertions;
using GameDiscoveries.Infrastructure.PostgreSQL;
using GameDiscoveries.Modules.Community.Domain;

namespace GameDiscoveries.UnitTests.Community;

public sealed class CommunityDtoMappingTests
{
    public CommunityDtoMappingTests() => DapperTypeHandlers.Register();

    [Fact]
    public void Privacy_settings_materialize_with_null_bio()
    {
        var table = new DataTable();
        table.Columns.Add("ShowFavorites", typeof(bool));
        table.Columns.Add("ShowHistory", typeof(bool));
        table.Columns.Add("ShowAchievements", typeof(bool));
        table.Columns.Add("ShowActivity", typeof(bool));
        table.Columns.Add("ShowOnLeaderboards", typeof(bool));
        table.Columns.Add("Bio", typeof(string));
        table.Rows.Add(true, false, true, false, true, DBNull.Value);

        using var reader = table.CreateDataReader();
        var settings = reader.Parse<PrivacySettingsDto>().Single();

        settings.Should().Be(new PrivacySettingsDto(true, false, true, false, true, null));
    }
}
