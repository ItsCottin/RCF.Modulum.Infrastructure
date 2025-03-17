using RCF.Modulum.Domain.Contracts;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace modulum.Infrastructure.Models.Identity
{
    public class ModulumUser : IdentityUser<string>
    {
        public string? NomeCompleto { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }

        public ModulumUser()
        {
            
        }
    }
}