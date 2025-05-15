using Microsoft.EntityFrameworkCore;
using modulum.Infrastructure.Contexts;
using modulum.Application.Interfaces.Services.DynamicEntity;
using modulum.Domain.Entities.DynamicEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using modulum.Shared.Wrapper;
using modulum.Application.Requests.Dynamic;
using AutoMapper;
using System.Reflection.PortableExecutable;
using static OfficeOpenXml.ExcelErrorValue;
using modulum.Application.Interfaces.Services;
using modulum.Shared.Enum;
using Azure.Core;
using Newtonsoft.Json.Linq;

namespace modulum.Infrastructure.Services.DynamicEntity
{
    public class DynamicTableService : IDynamicTableService
    {
        private readonly ModulumContext _context;
        private readonly IMapper _mapper;
        private readonly ICurrentUserService _currentUserService;

        public DynamicTableService
            (
            IMapper mapper,
            ModulumContext context,
            ICurrentUserService currentUserService
            )
        {
            _mapper = mapper;
            _context = context;
            _currentUserService = currentUserService;
        }

        //public async Task<IResult> CriarTabelaFisicaAsync(Table table)
        //{
        //    try
        //    {
        //        //var table = await _context.Tables.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == tableId);
        //
        //        if (table == null)
        //            return await Result.FailAsync("Tabela não encontrada.");
        //
        //        //string tableName = table.NomeTabela.Replace(" ", "_");
        //
        //        var columns = table.Fields.Select(f =>
        //        {
        //            string column = $"{f.NomeCampoBase} {f.Tipo}";
        //
        //            if (f.Tamanho.HasValue && (f.Tipo.ToString().Equals("VARCHAR", StringComparison.OrdinalIgnoreCase)))
        //            {
        //                column += $"({f.Tamanho})";
        //            }
        //
        //            if (f.IsPrimaryKey) // Adicionar validação para caso IsPrimaryKey = true vier em mais de um campo
        //                column += " PRIMARY KEY IDENTITY";
        //
        //            return column;
        //        });
        //
        //        string createTableQuery = $"CREATE TABLE {table.NomeTabela} ({string.Join(", ", columns)});";
        //
        //        await _context.Database.ExecuteSqlRawAsync(createTableQuery);
        //        return Result.Success("Tabela criada com sucesso");
        //    }
        //    catch (Exception ex)
        //    {
        //        _context.Fields.RemoveRange(table.Fields);
        //        _context.Tables.Remove(table);
        //        await _context.SaveChangesAsync();
        //        return await Result.FailAsync(ex.Message);
        //    }
        //}

        public async Task<IResult> InsertAsync(DynamicTableRequest request)
        {
            // Implementar futuramente aqui as permissoes de acesso dada do usuario criador da tabela a outros usuarios registrados
            // Codigo antigo, apenas comentado caso o novo codigo nao funcionar
            //var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == request.Id);
            //
            //if (table == null)
            //    return await Result.FailAsync("Tabela não encontrada.");
            //
            //foreach (var registro in request.Resultados)
            //{
            //    var columns = new List<string>();
            //    var values = new List<string>();
            //
            //    foreach (var field in registro.Valores)
            //    {
            //        var columnName = field.NomeCampoBase;
            //        var valor = FormatSqlValue(field.Tipo.ToString(), field.Valor);
            //        columns.Add(columnName);
            //        values.Add(valor);
            //    }
            //
            //    var sql = $"INSERT INTO {table.NomeTabela} ({string.Join(",", columns)}) VALUES ({string.Join(",", values)});";
            //    await _context.Database.ExecuteSqlRawAsync(sql);
            //}
            //
            //return await Result.SuccessAsync("Registro incluído com sucesso"); ;

            var table = await _context.Tables.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == request.Id && t.IdUsuario == int.Parse(_currentUserService.UserId));

            if (table == null)
                return await Result.FailAsync($"Tela '{request.NomeTela}' não encontrada");

            var nomeTabela = table.NomeTabela;
            var nomeCampoPk = table.Fields.FirstOrDefault(f => f.IsPrimaryKey)?.NomeCampoBase;

            if (string.IsNullOrWhiteSpace(nomeCampoPk))
                return await Result.FailAsync("Chave primária não definida para a tabela.");

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = $@"SELECT COLUMNPROPERTY(OBJECT_ID('{nomeTabela}'), '{nomeCampoPk}','IsIdentity')";

            var result = await command.ExecuteScalarAsync();
            int isIdentity = Convert.ToInt32(result);

            foreach (var registro in request.Resultados)
            {
                var columns = new List<string>();
                var values = new List<string>();

                if (isIdentity == 0)
                {
                    var maxIdQuery = $"SELECT ISNULL(MAX([{nomeCampoPk}]), 0) + 1 FROM {nomeTabela}";
                    var nextId = await _context.Database.ExecuteScalarAsync<int>(maxIdQuery);
                    columns.Add(nomeCampoPk);
                    values.Add(nextId.ToString());
                }

                foreach (var field in registro.Valores)
                {
                    var columnName = field.NomeCampoBase;
                    var valor = FormatSqlValue(field.Tipo.ToString(), field.Valor);
                    columns.Add(columnName);
                    values.Add(valor);
                }

                var sql = $"INSERT INTO {nomeTabela} ({string.Join(",", columns)}) VALUES ({string.Join(",", values)});";
                await _context.Database.ExecuteSqlRawAsync(sql);
            }

            return await Result.SuccessAsync("Registro incluído com sucesso");
        }
        
        public async Task<IResult> UpdateAsync(DynamicTableRequest request)
        {
            // Implementar futuramente aqui as permissoes de acesso dada do usuario criador da tabela a outros usuarios registrados
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == request.Id);

            if (table == null)
                return await Result.FailAsync($"Tela '{request.NomeTela}' não encontrada");

            foreach (var registro in request.Resultados)
            {
                var setClauses = new List<string>();
                string whereClause = string.Empty;

                foreach (var field in registro.Valores)
                {
                    var columnName = field.NomeCampoBase;
                    var valor = FormatSqlValue(field.Tipo.ToString(), field.Valor);

                    if (field.IsPrimaryKey)
                        whereClause = $"{columnName} = {valor}";
                    else
                        setClauses.Add($"{columnName} = {valor}");
                }

                var sql = $"UPDATE {table.NomeTabela} SET {string.Join(",", setClauses)} WHERE {whereClause};";
                await _context.Database.ExecuteSqlRawAsync(sql);
            }

            return await Result.SuccessAsync("Registro alterado com sucesso");
        }

        public async Task<IResult> DeletePorIdAsync(DynamicForIdRequest request)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == request.IdTable);

            if (table == null)
                return await Result.FailAsync("Tela não encontrada.");

            var relacionamentos = await _context.Relationships.Where(t => t.TabelaOrigemId == table.Id).ToArrayAsync();

            foreach (var r in relacionamentos)
            {
                var select = $"SELECT COUNT(1) FROM {r.TabelaDestino.NomeTabela} WHERE {r.CampoDestino} = {request.IdRegistro};";

                // Executa a consulta e verifica se existem registros
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = select;

                var result = await command.ExecuteScalarAsync();
                int count = Convert.ToInt32(result);

                if (count > 0)
                {
                    return await Result.FailAsync($"Existem registros na tabela '{r.TabelaDestino.NomeTabela}' que referenciam o registro com o registro que voce esta tentando excluir '{request.IdRegistro}'.");
                }
            }

            var sql = $"DELETE FROM {table.NomeTabela} WHERE {table.CampoPK} = {request.IdRegistro};";
            await _context.Database.ExecuteSqlRawAsync(sql);

            return await Result.SuccessAsync("Registro deletado com sucesso");
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

        public async Task<IResult<DynamicTableRequest>> ConsultaTodosPorIdTabelaAsync(int idTabela)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == idTabela);

            if (table == null)
                return await Result<DynamicTableRequest>.FailAsync("Tela não encontrada.");

            var campos = table.Fields;
            var colunas = campos.Select(c => c.NomeCampoBase).ToList();
            var sql = $"SELECT {string.Join(",", colunas)} FROM {table.NomeTabela};";
            var registros = new List<DynamicDadoRequest>();
            var relacionamentos = _context.Relationships
                .Include(r => r.TabelaDestino).Where(r => r.TabelaOrigemId == table.Id).ToList();

            // Pré-carrega todas as opções de campos estrangeiros em memória
            var opcoesRelacionamentos = new Dictionary<string, List<DynamicOpcaoRequest>>();
            using (var connection = _context.Database.GetDbConnection())
            {
                await connection.OpenAsync();

                foreach (var relacionamento in relacionamentos)
                {
                    if (relacionamento.TabelaDestino != null && !string.IsNullOrWhiteSpace(relacionamento.CampoParaExibicaoRelacionamento))
                    {
                        var sqlOpcoes = $"SELECT {relacionamento.TabelaDestino.CampoPK}, {relacionamento.CampoParaExibicaoRelacionamento} FROM {relacionamento.TabelaDestino.NomeTabela};";
                        using var commandOpcoes = connection.CreateCommand();
                        commandOpcoes.CommandText = sqlOpcoes;
                        using var readerOpcoes = await commandOpcoes.ExecuteReaderAsync();

                        var opcoes = new List<DynamicOpcaoRequest>();
                        while (await readerOpcoes.ReadAsync())
                        {
                            opcoes.Add(new DynamicOpcaoRequest
                            {
                                IdRegistro = Convert.ToInt32(readerOpcoes[relacionamento.TabelaDestino.CampoPK]),
                                ValorExibicao = readerOpcoes[relacionamento.CampoParaExibicaoRelacionamento]?.ToString()
                            });
                        }
                        opcoesRelacionamentos[relacionamento.CampoOrigem] = opcoes;
                    }
                }

                // Agora executa a consulta principal
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var valores = new List<DynamicFieldRequest>();
                    foreach (var campo in campos)
                    {
                        var valorLido = reader[campo.NomeCampoBase]?.ToString();
                        if (campo.Tipo == TypeColumnEnum.BIT)
                        {
                            valorLido = valorLido.Equals("True") ? "1" : "0";
                        }

                        // Busca as opções já carregadas em memória
                        var opcoes = campo.IsForeigeKey && opcoesRelacionamentos.ContainsKey(campo.NomeCampoBase)
                            ? opcoesRelacionamentos[campo.NomeCampoBase].Cast<DynamicOpcaoRequest?>().ToList()
                            : new List<DynamicOpcaoRequest?>();

                        valores.Add(new DynamicFieldRequest
                        {
                            NomeCampoBase = campo.NomeCampoBase,
                            NomeCampoTela = campo.NomeCampoTela,
                            Tipo = campo.Tipo,
                            Tamanho = campo.Tamanho,
                            IsPrimaryKey = campo.IsPrimaryKey,
                            IsForeigeKey = campo.IsForeigeKey,
                            IsObrigatorio = campo.IsObrigatorio,
                            Id = campo.Id,
                            IdTabela = campo.TableId,
                            Valor = valorLido,
                            Opcoes = opcoes
                        });
                    }

                    var pkValue = reader[table.CampoPK]?.ToString();
                    var idRegistro = int.TryParse(pkValue, out var idParsed) ? idParsed : 0;

                    registros.Add(new DynamicDadoRequest
                    {
                        Id = idRegistro,
                        Valores = valores
                    });
                }
            }

            var response = new DynamicTableRequest
            {
                NomeTabela = table.NomeTabela,
                NomeTela = table.NomeTela,
                CampoPK = table.CampoPK,
                Id = table.Id,
                Resultados = registros
            };

            return await Result<DynamicTableRequest>.SuccessAsync(response);
        }


        public async Task<IResult<DynamicTableRequest>> ConsultaRegistroPorIdTabelaEIdRegistroAsync(DynamicForIdRequest request)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == request.IdTable);

            if (table == null)
                return await Result<DynamicTableRequest>.FailAsync("Tela não encontrada.");

            var campos = table.Fields;

            var colunas = campos.Select(c => c.NomeCampoBase).ToList();
            var sql = $"SELECT {string.Join(",", colunas)} FROM {table.NomeTabela} WHERE {table.CampoPK} = {request.IdRegistro};";

            var registros = new List<DynamicDadoRequest>();

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync();
            bool IsExisteRegistro = false;
            while (await reader.ReadAsync())
            {
                var pkValue = reader[table.CampoPK]?.ToString();
                int idConsultado = int.TryParse(pkValue, out var idParsed) ? idParsed : 0;

                if (idConsultado == request.IdRegistro)
                {
                    IsExisteRegistro = true;
                    var valores = new List<DynamicFieldRequest>();

                    foreach (var campo in campos)
                    {
                        var valorLido = reader[campo.NomeCampoBase]?.ToString();
                        if (campo.Tipo == TypeColumnEnum.BIT)
                        {
                            valorLido = valorLido.Equals("True") ? "1" : "0"; // Fix consulta quando o dado em base é um bit
                        }

                        var opcoes = new List<DynamicOpcaoRequest?>();
                        if (campo.IsForeigeKey)
                        {
                            var relacionamento = table.RelacionamentosComoOrigem.FirstOrDefault(r => r.CampoOrigem == campo.NomeCampoBase);

                            if (relacionamento != null)
                            {
                                // Monta a consulta para buscar os valores da tabela relacionada
                                var sqlOpcoes = $@"
                                    SELECT {relacionamento.TabelaDestino.CampoPK}, 
                                    {relacionamento.CampoParaExibicaoRelacionamento} 
                                    FROM {relacionamento.TabelaDestino.NomeTabela};";

                                using var connectionOpcoes = _context.Database.GetDbConnection();
                                await connectionOpcoes.OpenAsync();

                                using var commandOpcoes = connectionOpcoes.CreateCommand();
                                commandOpcoes.CommandText = sqlOpcoes;

                                using var readerOpcoes = await commandOpcoes.ExecuteReaderAsync();
                                while (await readerOpcoes.ReadAsync())
                                {
                                    opcoes.Add(new DynamicOpcaoRequest
                                    {
                                        IdRegistro = Convert.ToInt32(readerOpcoes[relacionamento.TabelaDestino.CampoPK]),
                                        ValorExibicao = readerOpcoes[relacionamento.CampoParaExibicaoRelacionamento]?.ToString()
                                    });
                                }
                            }
                        }
                        valores.Add(new DynamicFieldRequest
                        {
                            NomeCampoBase = campo.NomeCampoBase,
                            NomeCampoTela = campo.NomeCampoTela,
                            Tipo = campo.Tipo,
                            Tamanho = campo.Tamanho,
                            IsPrimaryKey = campo.IsPrimaryKey,
                            IsForeigeKey = campo.IsForeigeKey,
                            IsObrigatorio = campo.IsObrigatorio,
                            Id = campo.Id,
                            IdTabela = campo.TableId,
                            Valor = valorLido,
                            Opcoes = opcoes
                        });
                    }
                    var registro = new DynamicDadoRequest
                    {
                        Id = idConsultado,
                        Valores = valores
                    };
                    registros.Add(registro);
                }
            }

            if (IsExisteRegistro)
            {
                var response = new DynamicTableRequest
                {
                    NomeTabela = table.NomeTabela,
                    NomeTela = table.NomeTela,
                    CampoPK = table.CampoPK,
                    Id = table.Id,
                    Resultados = registros
                };
                return await Result<DynamicTableRequest>.SuccessAsync(response);
            }
            else
            {
                return await Result<DynamicTableRequest>.FailAsync($"Não foi encontrado nenhum registro com o Id '{request.IdRegistro}' informado");
            }
        }

        public async Task<IResult<List<MenuRequest>>> GetMenu()
        {
            var tables = await _context.Tables.Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).ToListAsync();
            var menuRequests = _mapper.Map<List<MenuRequest>>(tables);
            return await Result<List<MenuRequest>>.SuccessAsync(menuRequests);
        }

        public async Task<IResult<DynamicTableRequest>> GetNewObjetoDinamico(int idTabela)
        {
            var table = await _context.Tables
                .Include(t => t.Fields)
                .Include(t => t.RelacionamentosComoOrigem) // Inclui os relacionamentos
                .Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId))
                .FirstOrDefaultAsync(t => t.Id == idTabela);

            if (table == null)
                return await Result<DynamicTableRequest>.FailAsync("Tela não encontrada.");

            var campos = table.Fields;

            DynamicTableRequest retorno = new DynamicTableRequest()
            {
                Id = table.Id,
                NomeTabela = table.NomeTabela,
                NomeTela = table.NomeTela,
                Resultados = new List<DynamicDadoRequest>()
                {
                    new DynamicDadoRequest()
                    {
                        Id = 0,
                        Valores = new List<DynamicFieldRequest>()
                    }
                }
            };

            foreach (var resultado in retorno.Resultados)
            {
                foreach (var campo in campos)
                {
                    if (campo.IsPrimaryKey)
                    {
                        retorno.CampoPK = campo.NomeCampoBase;
                    }
                    else if (campo.IsForeigeKey)
                    {
                        // Busca o relacionamento correspondente
                        var relacionamento = table.RelacionamentosComoOrigem
                            .FirstOrDefault(r => r.CampoOrigem == campo.NomeCampoBase);

                        relacionamento.TabelaOrigem = await _context.Tables.FirstOrDefaultAsync(t => t.Id == relacionamento.TabelaOrigemId);
                        relacionamento.TabelaDestino = await _context.Tables.FirstOrDefaultAsync(t => t.Id == relacionamento.TabelaDestinoId);

                        var opcoes = new List<DynamicOpcaoRequest?>();

                        if (relacionamento != null)
                        {
                            // Monta a consulta para buscar os valores da tabela relacionada
                            var sqlOpcoes = $@"SELECT {relacionamento.TabelaDestino.CampoPK}, {relacionamento.CampoParaExibicaoRelacionamento} FROM {relacionamento.TabelaDestino.NomeTabela};";

                            using var connection = _context.Database.GetDbConnection();
                            await connection.OpenAsync();

                            using var command = connection.CreateCommand();
                            command.CommandText = sqlOpcoes;

                            using var reader = await command.ExecuteReaderAsync();
                            while (await reader.ReadAsync())
                            {
                                opcoes.Add(new DynamicOpcaoRequest
                                {
                                    IdRegistro = Convert.ToInt32(reader[relacionamento.TabelaDestino.CampoPK]),
                                    ValorExibicao = reader[relacionamento.CampoParaExibicaoRelacionamento]?.ToString()
                                });
                            }
                        }

                        // Adiciona o campo com as opções
                        resultado.Valores.Add(new DynamicFieldRequest
                        {
                            NomeCampoBase = campo.NomeCampoBase,
                            NomeCampoTela = campo.NomeCampoTela,
                            Tipo = campo.Tipo,
                            Tamanho = campo.Tamanho,
                            IsPrimaryKey = campo.IsPrimaryKey,
                            IsForeigeKey = campo.IsForeigeKey,
                            IsObrigatorio = campo.IsObrigatorio,
                            Id = campo.Id,
                            IdTabela = table.Id,
                            Valor = string.Empty,
                            Opcoes = opcoes
                        });
                    }
                    else
                    {
                        // Adiciona campos normais sem opções
                        resultado.Valores.Add(new DynamicFieldRequest
                        {
                            NomeCampoBase = campo.NomeCampoBase,
                            NomeCampoTela = campo.NomeCampoTela,
                            Tipo = campo.Tipo,
                            Tamanho = campo.Tamanho,
                            IsPrimaryKey = campo.IsPrimaryKey,
                            IsForeigeKey = campo.IsForeigeKey,
                            IsObrigatorio = campo.IsObrigatorio,
                            Id = campo.Id,
                            IdTabela = table.Id,
                            Valor = string.Empty
                        });
                    }
                }
            }

            return await Result<DynamicTableRequest>.SuccessAsync(retorno);
        }

    }
}
