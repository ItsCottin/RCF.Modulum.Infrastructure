using modulum.Application.Interfaces.Services;
using modulum.Infrastructure.Models.Identity;
using RCF.Modulum.Domain.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Reflection.Emit;
using modulum.Domain.Entities.MapCoreEntity;

namespace modulum.Infrastructure.Contexts
{
    public class ModulumContext : IdentityDbContext<ModulumUser, IdentityRole, string>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTimeService _dateTimeService;

        public ModulumContext(DbContextOptions<ModulumContext> options, ICurrentUserService currentUserService, IDateTimeService dateTimeService)
            : base(options)
        {
            _currentUserService = currentUserService;
            _dateTimeService = dateTimeService;
        }

        public DbSet<ModulumUser> ModulumUsers { get; set; }

        public DbSet<Table> Tables { get; set; }
        public DbSet<Field> Fields { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ModulumUser>(entity =>
            {
                entity.ToTable(name: "tbl_user", "dbo");
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.RefreshToken).IsRequired(false);
            });

            // Ajustar essa linha para utilizar regras de acesso personalizada
            builder.Entity<IdentityRole>(entity =>
            {
                entity.ToTable(name: "tbl_role", "dbo");
            });
            builder.Entity<IdentityUserRole<string>>(entity =>
            {
                entity.ToTable("tbl_user_role", "dbo");

                entity.HasOne<ModulumUser>().WithMany().HasForeignKey(ur => ur.UserId).IsRequired().OnDelete(DeleteBehavior.Cascade); // Define a relação com a tabela tbl_user

                entity.HasOne<IdentityRole>().WithMany().HasForeignKey(ur => ur.RoleId).IsRequired().OnDelete(DeleteBehavior.Cascade);

            });

            builder.Entity<IdentityUserClaim<string>>(entity =>
            {
                entity.ToTable("tbl_user_claim", "dbo");
            });

            builder.Entity<IdentityUserLogin<string>>(entity =>
            {
                entity.ToTable("tbl_user_login", "dbo");
                entity.HasKey(x => new { x.LoginProvider, x.ProviderKey }); // Definição da chave composta
            });

            builder.Entity<IdentityUserToken<string>>(entity =>
            {
                entity.ToTable("tbl_user_token", "dbo");
            });

            builder.Entity<IdentityRoleClaim<string>>(entity =>
            {
                entity.ToTable("tbl_role_claim", "dbo");
            });

            builder.Entity<Table>().HasMany(t => t.Fields).WithOne(f => f.Table).HasForeignKey(f => f.TableId);
        }
    }
}
