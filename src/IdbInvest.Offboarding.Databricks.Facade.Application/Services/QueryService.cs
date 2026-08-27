using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;
using IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;

namespace IdbInvest.Offboarding.Databricks.Facade.Application.Services;

public sealed class QueryService(
    IResourceRegistry registry,
    IQueryBuilder queryBuilder,
    IDatabricksRepository repository) : IQueryService
{
    public async Task<QueryResponseDto> QueryAsync(string resource, QueryRequestDto request, string correlationId, CancellationToken cancellationToken)
    {
        var definition = registry.GetRequired(resource);
        var plan = queryBuilder.Build(definition, request);
        var result = await repository.QueryAsync(plan, cancellationToken);
        var hasMore = result.Rows.Count > request.PageSize;
        var rows = hasMore ? result.Rows.Take(request.PageSize).ToArray() : result.Rows;
        long? total = null;
        if (request.IncludeTotal)
            total = await repository.CountAsync(queryBuilder.BuildCount(definition, request), cancellationToken);
        return new QueryResponseDto(rows, new QueryMetadataDto(resource, request.Page, request.PageSize, rows.Count, total, hasMore, correlationId));
    }

    public ResourceMetadataDto GetMetadata(string resource)
    {
        var definition = registry.GetRequired(resource);
        var fields = definition.Fields.Select(x => new ResourceFieldDto(x.Key, x.Value.Type, x.Value.Filterable, x.Value.Sortable, x.Value.Selectable)).ToArray();
        return new ResourceMetadataDto(definition.Name, fields, definition.MaxPageSize);
    }
}
