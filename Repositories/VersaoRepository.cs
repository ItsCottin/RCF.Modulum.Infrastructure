using Microsoft.EntityFrameworkCore;
using modulum.Application.Interfaces.Repositories;
using modulum.Application.Requests.Versao;
using modulum.Application.Responses.Versao;
using modulum.Domain.Entities;
using modulum.Domain.Entities.DynamicEntity;
using modulum.Infrastructure.Contexts;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace modulum.Infrastructure.Repositories
{
    public class VersaoRepository : IVersaoRepository
    {
        private readonly IRepositoryAsync<NugetPacote, int> _repository;
        private readonly ModulumContext _context;

        public VersaoRepository
        (
            IRepositoryAsync<NugetPacote, int> repository,
            ModulumContext context
        )
        {
            _repository = repository;
            _context = context;
        }

        public async Task<bool> Update(string? versao, PackageListResultRequest request)
        {
            var novosProjetos = MapearParaTabela(versao, request);

            foreach (var novoProjeto in novosProjetos)
            {
                // Verifica se já existe um projeto com o mesmo nome
                var projetoExistente = await _context.Set<Projeto>()
                    .FirstOrDefaultAsync(p => p.Nome == novoProjeto.Nome);

                if (projetoExistente == null)
                {
                    await _context.Set<Projeto>().AddAsync(novoProjeto);
                }
                else
                {
                    // Chapeu, remove todos os pacote da base para adicionar novamente com os pacotes do request
                    await _context.NugetPacotes.Where(p => p.ProjetoId == projetoExistente.Id).ExecuteDeleteAsync();

                    // Atualiza versão do projeto
                    projetoExistente.Versao = novoProjeto.Versao;
                    projetoExistente.Pacotes = novoProjeto.Pacotes;

                    _context.Update(projetoExistente);
                }

                await _context.SaveChangesAsync();
            }

            return true;
        }

        public List<Projeto> MapearParaTabela(string? versao, PackageListResultRequest json)
        {

            List<Projeto> projetos = new List<Projeto>();

            foreach (var projeto in json.Projects)
            {

                if (projeto.Frameworks == null)
                    continue;

                projetos.Add(new Projeto
                {
                    Nome = Path.GetFileNameWithoutExtension(projeto?.Path ?? string.Empty),
                    Versao = versao ?? "demo",
                    Pacotes = MapearPacotes(projeto)
                });

                
            }
            return projetos;
        }

        public List<NugetPacote> MapearPacotes(ProjectInfoRequest projeto)
        {

            var pacotes = new List<NugetPacote>();

            foreach (var framework in projeto.Frameworks)
            {
                if (framework.TopLevelPackages == null)
                    continue;

                foreach (var pacote in framework.TopLevelPackages)
                {
                    pacotes.Add(new NugetPacote
                    {
                        Pacote = pacote.Id,
                        RequestedVersion = pacote.RequestedVersion,
                        ResolvedVersion = pacote.ResolvedVersion,
                        Framework = framework.Framework
                    });
                }
            }

            return pacotes;
        }
    }
}
