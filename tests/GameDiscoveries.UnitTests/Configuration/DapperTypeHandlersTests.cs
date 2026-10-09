using System.Data;
using Dapper;
using FluentAssertions;
using GameDiscoveries.Infrastructure.PostgreSQL;
using GameDiscoveries.Modules.Xp.Models;
using Npgsql;

namespace GameDiscoveries.UnitTests.Configuration;

public sealed class DapperTypeHandlersTests
{
    public DapperTypeHandlersTests() => DapperTypeHandlers.Register();

    [Fact]
    public void Timestamptz_rows_materialize_into_records_with_DateTimeOffset()
    {
        var createdAt = new DateTime(2026, 10, 9, 13, 30, 0, DateTimeKind.Utc);
        var table = new DataTable();
        table.Columns.Add("TransactionId", typeof(Guid));
        table.Columns.Add("RuleCode", typeof(string));
        table.Columns.Add("EventType", typeof(string));
        table.Columns.Add("ReferenceType", typeof(string));
        table.Columns.Add("ReferenceId", typeof(string));
        table.Columns.Add("XpAmount", typeof(int));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Rows.Add(Guid.NewGuid(), "RULE", "EVENT", "REF", "1", 10, "desc", createdAt);

        using var reader = table.CreateDataReader();
        var rows = reader.Parse<XpTransactionDto>().ToList();

        rows.Should().ContainSingle()
            .Which.CreatedAt.Should().Be(new DateTimeOffset(createdAt));
    }

    [Fact]
    public void Unspecified_kind_is_treated_as_utc()
    {
        var table = new DataTable();
        table.Columns.Add("At", typeof(DateTime));
        table.Columns.Add("Missing", typeof(DateTime));
        table.Rows.Add(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified), DBNull.Value);

        using var reader = table.CreateDataReader();
        var row = reader.Parse<TimestampRow>().Single();

        row.At.Should().Be(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        row.Missing.Should().BeNull();
    }

    [Fact]
    public void Parameters_stay_typed_as_DateTimeOffset_including_nulls()
    {
        var handler = new DateTimeOffsetTypeHandler();
        var at = new NpgsqlParameter();
        var missing = new NpgsqlParameter();

        handler.SetValue(at, new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.FromHours(7)));
        handler.SetValue(missing, DBNull.Value);

        at.DbType.Should().Be(DbType.DateTimeOffset);
        ((DateTimeOffset)at.Value!).Offset.Should().Be(TimeSpan.Zero);
        ((DateTimeOffset)at.Value!).Should().Be(new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.Zero));
        missing.DbType.Should().Be(DbType.DateTimeOffset);
        missing.Value.Should().Be(DBNull.Value);
    }

    public sealed class TimestampRow
    {
        public DateTimeOffset At { get; set; }
        public DateTimeOffset? Missing { get; set; }
    }
}
