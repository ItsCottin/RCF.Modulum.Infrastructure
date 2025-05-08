using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace modulum.Infrastructure.Contexts
{
    public static class DbContextExtensions
    {
        public static async Task<T> ExecuteScalarAsync<T>(this DatabaseFacade database, string sql)
        {
            using var command = database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            if (command.Connection.State != ConnectionState.Open)
                await command.Connection.OpenAsync();

            var result = await command.ExecuteScalarAsync();
            return (result == null || result == DBNull.Value) ? default! : (T)Convert.ChangeType(result, typeof(T))!;
        }
    }
}
