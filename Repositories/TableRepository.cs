using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using modulum.Application.Interfaces.Repositories;
using modulum.Domain.Entities.Account;
using modulum.Domain.Entities.DynamicEntity;
using modulum.Infrastructure.Contexts;

namespace modulum.Infrastructure.Repositories
{
    public class TableRepository : ITableRepository
    {
        private readonly IRepositoryAsync<Table, int> _repository;
        private readonly ModulumContext _context;

        public TableRepository(ModulumContext context, IRepositoryAsync<Table, int> repository)
        {
            _repository = repository;
            _context = context;
        }

        public async Task AddTable(Table table)
        { 
            await _repository.AddAsync(table);
        }

        public async Task GetTableById(int id)
        {
            await _repository.GetByIdAsync(id);
        }

        public async Task DeleteTable(Table table)
        {
            await _repository.DeleteAsync(table);
        }

        public async Task UpdateTable(Table table)
        {
            await _repository.UpdateAsync(table);
        }

        public async Task<Table?> GetTableByName(string name)
        {
            return _context.Tables.Include(t => t.Fields).FirstOrDefaultAsync(t => t.NomeTabela == name).Result;
        }
    }
}
