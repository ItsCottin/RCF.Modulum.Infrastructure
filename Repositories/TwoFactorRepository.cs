using Microsoft.EntityFrameworkCore;
using modulum.Application.Interfaces.Repositories;
using modulum.Domain.Entities.Account;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace modulum.Infrastructure.Repositories
{
    public class TwoFactorRepository : ITwoFactorRepository
    {
        private readonly IRepositoryAsync<TwoFactor, int> _repository;

        public TwoFactorRepository(IRepositoryAsync<TwoFactor, int> repository)
        {
            _repository = repository;
        }

        public async Task<TwoFactor> GetTwoFactorByUserId(int userId)
        {
            return await _repository.Entities.FirstOrDefaultAsync(b => b.IdUser == userId);
        }

        public async Task UpdateTwoFactor(TwoFactor twoFactor)
        {
            await _repository.UpdateAsync(twoFactor);
        }
    }
}
