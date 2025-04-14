using modulum.Application.Interfaces.Services;
using modulum.Infrastructure.Models.Identity;
using modulum.Domain.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Reflection.Emit;
using modulum.Domain.Entities.DynamicEntity;
using modulum.Domain.Entities.Account;
using RCF.Modulum.Infrastructure.Models.Identity;

namespace modulum.Infrastructure.Contexts
{
    public class ModulumContext : IdentityDbContext
        <
            ModulumUser,
            ModulumRole,
            int,
            ModulumUserClaim,
            ModulumUserRole,
            ModulumUserLogin,
            ModulumRoleClaim,
            ModulumUserToken
        >
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
        public DbSet<ModulumRole> ModulumRoles { get; set; }
        public DbSet<ModulumUserRole> ModulumUserRoles { get; set; }
        public DbSet<ModulumUserClaim> ModulumUserClaims { get; set; }
        public DbSet<ModulumRoleClaim> ModulumRoleClaims { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<Field> Fields { get; set; }

        public DbSet<TwoFactor> TwoFactors { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ModulumUser>(entity =>
            {
                entity.ToTable(name: "tbl_user", "dbo");
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.RefreshToken).IsRequired(false);
                entity.Property(e => e.IsCadastroFinalizado).HasDefaultValue(true);

                // Campos removidos do IdentityUser
                entity.Ignore(e => e.PhoneNumber);
                entity.Ignore(e => e.PhoneNumberConfirmed);
                entity.Ignore(e => e.LockoutEnabled);
                entity.Ignore(e => e.LockoutEnd);
                entity.Ignore(e => e.AccessFailedCount);
            });

            // Ajustar essa linha para utilizar regras de acesso personalizada
            builder.Entity<ModulumRole>(entity =>
            {
                entity.ToTable(name: "tbl_role", "dbo");
            });
            builder.Entity<ModulumUserRole>(entity =>
            {
                entity.ToTable("tbl_user_role", "dbo");

                entity.HasOne<ModulumUser>().WithMany().HasForeignKey(ur => ur.UserId).IsRequired().OnDelete(DeleteBehavior.Cascade); // Define a relação com a tabela tbl_user

                entity.HasOne<ModulumRole>().WithMany().HasForeignKey(ur => ur.RoleId).IsRequired().OnDelete(DeleteBehavior.Cascade);

            });

            builder.Entity<ModulumUserClaim>(entity =>
            {
                entity.ToTable("tbl_user_claim", "dbo");
            });

            builder.Entity<ModulumUserLogin>(entity =>
            {
                entity.ToTable("tbl_user_login", "dbo");
                entity.HasKey(x => new { x.LoginProvider, x.ProviderKey }); // Definição da chave composta
            });

            builder.Entity<ModulumUserToken>(entity =>
            {
                entity.ToTable("tbl_user_token", "dbo");
            });

            builder.Entity<ModulumRoleClaim>(entity =>
            {
                entity.ToTable("tbl_role_claim", "dbo");
            });

            builder.Entity<Table>(entity =>
            {
                entity.HasMany(t => t.Fields).WithOne(f => f.Table).HasForeignKey(f => f.TableId);
                entity.ToTable("tbl_table", "dbo");
            });

            builder.Entity<Field>(entity =>
            {
                entity.ToTable("tbl_field", "dbo");
                entity.Property(f => f.Tipo).HasConversion<string>();
            });

            builder.Entity<TwoFactor>(entity =>
            {
                entity.ToTable("tbl_two_factor", "dbo");
                entity.HasOne<ModulumUser>().WithMany(u => u.TwoFactors).HasForeignKey(tf => tf.IdUser).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
