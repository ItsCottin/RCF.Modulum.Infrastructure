using Microsoft.EntityFrameworkCore;
using modulum.Infrastructure.Contexts;
using RCF.Modulum.Application.Interfaces.Services.CoreEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RCF.Modulum.Infrastructure.Services.CoreEntity
{
    public class DynamicTableService : IDynamicTableService
    {
        private readonly ModulumContext _context;

        public DynamicTableService(ModulumContext context)
        {
            _context = context;
        }

        public async Task CriarTabelaFisicaAsync(int tableId)
        {
            var table = await _context.Tables.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == tableId);

            if (table == null)
                throw new Exception("Tabela não encontrada.");

            string tableName = table.NomeTabela.Replace(" ", "_");

            var columns = table.Fields.Select(f =>
            {
                string column = $"{f.NomeColuna} {f.Tipo}";

                if (f.Tamanho.HasValue)
                    column += $"({f.Tamanho})";

                if (f.IsPrimaryKey)
                    column += " PRIMARY KEY";

                return column;
            });

            string createTableQuery = $"CREATE TABLE {tableName} ({string.Join(", ", columns)});";

            await _context.Database.ExecuteSqlRawAsync(createTableQuery);
        }
    }
}
