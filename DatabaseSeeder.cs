using modulum.Application.Interfaces.Services;
using modulum.Infrastructure.Contexts;
using modulum.Infrastructure.Models.Identity;
using modulum.Shared.Constants.Permission;
using modulum.Shared.Constants.Role;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using modulum.Application.Responses.Dataseeder;
using Azure.Storage.Blobs;
using modulum.Shared.Constants.Application;
using Newtonsoft.Json;

namespace modulum.Infrastructure
{
    // Essa class tem como objetivo abastecer o banco de dados na inicialização
    public class DatabaseSeeder : IDatabaseSeeder
    {
        private readonly ModulumContext _db;
        private readonly UserManager<ModulumUser> _userManager;
        private readonly RoleManager<ModulumRole> _roleManager;
        private DatabaseSeederResponse retorno;
    
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
            GetSeedData();
            AddUsuarios();
            _db.SaveChanges();
        }

        public void GetSeedData()
        {
            var blobClient = new BlobClient(new Uri(Environment.GetEnvironmentVariable(ApplicationConstants.Variable.BlobDataSeeder)));
            var response = blobClient.DownloadContent();
            var contentString = response.Value.Content.ToString();
            using (var stringReader = new StringReader(contentString))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                var serializer = new JsonSerializer();
                retorno = serializer.Deserialize<DatabaseSeederResponse>(jsonReader);
            }
        }

        private void AddUsuarios()
        {
            Task.Run(async () =>
            {
                foreach (var item in retorno.Usuarios)
                {
                    var role = new ModulumRole { Name = item.Role, NormalizedName = item.Role.ToUpper() };
                    var adminRoleInDb = await _roleManager.FindByNameAsync(item.Role);
                    if (adminRoleInDb == null)
                    {
                        await _roleManager.CreateAsync(role);
                    }

                    var superUser = new ModulumUser
                    {
                        NomeCompleto = item.NomeCompleto,
                        Email = item.Email,
                        UserName = item.UserName,
                        NormalizedEmail = item.Email.ToUpper(),
                        NormalizedUserName = item.UserName.ToUpper(),
                        EmailConfirmed = true,
                        IsCadastroFinalizado = true,
                    };
                    var superUserInDb = await _userManager.FindByEmailAsync(superUser.Email);
                    if (superUserInDb == null)
                    {
                        await _userManager.CreateAsync(superUser, item.Password);
                        var roleInDb = await _roleManager.FindByNameAsync(item.Role.ToUpper());
                        if (roleInDb != null)
                        {
                            var result = await _userManager.AddToRoleAsync(superUser, item.Role);
                        }
                    }
                }
            }).GetAwaiter().GetResult();
        }
    }
}