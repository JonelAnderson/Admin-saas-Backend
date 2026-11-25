using System;
using DS.Platform.Domain.Contracts;
using Microsoft.AspNetCore.Identity;

namespace DS.Platform.Domain.Entities.Identity
{
    public class AdmRoleClaim : IdentityRoleClaim<string>, IAuditableEntity<int>
    {
        public AdmRoleClaim() : base()
        {
        }

        public AdmRoleClaim(string roleClaimDescription = null!, string roleClaimGroup = null!) : base()
        {
            Description = roleClaimDescription;
            Group = roleClaimGroup;
        }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string? Group { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }
        public virtual AdmRole? Role { get; set; }
       
    }
}