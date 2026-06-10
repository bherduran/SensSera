using SensSera.Domain.Common;
using SensSera.Domain.Enums;

namespace SensSera.Domain.Entities;

public class User : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }
    public Organization Organization { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [] ;
}