using System.Globalization;
using System.Text.RegularExpressions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Infrastructure.DataMotion;

internal static class SqlParameterRenderer
{
    private static readonly Regex ParameterToken = new(
        @":(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    public static string Render(string sql, IReadOnlyList<SqlParameterValue> parameters)
    {
        if (parameters.Count == 0) return sql;

        var values = parameters.ToDictionary(
            x => x.Name,
            ToSqlLiteral,
            StringComparer.OrdinalIgnoreCase);

        var replaced = ParameterToken.Replace(sql, match =>
        {
            var name = match.Groups["name"].Value;
            if (!values.TryGetValue(name, out var value))
                throw new InvalidQueryException($"SQL parameter '{name}' does not have a value.");

            return value;
        });

        var unresolved = ParameterToken.Match(replaced);
        if (unresolved.Success)
            throw new InvalidQueryException($"SQL parameter '{unresolved.Groups["name"].Value}' does not have a value.");

        return replaced;
    }

    private static string ToSqlLiteral(SqlParameterValue parameter)
    {
        var type = parameter.Type.Trim().ToUpperInvariant();
        return type switch
        {
            "STRING" or "VARCHAR" or "CHAR" => Quote(parameter.Value),
            "DATE" => $"DATE {Quote(ValidateDate(parameter.Value))}",
            "TIMESTAMP" or "DATETIME" => $"TIMESTAMP {Quote(ValidateTimestamp(parameter.Value))}",
            "BOOLEAN" or "BOOL" => ParseBoolean(parameter.Value),
            "BYTE" or "SHORT" or "INT" or "INTEGER" or "LONG" or "BIGINT" => ParseInteger(parameter.Value),
            "FLOAT" or "DOUBLE" or "DECIMAL" or "NUMERIC" => ParseDecimal(parameter.Value),
            _ => throw new InvalidQueryException($"Unsupported SQL parameter type '{parameter.Type}'.")
        };
    }

    private static string Quote(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string ValidateDate(string value)
    {
        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
            throw new InvalidQueryException("DATE filter value must use yyyy-MM-dd.");

        return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string ValidateTimestamp(string value)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed))
            throw new InvalidQueryException("TIMESTAMP filter value is invalid.");

        return parsed.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static string ParseBoolean(string value)
    {
        if (!bool.TryParse(value, out var parsed))
            throw new InvalidQueryException("BOOLEAN filter value must be true or false.");

        return parsed ? "TRUE" : "FALSE";
    }

    private static string ParseInteger(string value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidQueryException("Integer filter value is invalid.");

        return parsed.ToString(CultureInfo.InvariantCulture);
    }

    private static string ParseDecimal(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidQueryException("Decimal filter value is invalid.");

        return parsed.ToString(CultureInfo.InvariantCulture);
    }
}
