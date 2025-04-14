using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using modulum.Domain.Entities.DynamicEntity;
using modulum.Application.Interfaces.Repositories;

namespace modulum.Infrastructure.Repositories
{
    public class FieldRepository : IFieldRepository
    {
        private readonly IRepositoryAsync<Field, int> _repository;

        public FieldRepository(IRepositoryAsync<Field, int> repository)
        {
            _repository = repository;
        }

        public async Task AddField(Field field)
        {
            await _repository.AddAsync(field);
        }

        public async Task GetFieldById(int id)
        {
            await _repository.GetByIdAsync(id);
        }

        public async Task DeleteField(Field field)
        {
            await _repository.DeleteAsync(field);
        }

        public async Task UpdateField(Field field)
        {
            await _repository.UpdateAsync(field);
        }
    }
}
