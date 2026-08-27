using System.Text;
using System.Text.RegularExpressions;
using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;
using IdbInvest.Offboarding.Databricks.Facade.Core.Models;

namespace IdbInvest.Offboarding.Databricks.Facade.Application.Query;

public sealed class QueryBuilder : IQueryBuilder
{
    private static readonly Regex SafeIdentifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
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
        var parsed = FilterParser.Parse(request.Filters);
        var clauses = new List<string>();
        var values = new List<SqlParameterValue>();
        var i = 0;
        foreach (var filter in parsed)
        {
            if (!definition.Fields.TryGetValue(filter.Field, out var field) || !field.Filterable)
                throw new InvalidQueryException($"Field '{filter.Field}' is not filterable.");
            if (!Operators.TryGetValue(filter.Operator, out var op))
                throw new InvalidQueryException($"Operator '{filter.Operator}' is not allowed.");
            var name = $"p{i++}";
            var value = filter.Operator.Equals("contains", StringComparison.OrdinalIgnoreCase) ? $"%{filter.Value}%" : filter.Value;
            clauses.Add($"{Quote(field.Column)} {op} :{name}");
            values.Add(new SqlParameterValue(name, value, field.Type));
        }
        parameters = values;
        return clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses);
    }

    private static string BuildOrderBy(ResourceDefinition definition, string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return string.Empty;
        var parts = new List<string>();
        foreach (var token in sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var desc = token.StartsWith('-');
            var publicField = desc ? token[1..] : token;
            if (!definition.Fields.TryGetValue(publicField, out var field) || !field.Sortable)
                throw new InvalidQueryException($"Field '{publicField}' is not sortable.");
            parts.Add($"{Quote(field.Column)} {(desc ? "DESC" : "ASC")}");
        }
        return parts.Count == 0 ? string.Empty : " ORDER BY " + string.Join(", ", parts);
    }

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
