namespace IdbInvest.Offboarding.Databricks.Facade.Core.DTO;

public sealed record QueryRequestDto(
    string? Fields,
    IReadOnlyList<string> Filters,
    string? Sort,
    int Page = 1,
    int PageSize = 100,
    bool IncludeTotal = false);
