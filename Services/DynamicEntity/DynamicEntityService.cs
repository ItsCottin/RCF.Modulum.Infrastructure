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
using modulum.Application.Requests.Dynamic.Update;
using modulum.Shared.Enum;
using modulum.Application.Requests.Dynamic.Relationship;
using Azure.Core;
using modulum.Application.Responses.Dynamic;
using MediatR;

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
        public async Task<IResult> CriarMapTabelaAsync(CreateDynamicTableRequest request) 
        {
            var table = _mapper.Map<Table>(request);
            table = GetModelTableRegularizado(table);

            //var relacionamentoBase = new List<Relationship>();
            //bool IsExisteRelacionamento = false;
            //if (request.Relacionamentos != null)
            //{
            //    if (request.Relacionamentos.Count != 0)
            //    {
            //        IsExisteRelacionamento = true;
            //        foreach (var relacionamento in request.Relacionamentos)
            //        {
            //            var tableDestino = await _context.Tables.FirstOrDefaultAsync(x => x.Id == relacionamento.IdTable);
            //            var campo = new Field()
            //            {
            //                NomeCampoBase = relacionamento.NomeCampoId + "_" + tableDestino.NomeTabela,
            //                NomeCampoTela = tableDestino.CampoParaExibicaoRelacionamento,
            //                Tipo = TypeColumnEnum.INT,
            //                IsPrimaryKey = false,
            //                IsForeigeKey = true,
            //                TableId = table.Id
            //            };
            //            table.Fields.Add(campo);
            //        };
            //    }
            //}

            _context.Tables.Add(table);
            await _context.SaveChangesAsync();
            //table = await _tableRepository.GetTableByName(table.NomeTabela);
            //
            //if (IsExisteRelacionamento)
            //{
            //    foreach (var relacionamento in request.Relacionamentos)
            //    {
            //        var tableDestino = await _context.Tables.Include(t => t.Fields).FirstOrDefaultAsync(x => x.Id == relacionamento.IdTable);
            //        var NomeCampoOrigem = table.Fields.FirstOrDefault(x => x.NomeCampoBase.Contains(tableDestino.NomeTabela) && x.IsForeigeKey);
            //        relacionamentoBase.Add(new Relationship()
            //        {
            //            Id = 0,
            //            TabelaOrigemId = table.Id,
            //            TabelaOrigem = table,
            //            TabelaDestinoId = relacionamento.IdTable,
            //            TabelaDestino = tableDestino,
            //            CampoOrigem = NomeCampoOrigem.NomeCampoBase,
            //            CampoDestino = tableDestino.CampoPK,
            //        });
            //    }
            //}
            //
            //await _context.Relationships.AddRangeAsync(relacionamentoBase);
            //await _context.SaveChangesAsync();

            try
            {
                if (table == null)
                    return await Result.FailAsync($"Tela '{request.NomeTela}' não encontrada");

                var columnDefinitions = new List<string>();
                var foreignKeyConstraints = new List<string>();
                int fkIndex = 1;

                foreach (var f in table.Fields)
                {
                    string column = $"{f.NomeCampoBase} {f.Tipo}";

                    if (f.Tamanho.HasValue && f.Tipo.ToString().Equals("VARCHAR", StringComparison.OrdinalIgnoreCase))
                        column += $"({f.Tamanho})";

                    if (f.IsPrimaryKey)
                        column += " PRIMARY KEY IDENTITY(1,1)";

                    columnDefinitions.Add(column);

                    //if (f.IsForeigeKey)
                    //{
                    //    // Procura o relacionamento com base no nome do campo
                    //    var relacionamento = relacionamentoBase.FirstOrDefault(r => r.CampoOrigem == f.NomeCampoBase);
                    //    if (relacionamento != null)
                    //    {
                    //        string fkName = $"FK_{table.NomeTabela}_{fkIndex++}";
                    //        string constraint = $"CONSTRAINT {fkName} FOREIGN KEY ({f.NomeCampoBase}) REFERENCES {relacionamento.TabelaDestino.NomeTabela}({relacionamento.CampoDestino})";
                    //        foreignKeyConstraints.Add(constraint);
                    //    }
                    //}
                }

                var allDefinitions = columnDefinitions.Concat(foreignKeyConstraints);
                string createTableQuery = $"CREATE TABLE {table.NomeTabela} ({string.Join(", ", allDefinitions)});";

                await _context.Database.ExecuteSqlRawAsync(createTableQuery);
                return await Result.SuccessAsync($"Sua tela '{table.NomeTela}' foi criada com sucesso");
            }
            catch (Exception ex)
            {
                _context.Fields.RemoveRange(table.Fields);
                _context.Tables.Remove(table);
                await _context.SaveChangesAsync();
                return await Result.FailAsync(ex.Message);
            }
        }

        public async Task<IResult> AlterRelacionamento(List<CreateDynamicRelationshipRequest> request)
        {
            // Obtém a tabela de origem com seus campos
            var table = await _context.Tables
                .Include(t => t.Fields)
                .Include(t => t.RelacionamentosComoOrigem)
                .Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId))
                .FirstOrDefaultAsync(t => t.Id == request.FirstOrDefault().TabelaOrigemId);

            if (table == null)
            {
                return await Result.FailAsync($"Tela não encontrada");
            }

            // Mapeia os relacionamentos do request para a entidade Relationship
            var relacionamentosRequest = _mapper.Map<List<Relationship>>(request);

            // Relacionamentos existentes no banco de dados
            var relacionamentosExistentes = table.RelacionamentosComoOrigem;

            // Identifica relacionamentos a serem adicionados
            var relacionamentosAdicionar = relacionamentosRequest
                .Where(r => !relacionamentosExistentes.Any(e => e.TabelaDestinoId == r.TabelaDestinoId && e.CampoOrigem == r.CampoOrigem))
                .ToList();

            // Identifica relacionamentos a serem removidos
            var relacionamentosRemover = relacionamentosExistentes
                .Where(e => !relacionamentosRequest.Any(r => r.TabelaDestinoId == e.TabelaDestinoId && r.CampoOrigem == e.CampoOrigem))
                .ToList();

            // Adiciona novos relacionamentos
            foreach (var relacionamento in relacionamentosAdicionar)
            {
                // correção pensada por mim mesmo
                var tableDestino = await _context.Tables.FirstOrDefaultAsync(t => t.Id == relacionamento.TabelaDestinoId);
                relacionamento.TabelaDestino = tableDestino;

                var tableOrigem = await _context.Tables.FirstOrDefaultAsync(t => t.Id == relacionamento.TabelaOrigemId);
                relacionamento.TabelaOrigem = tableOrigem;

                

                // Define o nome da coluna e o nome da constraint de chave estrangeira
                var nomeColuna = $"{relacionamento.CampoDestino}_{relacionamento.TabelaDestino.NomeTabela}";
                var fkName = $"FK_{table.NomeTabela}_{relacionamento.TabelaDestino.NomeTabela}_{relacionamento.CampoDestino}";

                relacionamento.CampoOrigem = nomeColuna;

                // Adiciona o relacionamento na tabela
                relacionamento.NomeConstraint = fkName;
                table.RelacionamentosComoOrigem.Add(relacionamento);

                // Adiciona a coluna correspondente na tabela dinâmica como FOREIGN KEY
                var sql = $@"ALTER TABLE {table.NomeTabela} ADD {nomeColuna} INT, CONSTRAINT {fkName} FOREIGN KEY ({nomeColuna}) REFERENCES {relacionamento.TabelaDestino.NomeTabela}({relacionamento.CampoDestino});";
                await _context.Database.ExecuteSqlRawAsync(sql);

                _context.Fields.Add(new Field 
                    { 
                        IsForeigeKey = true, 
                        IsObrigatorio = relacionamento.IsObrigatorio, 
                        IsPrimaryKey = false,
                        NomeCampoBase = nomeColuna,
                        NomeCampoTela = request.FirstOrDefault(x => x.TabelaOrigemId == relacionamento.TabelaOrigemId).CampoTelaParaExibicaoRelacionamento,
                        Tipo = TypeColumnEnum.INT,
                        TableId = table.Id,
                        Tamanho = null
                });

                await _context.SaveChangesAsync();
            }


            // Remove relacionamentos antigos
            foreach (var relacionamento in relacionamentosRemover)
            {
                // correção pensada por mim mesmo
                var tableDestino = await _context.Tables.FirstOrDefaultAsync(t => t.Id == relacionamento.TabelaDestinoId);
                relacionamento.TabelaDestino = tableDestino;

                var tableOrigem = await _context.Tables.FirstOrDefaultAsync(t => t.Id == relacionamento.TabelaDestinoId);
                relacionamento.TabelaOrigem = tableOrigem;

                // Define o nome da coluna e da constraint
                var nomeColuna = $"{relacionamento.CampoOrigem}_{relacionamento.TabelaDestino.NomeTabela}";
                var fkName = relacionamento.NomeConstraint;

                // Remove o relacionamento da tabela
                table.RelacionamentosComoOrigem.Remove(relacionamento);

                // Remove a constraint de chave estrangeira
                var dropConstraintSql = $"ALTER TABLE {table.NomeTabela} DROP CONSTRAINT {fkName};";
                await _context.Database.ExecuteSqlRawAsync(dropConstraintSql);

                // Remove a coluna correspondente na tabela dinâmica
                var dropColumnSql = $"ALTER TABLE {table.NomeTabela} DROP COLUMN {nomeColuna};";
                await _context.Database.ExecuteSqlRawAsync(dropColumnSql);
            }


            // Salva as alterações no banco de dados
            _context.Tables.Update(table);

            return await Result.SuccessAsync("Relacionamentos alterados com sucesso");
        }

        public async Task<IResult<List<CreateDynamicRelationshipRequest>>> ConsultarRelacionamento(int tableId)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == tableId);
            if (table == null)
            {
                return await Result<List<CreateDynamicRelationshipRequest>>.FailAsync("Tabela não encontrada");
            }
            
            var relacionamento = await _context.Relationships.Where(r => r.TabelaOrigemId == tableId).ToListAsync();
            var retorno = _mapper.Map<List<CreateDynamicRelationshipRequest>>(relacionamento);
            retorno.ForEach(r =>
            {
                r.NomeTelaOrigem = table.NomeTabela;
                r.NomeTelaDestino = _context.Tables.FirstOrDefault(t => t.Id == r.TabelaDestinoId).NomeTela;
            });
            return await Result<List<CreateDynamicRelationshipRequest>>.SuccessAsync(retorno);
        }

        public async Task<IResult<CreateDynamicTableRequest>> ConsultarMapTabelaAsync(int tableId)
        {
            // ".Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == tableId)" correção do chat para trazer as informações das tabelas filhas
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == tableId);
            if (table == null)
            {
                return await Result<CreateDynamicTableRequest>.FailAsync("Tabela não encontrada");
            }
            var retorno = _mapper.Map<CreateDynamicTableRequest>(table);
            return await Result<CreateDynamicTableRequest>.SuccessAsync(retorno);
        }

        public Table GetModelTableRegularizado(Table table) 
        {
            table.IdUsuario = int.Parse(_currentUserService.UserId);
            table.NomeTabela = "_" + table?.NomeTela.Replace(" ", "_") + "_" + _currentUserService.UserId;
            table?.Fields?.ToList().ForEach(field =>
            {
                if (field.IsPrimaryKey)
                {
                    field.NomeCampoBase = field.NomeCampoTela?.Replace(" ", "_");
                }
                else
                {
                    field.NomeCampoBase = "_" + field.NomeCampoTela?.Replace(" ", "_");
                }  
            });
            return table;
        }

        public async Task<IResult> RenameNomeTabelaTelaAsync(RenameNomeTabelaTelaRequest request)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == request.IdTabela);
            if (table == null)
            {
                return await Result.FailAsync("Tela não encontrada");
            }

            if (request.NovoNome.Equals(table.NomeTela))
            {
                return await Result.FailAsync($"Nome informado '{request.NovoNome}' é o mesmo do atual");
            }

            var nomeAntigo = table.NomeTela;
            table.NomeTela = request.NovoNome;
            _context.Update(table);
            _context.SaveChanges();

            return await Result.SuccessAsync($"Tela de nome '{nomeAntigo}' alterado com sucesso para '{table.NomeTela}'");
        }

        public async Task<IResult> AlterMapTableAsync(CreateDynamicTableRequest request)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == request.Id);
            if (table == null)
            {
                return await Result.FailAsync($"Tela '{request.NomeTela}' não encontrada");
            }

            if (!request.NomeTela.Equals(table.NomeTela))
            {
                var retorno = await RenameNomeTabelaTelaAsync(new RenameNomeTabelaTelaRequest { IdTabela = table.Id, NovoNome = request.NomeTela });
                if (!retorno.Succeeded)
                {
                    return retorno;
                }
            }

            // Identifica os campos a serem removidos
            var idsCamposRequest = request.Campos.Select(x => x.Id).Where(id => id > 0).ToHashSet(); // Considera apenas IDs válidos
            var camposRemover = table.Fields.Where(f => !idsCamposRequest.Contains(f.Id)).ToList();

            // Remove os campos que não estão no request
            foreach (var campoRemover in camposRemover)
            {
                var sql = $"ALTER TABLE {table.NomeTabela} DROP COLUMN {campoRemover.NomeCampoBase};";
                await _context.Database.ExecuteSqlRawAsync(sql);
                _context.Fields.Remove(campoRemover);
            }
            if (camposRemover.Any())
            {
                await _context.SaveChangesAsync();
            }

            bool IsHouveAlteracao = false;

            foreach (var origemNome in table.Fields)
            {
                var campoSelecionado = request.Campos.FirstOrDefault(x => x.Id == origemNome.Id);
                if (campoSelecionado != null)
                {
                    if (!campoSelecionado.NomeCampoTela.Equals(origemNome.NomeCampoTela))
                    {
                        origemNome.NomeCampoTela = campoSelecionado.NomeCampoTela;
                        _context.Update(origemNome);
                        IsHouveAlteracao = true;
                    }
                }
            }

            if (IsHouveAlteracao)
            {
                await _context.SaveChangesAsync();
            }

            // Adiciona novos campos
            var camposAdicionar = request.Campos.Where(x => x.Id == 0 || !table.Fields.Any(f => f.Id == x.Id)).ToList();
            foreach (var campoNovo in camposAdicionar)
            {
                var tipoSql = MapearTipoSql(campoNovo.Tipo!.Value, campoNovo.Tamanho);
                var nomeTabela = table.NomeTabela;
                var nomeColuna = campoNovo.NomeCampoBase;

                var sql = $"ALTER TABLE {nomeTabela} ADD {nomeColuna} {tipoSql};";
                await _context.Database.ExecuteSqlRawAsync(sql);

                var entidadeCampo = _mapper.Map<Field>(campoNovo);
                entidadeCampo.TableId = table.Id;
                _context.Fields.Add(entidadeCampo);
            }
            if (camposAdicionar.Any())
            {
                await _context.SaveChangesAsync();
            }


            return await Result.SuccessAsync($"Alteração da sua tela '{table.NomeTela}' realizada com sucesso");
        }


        private string MapearTipoSql(TypeColumnEnum tipo, int? tamanho)
        {
            return tipo switch
            {
                TypeColumnEnum.INT => "INT",
                TypeColumnEnum.VARCHAR => $"VARCHAR({tamanho ?? 255})",
                TypeColumnEnum.BIT => "BIT",
                TypeColumnEnum.DATE => "DATE",
                _ => throw new NotImplementedException($"Tipo não mapeado: {tipo}")
            };
        }

        private string FormatSqlValue(string tipo, object valor)
        {
            return tipo.ToLower() switch
            {
                "varchar" or "string" => $"'{valor}'",
                "date" => $"'{Convert.ToDateTime(valor):yyyy-MM-dd HH:mm:ss}'",
                "int" or "integer" => valor.ToString(),
                "bit" or "boolean" => valor.ToString(),
                _ => $"'{valor}'"
            };
        }

        public async Task<IResult> DeleteMapTableAsync(int tableId)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == tableId);
            if (table == null)
            {
                return await Result.FailAsync("Tela não encontrada");
            }

            // Criar validação se existe relacionamento na tabela que esta sendo excluida
            var sql = $"DROP TABLE {table.NomeTabela};";
            await _context.Database.ExecuteSqlRawAsync(sql);
            _context.Tables.Remove(table);
            _context.SaveChanges();
            return await Result.SuccessAsync($"Tela '{table.NomeTela}' excluída com sucesso");
        }
    }
}
