using modulum.Domain.Entities.MapCoreEntity;
using modulum.Infrastructure.Contexts;
using RCF.Modulum.Application.Interfaces.Services.CoreEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RCF.Modulum.Infrastructure.Services.CoreEntity
{
    public class CoreEntityService : ICoreEntityService
    {
        private readonly ModulumContext _context;

        public CoreEntityService(ModulumContext context)
        {
            _context = context;
        }

        public async Task<int> CriarTabelaAsync(Table table)
        {
            _context.Tables.Add(table);
            await _context.SaveChangesAsync();
            return table.Id;
        }
    }
}
