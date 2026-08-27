namespace IdbInvest.Offboarding.Databricks.Facade.Core.Models;

public sealed record SqlParameterValue(string Name, string Value, string Type);

public sealed record QueryPlan(
    string Sql,
    IReadOnlyList<SqlParameterValue> Parameters,
    IReadOnlyList<string> PublicFields,
    int Page,
    int PageSize);

public sealed record CountQueryPlan(
    string Sql,
    IReadOnlyList<SqlParameterValue> Parameters);

public sealed record DatabricksQueryResult(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    string? StatementId = null);
