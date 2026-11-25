using System;
using System.Collections.Generic;

namespace DS.Platform.Application.DTOs.Menu.Response
{
    public class MenuResponseDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string Type { get; set; } = "item";
        public string? Translate { get; set; }
        public string? Icon { get; set; }
        public bool Hidden { get; set; }
        public string? Url { get; set; }
        public string? Classes { get; set; }
        public bool ExactMatch { get; set; }
        public bool External { get; set; }
        public bool Target { get; set; }
        public bool Breadcrumbs { get; set; }
        public string? BadgeTitle { get; set; }
        public string? BadgeType { get; set; }
        public bool Disabled { get; set; }
        public int Order { get; set; }
        public int? MenuParentId { get; set; }
        public bool IsActive { get; set; }
        public string? StateName { get; set; }
    }
}
