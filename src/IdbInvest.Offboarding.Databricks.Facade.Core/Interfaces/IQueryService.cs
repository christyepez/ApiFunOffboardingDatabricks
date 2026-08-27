using IdbInvest.Offboarding.Databricks.Facade.Core.DTO;

namespace IdbInvest.Offboarding.Databricks.Facade.Core.Interfaces;

public interface IQueryService
{
    Task<QueryResponseDto> QueryAsync(string resource, QueryRequestDto request, string correlationId, CancellationToken cancellationToken);
    ResourceMetadataDto GetMetadata(string resource);
}
