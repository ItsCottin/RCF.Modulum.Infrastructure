using Microsoft.EntityFrameworkCore;
using modulum.Application.Interfaces.Repositories;
using modulum.Application.Requests.Versao;
using modulum.Application.Responses.Versao;
using modulum.Domain.Entities;
using modulum.Domain.Entities.DynamicEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace modulum.Infrastructure.Repositories
{
    public class VersaoRepository : IVersaoRepository
    {
        private readonly IRepositoryAsync<NugetPacote, int> _repository;

        public VersaoRepository(IRepositoryAsync<NugetPacote, int> repository)
        {
            _repository = repository;
        }

        public async Task<bool> Update(PackageListResultRequest request)
        {
            var novosPacotes = MapearParaTabela(request);

            // Agrupar por projeto (PacoteRaiz)
            var gruposPorProjeto = novosPacotes.GroupBy(x => x.PacoteRaiz);

            foreach (var grupo in gruposPorProjeto)
            {
                var pacoteRaiz = grupo.Key;
                var novos = grupo.ToList();

                // Buscar os registros existentes no banco para esse projeto
                var existentes = await _repository.Entities
                    .Where(x => x.PacoteRaiz == pacoteRaiz)
                    .ToListAsync();

                // Atualizar e adicionar
                foreach (var novo in novos)
                {
                    var existente = existentes.FirstOrDefault(x => x.Pacote == novo.Pacote);
                    if (existente != null)
                    {
                        // Atualizar se as versões mudaram
                        if (existente.ResolvedVersion != novo.ResolvedVersion ||
                            existente.RequestedVersion != novo.RequestedVersion ||
                            existente.Framework != novo.Framework)
                        {
                            existente.RequestedVersion = novo.RequestedVersion;
                            existente.ResolvedVersion = novo.ResolvedVersion;
                            existente.Framework = novo.Framework;
                            await _repository.UpdateAsync(existente);
                        }

                        // Remove da lista de existentes, porque ele foi tratado
                        existentes.Remove(existente);
                    }
                    else
                    {
                        // Novo pacote, adicionar
                        await _repository.AddAsync(novo);
                    }
                }

                // Remover os que sobraram (não estão mais no projeto)
                foreach (var excluido in existentes)
                {
                    await _repository.DeleteAsync(excluido);
                }

                // Após delete ou update, aplicar as mudanças
                await _repository.SaveChangesAsync(); // Só se você criar esse método no repository base
            }

            return true;
        }

        public List<NugetPacote> MapearParaTabela(PackageListResultRequest json)
        {
            var pacotes = new List<NugetPacote>();

            foreach (var projeto in json.Projects)
            {
                if (projeto.Frameworks == null)
                    continue;

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
                            Framework = framework.Framework,
                            PacoteRaiz = projeto.Path
                        });
                    }
                }
            }
            return pacotes;
        }
    }
}
