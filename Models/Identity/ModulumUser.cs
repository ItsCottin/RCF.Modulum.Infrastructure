using modulum.Domain.Contracts;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using modulum.Domain.Entities.Account;
using modulum.Domain.Entities.DynamicEntity;

namespace modulum.Infrastructure.Models.Identity
{
    public class ModulumUser : IdentityUser<int>
    {
        public string? NomeCompleto { get; set; }
        public string? RefreshToken { get; set; }
        public string? Cpf { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }

        public bool? IsCadastroFinalizado { get; set; }

        public List<TwoFactor> TwoFactors { get; set; } = new();

        public List<Table> Tables { get; set; } = new();

        public ModulumUser()
        {
            
        }
    }
}