using DS.Platform.Domain.Contracts;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace DS.Platform.Domain.Entities.Identity
{
    public class AdmUser : IdentityUser<string>, IAuditableEntity<string>
    {
        public AdmUser()
        {
            Subordinates = new HashSet<AdmUser>();
            FirstName = string.Empty;
            LastName = string.Empty;
            CreatedBy = string.Empty;
        }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }

        [Column(TypeName = "text")]
        public string? ProfilePictureDataUrl { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public DateTime? DeletedOn { get; set; }
        public string? DeletedBy { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }

        public virtual AdmUser? User { get; set; }
        public virtual ICollection<AdmUser> Subordinates { get; set; } 

    }
}