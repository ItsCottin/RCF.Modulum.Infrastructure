using AutoMapper;
using Microsoft.EntityFrameworkCore;
using modulum.Application.Interfaces.Repositories;
using modulum.Application.Requests.Versao;
using modulum.Application.Responses.Versao;
using modulum.Infrastructure.Contexts;
using modulum.Shared.Wrapper;
using RCF.Modulum.Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RCF.Modulum.Infrastructure.Services.Versao
{
    public class VersaoService : IVersao
    {
        private readonly ModulumContext _context;
        private readonly IMapper _mapper;
        private readonly IVersaoRepository _versaoRepository;

        public VersaoService
            (
            ModulumContext context,
            IMapper mapper,
            IVersaoRepository versaoRepository
            )
        { 
            _context = context;
            _mapper = mapper;
            _versaoRepository = versaoRepository;
        }

        public async Task<IResult<List<VersaoResponse>>> GetAllVersao()
        {
            var resultado = await _context.NugetPacotes.AsNoTracking().ToListAsync();
            var response = resultado.Select(v => _mapper.Map<VersaoResponse>(v)).ToList();
            if (!resultado.Any())
                return await Result<List<VersaoResponse>>.SuccessAsync(new List<VersaoResponse>(), "Nenhum pacote foi encontrado.");

            return await Result<List<VersaoResponse>>.SuccessAsync(response, "Lista de pacotes e versões");
        }

        public async Task<bool> AddEditPacotes(PackageListResultRequest request)
        {
            return await _versaoRepository.Update(request);
        }
    }
}
