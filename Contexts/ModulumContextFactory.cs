using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using modulum.Application.Interfaces.Services;
using modulum.Infrastructure.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using modulum.Shared.Constants.Application;

namespace Infrastructure.Contexts
{
    public class ModulumContextFactory : IDesignTimeDbContextFactory<ModulumContext>
    {
        public ModulumContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ModulumContext>();

            var connectionString = Environment.GetEnvironmentVariable(ApplicationConstants.Variable.ModulumConnectionString);
            optionsBuilder.UseSqlServer(connectionString, b => b.MigrationsAssembly("Modulum.Api"));

            // Serviços falsos para injeção de dependência em tempo de design
            var currentUserService = new CurrentUserServiceFake();
            var dateTimeService = new DateTimeServiceFake();

            return new ModulumContext(optionsBuilder.Options, currentUserService, dateTimeService);
        }
    }

    // Serviços falsos para ICurrentUserService e IDateTimeService (implemente conforme necessário)
    public class CurrentUserServiceFake : ICurrentUserService
    {
        public string UserId => "DesignTimeUser";
    }

    public class DateTimeServiceFake : IDateTimeService
    {
        public DateTime NowUtc => DateTime.UtcNow;
    }
}
