using SensSera.Application.DTOs;

namespace SensSera.Application.Interfaces;

public interface INodeService
{
    Task<List<NodeResponse>> GetAllByGreenhouseAsync(Guid greenhouseId, CancellationToken cancellationToken = default);
    Task<NodeResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NodeWithTokenResponse> CreateAsync(NodeRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NodeWithTokenResponse> RotateTokenAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NodeChannelResponse> AddChannelAsync(Guid nodeId, NodeChannelRequest request, CancellationToken cancellationToken = default);
}
