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
using modulum.Application.Requests.Dynamic;

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

        public async Task<IResult> CriarMapTabelaAsync(CreateDynamicTableRequest request) 
        {
            var table = _mapper.Map<Table>(request);
            table = GetModelTableRegularizado(table);

            _context.Tables.Add(table);
            await _context.SaveChangesAsync();

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

        /// <summary>
        /// Metodo responsável por alterar os relacionamentos entre as tabelas
        /// Termo "Origem" se refere ao objeto cujo o PrimareyKey é o campo que vai virar ForeignKey no termo "Destino"
        /// Esse metodo tem como função adicionar ou alterar um relacionamento
        /// Responsabilidade de deletar um relacionamento foi migrado para o metodo DeletarRelacionamento e criado um novo endpoint
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public async Task<IResult> AlterRelacionamento(List<CreateDynamicRelationshipRequest> request)
        {
            var table = await _context.Tables
                .Include(t => t.Fields)
                .Include(t => t.RelacionamentosComoDestino)
                .Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId))
                .FirstOrDefaultAsync(t => t.Id == request.FirstOrDefault().TabelaOrigemId);

            if (table == null)
            {
                return await Result.FailAsync($"Tela não encontrada");
            }

            var relacionamentosRequest = _mapper.Map<List<Relationship>>(request);

            var relacionamentosExistentes = table.RelacionamentosComoDestino;

            var relacionamentosAdicionar = relacionamentosRequest
                .Where(x => x.Tipo == TypeRelationshipEnum.OneToMany && x.Id == 0)
                .Where(r => !relacionamentosExistentes.Any(e => e.TabelaDestinoId == r.TabelaDestinoId && e.TabelaOrigemId == r.TabelaOrigemId))
                .ToList();

            var relacionamentosAlterar = relacionamentosRequest
                .Where(r => !relacionamentosExistentes.Any(e => e.TabelaDestinoId == r.TabelaDestinoId && e.TabelaOrigemId == r.TabelaOrigemId && e.CampoParaExibicaoRelacionamento != r.CampoParaExibicaoRelacionamento) && r.Tipo == TypeRelationshipEnum.OneToMany && r.Id > 0)
                .ToList();

            foreach (var relacionamento in relacionamentosAdicionar)
            {
                var tableDestino = await _context.Tables.FirstOrDefaultAsync(t => t.Id == relacionamento.TabelaDestinoId);
                relacionamento.TabelaDestino = tableDestino;

                var tableOrigem = await _context.Tables.FirstOrDefaultAsync(t => t.Id == relacionamento.TabelaOrigemId);
                relacionamento.TabelaOrigem = tableOrigem;

                relacionamento.CampoOrigem = tableOrigem.CampoPK;


                var nomeColuna = $"{relacionamento.CampoOrigem}_{relacionamento.TabelaOrigem.NomeTabela}";
                var fkName = $"FK_{relacionamento.TabelaDestino.NomeTabela}_{nomeColuna}_REF_{relacionamento.TabelaOrigem.NomeTabela}_{relacionamento.CampoOrigem}";

                relacionamento.CampoDestino = nomeColuna;

                relacionamento.NomeConstraint = fkName;
                var relacionamentos = new List<Relationship>();
                relacionamentos.Add(relacionamento);

                // Avaliar a nescessidade de armazenar o relacionamento "voltando"
                // Avaliado e foi nescessario
                relacionamentos.Add(new Relationship 
                {
                    TabelaOrigemId = tableDestino.Id,
                    TabelaDestinoId = tableOrigem.Id,
                    TabelaDestino = tableOrigem,
                    TabelaOrigem = tableDestino,
                    CampoDestino = relacionamento.CampoOrigem,
                    CampoOrigem = relacionamento.CampoDestino,
                    NomeConstraint = fkName,
                    IsObrigatorio = relacionamento.IsObrigatorio,
                    Tipo = TypeRelationshipEnum.ManyToOne // Se for nescessario, avaliar qual tipo usar para o relacionamento "voltando"
                                                          // Avaliado, e foi utilizado o tipo 'ManyToOne'
                });

                await _context.Relationships.AddRangeAsync(relacionamentos);

                // Adiciona a coluna correspondente na tabela dinâmica como FOREIGN KEY
                var sql = $@"ALTER TABLE {relacionamento.TabelaDestino.NomeTabela} ADD {nomeColuna} INT, CONSTRAINT {fkName} FOREIGN KEY ({nomeColuna}) REFERENCES {relacionamento.TabelaOrigem.NomeTabela}({relacionamento.CampoOrigem});";
                await _context.Database.ExecuteSqlRawAsync(sql);

                await _context.Fields.AddAsync(new Field 
                    { 
                        IsForeigeKey = true, 
                        IsObrigatorio = relacionamento.IsObrigatorio, 
                        IsPrimaryKey = false,
                        NomeCampoBase = nomeColuna,
                        NomeCampoTela = request.FirstOrDefault(x => x.TabelaOrigemId == relacionamento.TabelaOrigemId).CampoTelaParaExibicaoRelacionamento,
                        Tipo = TypeColumnEnum.INT,
                        TableId = relacionamento.TabelaDestinoId,
                        Tamanho = null
                });

                await _context.SaveChangesAsync();
            }

            foreach (var item in relacionamentosAlterar)
            {
                var relacionamentoDb = await _context.Relationships.FirstOrDefaultAsync(x => x.Id == item.Id);
                if (relacionamentoDb == null)
                {
                    return await Result.FailAsync("Relacionamento não encontrado");
                }

                relacionamentoDb.CampoParaExibicaoRelacionamento = item.CampoParaExibicaoRelacionamento;

                _context.Update(relacionamentoDb);
                await _context.SaveChangesAsync();
            }

            return await Result.SuccessAsync("Relacionamentos alterados com sucesso");
        }

        public async Task<IResult<List<CreateDynamicRelationshipRequest>>> ConsultarRelacionamento(int tableId)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == tableId);
            if (table == null)
            {
                return await Result<List<CreateDynamicRelationshipRequest>>.FailAsync("Tabela não encontrada");
            }
            
            var relacionamento = await _context.Relationships.Where(r => r.TabelaDestinoId == tableId).ToListAsync();
            var retorno = _mapper.Map<List<CreateDynamicRelationshipRequest>>(relacionamento);
            retorno.ForEach(r =>
            {
                r.NomeTelaDestino = table.NomeTela;
                r.NomeTelaOrigem = _context.Tables.FirstOrDefault(t => t.Id == r.TabelaOrigemId).NomeTela;
            });
            return await Result<List<CreateDynamicRelationshipRequest>>.SuccessAsync(retorno);
        }

        public async Task<IResult> DeletarRelacionamento(DynamicForIdRequest request)
        {
            var relacionamentoRequest = await _context.Relationships
                .Include(t => t.TabelaDestino)
                .Include(t => t.TabelaOrigem)
                .FirstOrDefaultAsync(t => t.Id == request.IdRegistro);

            if (relacionamentoRequest == null)
            {
                return await Result.FailAsync($"Relacionamento não encontrada");
            }

            var relacionamentoInverso = await _context.Relationships
                .Include(t => t.TabelaDestino)
                .Include(t => t.TabelaOrigem)
                .FirstOrDefaultAsync(t => t.TabelaDestinoId == relacionamentoRequest.TabelaOrigemId && t.TabelaOrigemId == relacionamentoRequest.TabelaDestinoId);

            if (relacionamentoInverso == null)
            {
                return await Result.FailAsync($"Inconsistncia no banco de dados, entre em contato com o suporte");
            }

            Table tableParaDeleteForeigeKey = null;
            string nomeConstraintParaDelete = null;
            string nomeCampoParaDelete = null;

            if (relacionamentoRequest.Tipo == TypeRelationshipEnum.OneToMany)
            {
                tableParaDeleteForeigeKey = relacionamentoRequest.TabelaDestino;
                nomeConstraintParaDelete = relacionamentoRequest.NomeConstraint;
                nomeCampoParaDelete = relacionamentoRequest.CampoDestino;
            }
            else
            {
                tableParaDeleteForeigeKey = relacionamentoInverso.TabelaDestino;
                nomeConstraintParaDelete = relacionamentoInverso.NomeConstraint;
                nomeCampoParaDelete = relacionamentoInverso.CampoDestino;
            }

            var dropConstraintSql = $"ALTER TABLE {tableParaDeleteForeigeKey.NomeTabela} DROP CONSTRAINT {nomeConstraintParaDelete};";
            await _context.Database.ExecuteSqlRawAsync(dropConstraintSql);
            
            var dropColumnSql = $"ALTER TABLE {tableParaDeleteForeigeKey.NomeTabela} DROP COLUMN {nomeCampoParaDelete};";
            await _context.Database.ExecuteSqlRawAsync(dropColumnSql);

            _context.Relationships.Remove(relacionamentoRequest);
            _context.Relationships.Remove(relacionamentoInverso);

            var campoParaDelete = await _context.Fields
                .FirstOrDefaultAsync(f => f.NomeCampoBase == nomeCampoParaDelete && f.TableId == tableParaDeleteForeigeKey.Id);

            _context.Fields.Remove(campoParaDelete);

            await _context.SaveChangesAsync();

            return await Result.SuccessAsync("Remoção de vinculo realizado com sucesso");
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
            var table = await _context.Tables
                .Include(t => t.Fields)
                .Include(t => t.RelacionamentosComoDestino)
                .Include(t => t.RelacionamentosComoOrigem)
                .Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId))
                .FirstOrDefaultAsync(t => t.Id == tableId);
            if (table == null)
            {
                return await Result.FailAsync("Tela não encontrada");
            }

            var IsExisteRelacionamentoOneToMany = table.RelacionamentosComoOrigem.Any(r => r.Tipo == TypeRelationshipEnum.OneToMany);

            if (IsExisteRelacionamentoOneToMany)
            {
                return await Result.FailAsync($"Não é possível deletar a tela '{table.NomeTela}', existem referencias dessa tela em outras telas");
            }

            // Chapeu pois o metodo 'DeletarRelacionamento' deleta a lista 'table.RelacionamentosComoDestino' e isso da erro de colletion modificaty
            List<int> IdsParaDeletar = new List<int>(); 

            if (table.RelacionamentosComoDestino != null)
            { 
                foreach (var relacionamento in table.RelacionamentosComoDestino)
                {
                    IdsParaDeletar.Add(relacionamento.Id);
                }
            }

            foreach (int IdParaDeletar in IdsParaDeletar)
            {
                await DeletarRelacionamento(new DynamicForIdRequest { IdRegistro = IdParaDeletar });
            }

            // Criar validação se existe relacionamento na tabela que esta sendo excluida
            // Agora esta feito nas acima =)

            var sql = $"DROP TABLE {table.NomeTabela};";
            await _context.Database.ExecuteSqlRawAsync(sql);
            _context.Tables.Remove(table);
            _context.SaveChanges();
            return await Result.SuccessAsync($"Tela '{table.NomeTela}' excluída com sucesso");
        }
    }
}
