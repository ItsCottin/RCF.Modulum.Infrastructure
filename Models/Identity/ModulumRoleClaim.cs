using System;
using RCF.Modulum.Domain.Contracts;
using Microsoft.AspNetCore.Identity;

namespace modulum.Infrastructure.Models.Identity
{
    public class ModulumRoleClaim : IdentityRoleClaim<string>
    {
        public string Description { get; set; }
        public string Group { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime? LastModifiedOn { get; set; }
        public virtual ModulumRole Role { get; set; }

        public ModulumRoleClaim() : base()
        {
        }

        public ModulumRoleClaim(string roleClaimDescription = null, string roleClaimGroup = null) : base()
        {
            Description = roleClaimDescription;
            Group = roleClaimGroup;
        }
    }
}