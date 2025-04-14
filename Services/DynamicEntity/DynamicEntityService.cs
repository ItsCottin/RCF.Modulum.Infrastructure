using modulum.Domain.Entities.DynamicEntity;
using modulum.Infrastructure.Contexts;
using modulum.Application.Interfaces.Services.DynamicEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using modulum.Application.Requests.Dynamic.Create;
using modulum.Shared.Models;
using AutoMapper;
using modulum.Shared.Wrapper;
using modulum.Application.Interfaces.Repositories;
using modulum.Infrastructure.Repositories;

namespace modulum.Infrastructure.Services.DynamicEntity
{
    public class DynamicEntityService : IDynamicEntityService
    {
        private readonly ModulumContext _context;
        private readonly IMapper _mapper;
        private readonly ITableRepository _tableRepository;

        public DynamicEntityService
            (
                ModulumContext context, 
                IMapper mapper, 
                ITableRepository tableRepository
            )
        {
            _context = context;
            _mapper = mapper;
            _tableRepository = tableRepository;
        }

        public async Task<IResult<Table>> CriarMapTabelaAsync(CreateDynamicTableRequest request)
        {
            var table = _mapper.Map<Table>(request);
            _context.Tables.Add(table);
            await _context.SaveChangesAsync();
            table = await _tableRepository.GetTableByName(request.NomeTabela);
            return await Result<Table>.SuccessAsync(table);
        }
    }
}
