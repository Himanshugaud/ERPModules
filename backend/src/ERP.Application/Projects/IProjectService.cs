using ERP.Shared.Pagination;

namespace ERP.Application.Projects;

public interface IProjectService
{
    Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<PagedResult<ProjectResponse>> ListAsync(Abstractions.ProjectFilter filter, CancellationToken ct = default);
    Task<ProjectResponse> GetAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectResponse> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid projectId, CancellationToken ct = default);
}
