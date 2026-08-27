using IdbInvest.Offboarding.Databricks.Facade.Core.Exceptions;

namespace IdbInvest.Offboarding.Databricks.Facade.Application.Query;

internal static class FilterParser
{
    internal sealed record ParsedFilter(string Field, string Operator, string Value);

    public static IReadOnlyList<ParsedFilter> Parse(IReadOnlyList<string> filters)
    {
        var result = new List<ParsedFilter>();
        foreach (var raw in filters.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            foreach (var item in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = item.Split(':', 3, StringSplitOptions.TrimEntries);
                if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidQueryException($"Invalid filter '{item}'. Expected field:operator:value.");
                result.Add(new ParsedFilter(parts[0], parts[1].ToLowerInvariant(), parts[2]));
            }
        }
        return result;
    }
}
