using modulum.Application.Interfaces.Services;
using modulum.Infrastructure.Contexts;
using modulum.Infrastructure.Models.Identity;
using modulum.Shared.Constants.Permission;
using modulum.Shared.Constants.Role;
using modulum.Shared.Constants.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace modulum.Infrastructure
{
    // Essa class tem como objetivo abastecer o banco de dados na inicialização
    public class DatabaseSeeder : IDatabaseSeeder
    {
        private readonly ModulumContext _db;
        private readonly UserManager<ModulumUser> _userManager;
        private readonly RoleManager<ModulumRole> _roleManager;
    
        public DatabaseSeeder(
            UserManager<ModulumUser> userManager,
            RoleManager<ModulumRole> roleManager,
            ModulumContext db)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }
    
        public void Initialize()
        {
            AddAdministrator();
            _db.SaveChanges();
            AddBasicUser();
            _db.SaveChanges();
        }
    
        private void AddAdministrator()
        {
            Task.Run(async () =>
            {
                //Check if Role Exists
                var adminRole = new ModulumRole { Name = RoleConstants.AdministratorRole, NormalizedName = RoleConstants.AdministratorRole.ToUpper() };
                var adminRoleInDb = await _roleManager.FindByNameAsync(RoleConstants.AdministratorRole);
                if (adminRoleInDb == null)
                {
                    await _roleManager.CreateAsync(adminRole);
                    adminRoleInDb = await _roleManager.FindByNameAsync(RoleConstants.AdministratorRole);
                }
                //Check if User Exists
                var superUser = new ModulumUser
                {
                    NomeCompleto = "Administrador",
                    Email = "admin@admin.com",
                    UserName = "admin@admin.com",
                    NormalizedEmail = "admin@admin.com".ToUpper(),
                    NormalizedUserName = "admin@admin.com".ToUpper(),
                    EmailConfirmed = true,
                    IsCadastroFinalizado = true,
                };
                var superUserInDb = await _userManager.FindByEmailAsync(superUser.Email);
                if (superUserInDb == null)
                {
                    await _userManager.CreateAsync(superUser, UserConstants.DefaultPassword);
                    var result = await _userManager.AddToRoleAsync(superUser, RoleConstants.AdministratorRole);
                }
            }).GetAwaiter().GetResult();
        }
    
        private void AddBasicUser()
        {
            Task.Run(async () =>
            {
                //Check if Role Exists
                var basicRole = new ModulumRole { Name = RoleConstants.BasicRole, NormalizedName = RoleConstants.BasicRole.ToUpper() };
                var basicRoleInDb = await _roleManager.FindByNameAsync(RoleConstants.BasicRole);
                if (basicRoleInDb == null)
                {
                    await _roleManager.CreateAsync(basicRole);
                }
                //Check if User Exists
                var basicUser = new ModulumUser
                {
                    NomeCompleto = "Usuario",
                    Email = "usuario@usuario.com",
                    UserName = "usuario@usuario.com",
                    NormalizedEmail = "usuario@usuario.com".ToUpper(),
                    NormalizedUserName = "usuario@usuario.com".ToUpper(),
                    EmailConfirmed = true,
                    IsCadastroFinalizado = true,
                };
                var basicUserInDb = await _userManager.FindByEmailAsync(basicUser.Email);
                if (basicUserInDb == null)
                {
                    await _userManager.CreateAsync(basicUser, UserConstants.DefaultPassword);
                    await _userManager.AddToRoleAsync(basicUser, RoleConstants.BasicRole);
                }
            }).GetAwaiter().GetResult();
        }
    }
}