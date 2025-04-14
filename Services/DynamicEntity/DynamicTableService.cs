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

namespace modulum.Infrastructure.Services.DynamicEntity
{
    public class DynamicTableService : IDynamicTableService
    {
        private readonly ModulumContext _context;

        public DynamicTableService(ModulumContext context)
        {
            _context = context;
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
                return await Result.FailAsync(ex.Message);
            }
        }

        public async Task<IResult> InsertAsync(DynamicTableRequest request)
        {
            var table = await _context.Tables.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == request.Id);

            if (table == null) throw new Exception("Tabela não encontrada.");

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
            var table = await _context.Tables.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == request.Id);

            if (table == null) throw new Exception("Tabela não encontrada.");

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

            return await Result.SuccessAsync("Registro alterado com sucesso"); ;
        }
        
        public async Task<IResult> DeleteAsync(DynamicTableRequest request)
        {
            var table = await _context.Tables.Include(t => t.Fields).FirstOrDefaultAsync(t => t.Id == request.Id);

            if (table == null) throw new Exception("Tabela não encontrada.");

            foreach (var registro in request.Resultados)
            {
                var pkField = registro.Valores.FirstOrDefault(f => f.IsPrimaryKey);
                if (pkField == null) throw new Exception("Campo chave primária não encontrado.");

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
                "bigint" or "long" => valor.ToString(),
                _ => $"'{valor}'"
            };
        }

        public async Task<IResult<DynamicTableRequest>> ConsultarDinamicoAsync(int idTabela)
        {
            var table = await _context.Tables
                .Include(t => t.Fields)
                .FirstOrDefaultAsync(t => t.Id == idTabela);

            if (table == null) throw new Exception("Tabela não encontrada.");

            var campos = table.Fields;

            var colunas = campos.Select(c => c.NomeCampoBase).ToList();
            var sql = $"SELECT {string.Join(",", colunas)} FROM {table.NomeTabela};";

            var registros = new List<DynamicDadoRequest>();

            using var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var valores = new List<DynamicFieldRequest>();

                foreach (var campo in campos)
                {
                    var valorLido = reader[campo.NomeCampoBase]?.ToString();

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

                var registro = new DynamicDadoRequest
                {
                    Id = 0, // Resgatar o Id do registro aqui 
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
    }
}
