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

namespace Infrastructure.Contexts
{
    internal class ModulumContextFactory : IDesignTimeDbContextFactory<ModulumContext>
    {
        public ModulumContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ModulumContext>();

            var connectionString = Environment.GetEnvironmentVariable("MODULUM_CONNECTION_STRING");
            optionsBuilder.UseSqlServer(connectionString,
                b => b.MigrationsAssembly(typeof(ModulumContext).Assembly.GetName().Name));

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
