namespace DS.Platform.Application.DTOs.Identities.Response
{
    public class RolePermissionResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Slug { get; set; } = string.Empty;
        public int MenuId { get; set; }
        public string? MenuTitle { get; set; }
        public bool Selected { get; set; }
    }
}
