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
using Microsoft.EntityFrameworkCore;
using modulum.Application.Interfaces.Services;

namespace modulum.Infrastructure.Services.DynamicEntity
{
    public class DynamicEntityService : IDynamicEntityService
    {
        private readonly ModulumContext _context;
        private readonly IMapper _mapper;
        private readonly ITableRepository _tableRepository;
        private readonly ICurrentUserService _currentUserService;

        public DynamicEntityService
            (
                ModulumContext context, 
                IMapper mapper, 
                ITableRepository tableRepository,
                ICurrentUserService currentUserService

            )
        {
            _context = context;
            _mapper = mapper;
            _tableRepository = tableRepository;
            _currentUserService = currentUserService;
        }

        // Service ja preparado para remover campos referente a base "NomeTabela" e "NomeCampoBase"
        public async Task<IResult<Table>> CriarMapTabelaAsync(CreateDynamicTableRequest request) 
        {
            var table = _mapper.Map<Table>(request);
            table = GetModelTableRegularizado(table);
            _context.Tables.Add(table);
            await _context.SaveChangesAsync();
            table = await _tableRepository.GetTableByName(table.NomeTabela);
            return await Result<Table>.SuccessAsync(table);
        }

        public async Task<IResult<Table>> ConsultarMapTabelaAsync(int tableId)
        {
            // ".Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == tableId)" correção do chat para trazer as informações das tabelas filhas
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == tableId);
            if (table == null)
            {
                return await Result<Table>.FailAsync("Tabela não encontrada");
            }
            return await Result<Table>.SuccessAsync(table);
        }

        public Table GetModelTableRegularizado(Table table) 
        {
            table.IdUsuario = int.Parse(_currentUserService.UserId);
            table.NomeTabela = table?.NomeTela.Replace(" ", "_") + "_" + _currentUserService.UserId;
            table?.Fields?.ToList().ForEach(field =>
            {
                field.NomeCampoBase = field.NomeCampoTela?.Replace(" ", "_");
            });
            return table;
        }
    }
}
