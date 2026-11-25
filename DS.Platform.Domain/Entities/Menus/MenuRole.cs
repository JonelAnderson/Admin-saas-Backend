using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace DS.Platform.Domain.Entities.Menus
{
    public class MenuRole : AuditableEntity<int>
    {
        public int MenuId { get; init; }
        public string RoleId { get; init; } = null!;
        public Menu Menu { get; init; } = null!;
        public AdmRole Role { get; init; } = null!;
    }
}
