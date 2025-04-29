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

        public async Task<IResult> CriarTabelaFisicaAsync(Table table)
        {
            try
            {
                //var table = await _context.Tables.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == tableId);

                if (table == null)
                    return await Result.FailAsync("Tabela não encontrada.");

                //string tableName = table.NomeTabela.Replace(" ", "_");

                var columns = table.Fields.Select(f =>
                {
                    string column = $"{f.NomeCampoBase} {f.Tipo}";

                    if (f.Tamanho.HasValue && (f.Tipo.ToString().Equals("VARCHAR", StringComparison.OrdinalIgnoreCase)))
                    {
                        column += $"({f.Tamanho})";
                    }

                    if (f.IsPrimaryKey) // Adicionar validação para caso IsPrimaryKey = true vier em mais de um campo
                        column += " PRIMARY KEY";

                    return column;
                });

                string createTableQuery = $"CREATE TABLE {table.NomeTabela} ({string.Join(", ", columns)});";

                await _context.Database.ExecuteSqlRawAsync(createTableQuery);
                return Result.Success("Tabela criada com sucesso");
            }
            catch (Exception ex)
            {
                _context.Fields.RemoveRange(table.Fields);
                _context.Tables.Remove(table);
                await _context.SaveChangesAsync();
                return await Result.FailAsync(ex.Message);
            }
        }

        public async Task<IResult> InsertAsync(DynamicTableRequest request)
        {
            // Implementar futuramente aqui as permissoes de acesso dada do usuario criador da tabela a outros usuarios registrados
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == request.Id);

            if (table == null)
                return await Result.FailAsync("Tabela não encontrada.");

            foreach (var registro in request.Resultados)
            {
                var columns = new List<string>();
                var values = new List<string>();

                foreach (var field in registro.Valores)
                {
                    var columnName = field.NomeCampoBase;
                    var valor = FormatSqlValue(field.Tipo.ToString(), field.Valor);
                    columns.Add(columnName);
                    values.Add(valor);
                }

                var sql = $"INSERT INTO {table.NomeTabela} ({string.Join(",", columns)}) VALUES ({string.Join(",", values)});";
                await _context.Database.ExecuteSqlRawAsync(sql);
            }

            return await Result.SuccessAsync("Registro incluído com sucesso"); ;
        }
        
        public async Task<IResult> UpdateAsync(DynamicTableRequest request)
        {
            // Implementar futuramente aqui as permissoes de acesso dada do usuario criador da tabela a outros usuarios registrados
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == request.Id);

            if (table == null)
                return await Result.FailAsync("Tabela não encontrada.");

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
        
        public async Task<IResult> DeleteAsync(DynamicTableRequest request)
        {
            // Implementar futuramente aqui as permissoes de acesso dada do usuario criador da tabela a outros usuarios registrados
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == request.Id);

            if (table == null) 
                return await Result.FailAsync("Tabela não encontrada.");

            foreach (var registro in request.Resultados)
            {
                var pkField = registro.Valores.FirstOrDefault(f => f.IsPrimaryKey);
                if (pkField == null)
                    return await Result.FailAsync("Campo chave primária não encontrado.");

                var pkValue = FormatSqlValue(pkField.Tipo.ToString(), pkField.Valor);

                var sql = $"DELETE FROM {table.NomeTabela} WHERE {pkField.NomeCampoBase} = {pkValue};";
                await _context.Database.ExecuteSqlRawAsync(sql);
            }

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

        public async Task<IResult<DynamicTableRequest>> ConsultarDinamicoAsync(int idTabela)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == idTabela);

            if (table == null)
                return await Result<DynamicTableRequest>.FailAsync("Tabela não encontrada.");

            var campos = table.Fields;

            var colunas = campos.Select(c => c.NomeCampoBase).ToList();
            var sql = $"SELECT {string.Join(",", colunas)} FROM {table.NomeTabela};";

            var registros = new List<DynamicDadoRequest>();

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            int idregistro;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var valores = new List<DynamicFieldRequest>();

                foreach (var campo in campos)
                {
                    var valorLido = reader[campo.NomeCampoBase]?.ToString();
                    if (campo.Tipo == TypeColumnEnum.BIT)
                    {
                        valorLido = valorLido.Equals("True") ? "1" : "0"; // Fix consulta quando o dado em base é um bit
                    }
                    valores.Add(new DynamicFieldRequest
                    {
                        NomeCampoBase = campo.NomeCampoBase,
                        NomeCampoTela = campo.NomeCampoTela,
                        Tipo = campo.Tipo,
                        Tamanho = campo.Tamanho,
                        IsPrimaryKey = campo.IsPrimaryKey,
                        IsObrigatorio = campo.IsObrigatorio,
                        Id = campo.Id,
                        IdTabela = campo.TableId,
                        Valor = valorLido
                    });
                }

                var pkValue = reader[table.CampoPK]?.ToString();
                var idRegistro = int.TryParse(pkValue, out var idParsed) ? idParsed : 0;

                var registro = new DynamicDadoRequest
                {
                    Id = idRegistro, // Chat, Preencher aqui o valor do Id do registro definido no campo "CampoPK", lembrando que o id sempre sera int
                    Valores = valores
                };

                registros.Add(registro);
            }

            var response = new DynamicTableRequest
            {
                NomeTabela = table.NomeTabela,
                NomeTela = table.NomeTela,
                CampoPK = table.CampoPK,
                JsonObject = table.JsonObject,
                TelaObject = table.TelaObject,
                Id = table.Id,
                Resultados = registros
            };

            return await Result<DynamicTableRequest>.SuccessAsync(response);
        }

        public async Task<IResult<List<MenuRequest>>> GetMenu()
        {
            var tables = await _context.Tables.Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).ToListAsync();
            var menuRequests = _mapper.Map<List<MenuRequest>>(tables);
            return await Result<List<MenuRequest>>.SuccessAsync(menuRequests);
        }

        public async Task<IResult<DynamicTableRequest>> GetNewObjetoDinamico(int idTabela)
        {
            var table = await _context.Tables.Include(t => t.Fields).Where(u => u.IdUsuario == int.Parse(_currentUserService.UserId)).FirstOrDefaultAsync(t => t.Id == idTabela);

            if (table == null)
                return await Result<DynamicTableRequest>.FailAsync("Tabela não encontrada.");

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
                        retorno.CampoPK = campo.NomeCampoBase;

                    resultado.Valores.Add(new DynamicFieldRequest
                    {
                        NomeCampoBase = campo.NomeCampoBase,
                        NomeCampoTela = campo.NomeCampoTela,
                        Tipo = campo.Tipo,
                        Tamanho = campo.Tamanho,
                        IsPrimaryKey = campo.IsPrimaryKey,
                        IsObrigatorio = campo.IsObrigatorio,
                        Id = campo.Id,
                        IdTabela = table.Id,
                        Valor = string.Empty
                    });
                }
            }
            return await Result<DynamicTableRequest>.SuccessAsync(retorno);
        }
    }
}
