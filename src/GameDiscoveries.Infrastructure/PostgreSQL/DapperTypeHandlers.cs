using System.Data;
using Dapper;

namespace GameDiscoveries.Infrastructure.PostgreSQL;

public static class DapperTypeHandlers
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        SqlMapper.AddTypeHandler(typeof(DateTimeOffset), new DateTimeOffsetTypeHandler());
    }
}

/// <summary>
/// Npgsql reads timestamptz as UTC <see cref="DateTime"/>, which Dapper cannot bind to
/// <see cref="DateTimeOffset"/> constructor parameters (positional records) on its own.
/// Parameters keep Dapper's default <see cref="DbType.DateTimeOffset"/>, including nulls.
/// </summary>
public sealed class DateTimeOffsetTypeHandler : SqlMapper.ITypeHandler
{
    public void SetValue(IDbDataParameter parameter, object value)
    {
        parameter.DbType = DbType.DateTimeOffset;
        parameter.Value = value is DateTimeOffset dto ? dto.ToUniversalTime() : value;
    }

    public object Parse(Type destinationType, object value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime { Kind: DateTimeKind.Unspecified } dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
        DateTime dt => new DateTimeOffset(dt.ToUniversalTime()),
        _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to {nameof(DateTimeOffset)}.")
    };
}
