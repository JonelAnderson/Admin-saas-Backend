using DS.Platform.Domain.Contracts;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace DS.Platform.Domain.Entities.Identity
{
    public class AdmRole : IdentityRole, IAuditableEntity<string>
    {
        public AdmRole() : base()
        {
            RoleClaims = new HashSet<AdmRoleClaim>();
            Description = string.Empty;
            CreatedBy = string.Empty;
        }

        public AdmRole(string roleName, string roleDescription = null!) : base(roleName)
        {
            RoleClaims = new HashSet<AdmRoleClaim>();
            Description = roleDescription ?? string.Empty;
            CreatedBy = string.Empty;
        }
        public string Description { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }
        public virtual ICollection<AdmRoleClaim> RoleClaims { get; set; }

       

        public virtual AdmRole? Parent { get; set; }
        public virtual ICollection<AdmRole>? Childrens { get; set; }
    }
}