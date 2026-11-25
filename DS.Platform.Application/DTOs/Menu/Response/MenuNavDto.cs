using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Menu.Response
{
    public class MenuNavDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Translate { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Classes { get; set; } = string.Empty;
        public bool ExactMatch { get; set; }
        public bool External { get; set; }
        public bool Target { get; set; }
        public bool Breadcrumbs { get; set; }
        public string BadgeTitle { get; set; } = string.Empty;
        public string BadgeType { get; set; } = string.Empty;
        public bool Disabled { get; set; }
        public List<MenuNavDto> Children { get; set; } = new List<MenuNavDto>();
        public int Order { get; set; }
    }
}
