using System;
using System.Collections.Generic;
using RCF.Modulum.Domain.Contracts;
using Microsoft.AspNetCore.Identity;

namespace modulum.Infrastructure.Models.Identity
{
    public class ModulumRole : IdentityRole<string>
    {
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public virtual ICollection<ModulumRoleClaim> RoleClaims { get; set; }

        public ModulumRole() : base()
        {
            RoleClaims = new HashSet<ModulumRoleClaim>();
        }

        public ModulumRole(string roleName, string roleDescription = null) : base(roleName)
        {
            RoleClaims = new HashSet<ModulumRoleClaim>();
            Description = roleDescription;
        }
    }
}