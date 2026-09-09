using System.Globalization;
using System.Text.RegularExpressions;
using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Application.Query;

public sealed class QueryBuilder : IQueryBuilder
{
    private static readonly Regex SafeIdentifier = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    private static readonly IReadOnlyDictionary<string, string> Operators = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["eq"] = "=", ["ne"] = "<>", ["gt"] = ">", ["gte"] = ">=", ["lt"] = "<", ["lte"] = "<=", ["contains"] = "LIKE"
    };

    public QueryPlan Build(ResourceDefinition definition, QueryRequestDto request)
    {
        ValidateRequest(definition, request);
        var publicFields = ResolveFields(definition, request.Fields);
        var where = BuildWhere(definition, request, out var parameters);
        var orderBy = BuildOrderBy(definition, request.Sort);
        var offset = checked((request.Page - 1) * request.PageSize);
        var select = string.Join(", ", publicFields.Select(f => $"{Quote(definition.Fields[f].Column)} AS {Quote(f)}"));
        var sql = $"SELECT {select} FROM {QuoteSource(definition.Source)}{where}{orderBy} LIMIT {request.PageSize + 1} OFFSET {offset}";
        return new QueryPlan(sql, parameters, publicFields, request.Page, request.PageSize);
    }

    public CountQueryPlan BuildCount(ResourceDefinition definition, QueryRequestDto request)
    {
        ValidateRequest(definition, request);
        var where = BuildWhere(definition, request, out var parameters);
        return new CountQueryPlan($"SELECT COUNT(1) AS total_count FROM {QuoteSource(definition.Source)}{where}", parameters);
    }

    private static void ValidateRequest(ResourceDefinition definition, QueryRequestDto request)
    {
        if (request.Page < 1) throw new InvalidQueryException("page must be >= 1.");
        if (request.PageSize < 1 || request.PageSize > definition.MaxPageSize)
            throw new InvalidQueryException($"pageSize must be between 1 and {definition.MaxPageSize}.");
    }

    private static IReadOnlyList<string> ResolveFields(ResourceDefinition definition, string? fields)
    {
        var requested = string.IsNullOrWhiteSpace(fields)
            ? definition.DefaultFields
            : fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (requested.Count == 0) throw new InvalidQueryException("At least one field must be selected.");
        foreach (var field in requested)
        {
            if (!definition.Fields.TryGetValue(field, out var def) || !def.Selectable)
                throw new InvalidQueryException($"Field '{field}' is not selectable for resource '{definition.Name}'.");
        }
        return requested.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string BuildWhere(ResourceDefinition definition, QueryRequestDto request, out IReadOnlyList<SqlParameterValue> parameters)
    {
        var clauses = new List<string>();
        var values = new List<SqlParameterValue>();
        var index = 0;

        foreach (var required in definition.RequiredFilters)
            AddFilter(definition, required.Field, required.Operator, required.Value, clauses, values, ref index);

        AddLookbackFilter(definition, clauses, values, ref index);

        foreach (var filter in FilterParser.Parse(request.Filters))
            AddFilter(definition, filter.Field, filter.Operator, filter.Value, clauses, values, ref index);

        parameters = values;
        return clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses);
    }

    private static void AddLookbackFilter(
        ResourceDefinition definition,
        ICollection<string> clauses,
        ICollection<SqlParameterValue> values,
        ref int index)
    {
        if (definition.Lookback is null) return;

        var cutoff = DateTime.UtcNow.Date
            .AddDays(-definition.Lookback.Days)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        AddFilter(definition, definition.Lookback.Field, "gte", cutoff, clauses, values, ref index);
    }

    private static void AddFilter(
        ResourceDefinition definition,
        string fieldName,
        string operatorName,
        string rawValue,
        ICollection<string> clauses,
        ICollection<SqlParameterValue> values,
        ref int index)
    {
        var field = ResolveFilterField(definition, fieldName);
        var sqlOperator = ResolveOperator(operatorName);
        var parameterName = $"p{index++}";
        var value = operatorName.Equals("contains", StringComparison.OrdinalIgnoreCase) ? $"%{rawValue}%" : rawValue;

        clauses.Add($"{Quote(field.Column)} {sqlOperator} :{parameterName}");
        values.Add(new SqlParameterValue(parameterName, value, field.Type));
    }

    private static FieldDefinition ResolveFilterField(ResourceDefinition definition, string fieldName)
    {
        if (!definition.Fields.TryGetValue(fieldName, out var field) || !field.Filterable)
            throw new InvalidQueryException($"Field '{fieldName}' is not filterable.");
        return field;
    }

    private static string ResolveOperator(string operatorName)
    {
        if (!Operators.TryGetValue(operatorName, out var sqlOperator))
            throw new InvalidQueryException($"Operator '{operatorName}' is not allowed.");
        return sqlOperator;
    }

    private static string BuildOrderBy(ResourceDefinition definition, string? requestedSort)
    {
        var tokens = ResolveSortTokens(definition, requestedSort);
        if (tokens.Count == 0) return string.Empty;

        var parts = tokens.Select(token => BuildSortPart(definition, token));
        return " ORDER BY " + string.Join(", ", parts);
    }

    private static IReadOnlyList<string> ResolveSortTokens(ResourceDefinition definition, string? requestedSort)
    {
        var tokens = ParseSortTokens(requestedSort);
        foreach (var token in ParseSortTokens(definition.DefaultSort))
        {
            if (!ContainsSortField(tokens, token)) tokens.Add(token);
        }
        return tokens;
    }

    private static List<string> ParseSortTokens(string? sort) => string.IsNullOrWhiteSpace(sort)
        ? []
        : sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static bool ContainsSortField(IEnumerable<string> tokens, string candidateToken)
    {
        var candidate = GetSortFieldName(candidateToken);
        return tokens.Any(existing => string.Equals(GetSortFieldName(existing), candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildSortPart(ResourceDefinition definition, string token)
    {
        var publicField = GetSortFieldName(token);
        if (!definition.Fields.TryGetValue(publicField, out var field) || !field.Sortable)
            throw new InvalidQueryException($"Field '{publicField}' is not sortable.");

        var direction = token.StartsWith('-') ? "DESC" : "ASC";
        return $"{Quote(field.Column)} {direction}";
    }

    private static string GetSortFieldName(string token) => token.StartsWith('-') ? token[1..] : token;

    private static string Quote(string identifier)
    {
        if (!SafeIdentifier.IsMatch(identifier)) throw new InvalidOperationException($"Unsafe configured identifier '{identifier}'.");
        return $"`{identifier}`";
    }

    private static string QuoteSource(string source)
    {
        var parts = source.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3 || parts.Any(x => !SafeIdentifier.IsMatch(x)))
            throw new InvalidOperationException($"Unsafe configured source '{source}'.");
        return string.Join('.', parts.Select(Quote));
    }
}
