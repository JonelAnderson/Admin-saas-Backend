using DS.Platform.Domain.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace DS.Platform.Domain.Entities.Menus
{
    public class Menu : AuditableEntity<int>
    {
        public string? Title { get; set; } 
        public string Type { get; set; } = "item"; // item, collapse, group
        public string? Translate { get; set; }
        public string? Icon { get; set; }
        public bool Hidden { get; set; } = false;
        public string? Url { get; set; } 
        public string? Classes { get; set; }
        public bool ExactMatch { get; set; } = false;
        public bool External { get; set; } = false;
        public bool Target { get; set; } = false;
        public bool Breadcrumbs { get; set; } = false;
        public string? BadgeTitle { get; set; }
        public string? BadgeType { get; set; }
        public bool Disabled { get; set; } = false;
        //public List<string>? Permissions { get; set; }
        public int Order { get; set; }
        public int? MenuParentId { get; set; }

        public virtual Menu? MenuParent { get; set; }

        public virtual ICollection<Menu>? MenuChildren { get; set; } 
    }
}
