using SensSera.Domain.Common;

namespace SensSera.Domain.Entities;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<User> Users { get; set; } = [];
    public ICollection<Greenhouse> Greenhouses { get; set; } = [];
    public string Slug { get; set; } = string.Empty;
}