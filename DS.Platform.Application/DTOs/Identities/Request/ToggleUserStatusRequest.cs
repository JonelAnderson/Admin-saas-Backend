namespace DS.Platform.Application.DTOs.Identities.Request
{
    public class ToggleUserStatusRequest
    {
        public bool ActivateUser { get; set; }
        public string? UserId { get; set; }
    }
}