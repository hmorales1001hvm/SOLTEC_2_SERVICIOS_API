using AppCommon.Entities;
using Dapper;
using Soltec.Common.LoggerFramework;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Linq;
using System.Transactions;

namespace Soltec.WindowsServiceSQLite.Common
{
    public static class Data
    {
        public static string DbConnection = "Server=localhost;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
        public static async Task<object> DataScriptSQLServerToSQLite(SPOS_SQLScripts script)
        {
            var data = new List<object>();

#if DEBUG
            DbConnection = "Server=DESKTOP-6BRAUS9;Database=SPOSAA;Uid=sa;password=Howmanyfingers12345;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#else
            DbConnection = "Server=localhost\\spos;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#endif

            try
            {
                string sqliteConnStr = @"Data Source=C:\Sfspos\Soltec.WindowsServiceSQLite\SQLite\DB\DBSoltec.db;Version=3;BusyTimeout=5000;";

                using (var sqlConn = new SqlConnection(DbConnection))
                using (var sqliteConn = new SQLiteConnection(sqliteConnStr))
                {
                    sqlConn.Open();
                    sqliteConn.Open();

                    if (script.ResetearTablaSQLite == 1)
                    {
                        using (var drop = new SQLiteCommand($"DROP TABLE IF EXISTS {script.Nombre};", sqliteConn))
                        {
                            if (script.Nombre == "VentasImpuestos")
                                drop.ExecuteNonQuery();
                        }
                    }

                    using (var createCmd = new SQLiteCommand(script.ScriptTable, sqliteConn))
                    {
                        try
                        {
                            if (script.Nombre == "VentasImpuestos")
                                createCmd.ExecuteNonQuery();
                        }catch(Exception ex)
                        {
                            Logger.Error(ex);
                        }
                    }

                    using (var transaction = sqliteConn.BeginTransaction())
                    using (var sqlCmd = new SqlCommand(script.SQLScript, sqlConn))
                    using (var reader = sqlCmd.ExecuteReader())
                    {
                        int fieldCount = reader.FieldCount;

                        while (reader.Read())
                        {
                            string columnNames = string.Join(", ", GetColumnNames(reader));
                            string parameterNames = string.Join(", ", GetParameterNames(reader));

                            string insertSql = $"INSERT OR IGNORE INTO {script.Nombre} ({columnNames}) VALUES ({parameterNames})";
                            if (script.Nombre== "VentasImpuestos")
                                Logger.Info(insertSql);

                            using (var insertCmd = new SQLiteCommand(insertSql, sqliteConn))
                            {
                                insertCmd.Parameters.Clear();

                                for (int i = 0; i < fieldCount; i++)
                                {
                                    string paramName = $"@{reader.GetName(i)}";
                                    object value = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                                    insertCmd.Parameters.AddWithValue(paramName, value);
                                }

                                insertCmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"❌ Ocurrió un error al ejecutar el script: {script.SQLScript}.\nError: {ex.Message}");
            }

            return data;
        }


        static string[] GetColumnNames(SqlDataReader reader)
        {
            string[] names = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                names[i] = reader.GetName(i);
            return names;
        }

        static string[] GetParameterNames(SqlDataReader reader)
        {
            string[] names = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                names[i] = "@" + reader.GetName(i);
            return names;
        }



        public static async Task<object> DataScript(SPOS_SQLScripts script)
        {
            var data = new List<object>();
            string sqliteConnStr = @"Data Source=C:\Sfspos\Soltec.WindowsServiceSQLite\SQLite\DB\DBSoltec.db;Version=3;";

            using (var connection = new SQLiteConnection(sqliteConnStr))
            {
                try
                {
                    connection.Open();
                    data = (connection.Query<object>(script.SQLScript, commandTimeout: 10000)).ToList();
                    connection.Close();
                }
                catch (Exception ex)
                {
                    connection.Close();
                    Logger.Error($"Ocurrió un error al ejecutar el script: {script.SQLScript}.\nError: {ex.Message}");
                }
            }

            return data;
        }


        //public static async Task<Dictionary<string, object>> DataScriptMultiple(SPOS_SQLScripts script)
        //{
        //    var data = new List<object>();
        //    string sqliteConnStr = @"Data Source=C:\Sfspos\Soltec.WindowsServiceSQLite\SQLite\DB\DBSoltec.db;Version=3;";

        //    var resultado = new Dictionary<string, object>();
        //    using (var connection = new SQLiteConnection(sqliteConnStr))
        //    {
        //        try
        //        {
        //            connection.Open();
        //            // Extraer etiquetas a partir de los comentarios "--nombre"
        //            var nombres = script.SQLScript.Split(';')
        //                         .Select(b => b.Trim())
        //                         .Where(b => !string.IsNullOrWhiteSpace(b))
        //                         .Select(b =>
        //                         {
        //                             var lines = b.Split('\n');
        //                             var nameLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("--"));
        //                             return nameLine?.Replace("--", "").Trim() ?? $"tabla";
        //                         })
        //                         .ToList();

        //            using (var multi = await connection.QueryMultipleAsync(script.SQLScript, commandTimeout: 10000))
        //            {
        //                int index = 0;

        //                while (!multi.IsConsumed && index < nombres.Count)
        //                {
        //                    var rows = multi.Read().ToList();
        //                    resultado[nombres[index]] = rows;
        //                    index++;
        //                }
        //            }
        //            connection.Close();
        //        } catch(Exception ex)
        //        {
        //            Logger.Error($"Ocurrió un error al ejecutar el query Multiple. Error. {ex.Message}");
        //        }
        //    }
        //    return resultado;
        //}

        public static async Task<Dictionary<string, object>> DataScriptMultiple(SPOS_SQLScripts script)
        {
            var resultado = new Dictionary<string, object>();
            string sqliteConnStr = @"Data Source=C:\Sfspos\Soltec.WindowsServiceSQLite\SQLite\DB\DBSoltec.db;Version=3;";

            var bloquesSQL = script.SQLScript
                .Split(';')
                .Select(b => b.Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();

            using (var connection = new SQLiteConnection(sqliteConnStr))
            {
                try
                {
                    connection.Open();

                    for (int i = 0; i < bloquesSQL.Count; i++)
                    {
                        var bloque = bloquesSQL[i];

                        // Dividir por líneas
                        var lineas = bloque.Split('\n');

                        // Buscar comentario tipo "--nombre"
                        string nombre = null;
                        foreach (var linea in lineas)
                        {
                            var l = linea.Trim();
                            if (l.StartsWith("--"))
                            {
                                nombre = l.Replace("--", "").Trim();
                                break;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(nombre))
                        {
                            nombre = $"tabla{i + 1}";
                        }

                        var consultaSQL = string.Join("\n",lineas.Where(l => !string.IsNullOrWhiteSpace(l)) // evita nulls y líneas vacías
                                                 .Select(l =>
                                                 {
                                                     string line = l.Trim();

                                                     // Si la línea es un comentario completo, ignórala
                                                     if (line.StartsWith("--"))
                                                         return string.Empty;

                                                     // Si tiene comentario en línea, cortar antes de --
                                                     int index = line.IndexOf("--");
                                                     if (index >= 0)
                                                         line = line.Substring(0, index).Trim();

                                                     return line;
                                                 })
                                                 .Where(l => !string.IsNullOrWhiteSpace(l)) // eliminar líneas vacías post-trimming
                                         ).Trim();

                        if (!string.IsNullOrWhiteSpace(consultaSQL))
                        {
                            var filas = (await connection.QueryAsync(consultaSQL, commandTimeout: 10000)).ToList();
                            resultado[nombre] = filas;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] DataScriptMultiple: {ex.Message}");
                }
            }

            return resultado;
        }

        public static async Task<Dictionary<string, object>> DataScriptMultipleSQLServer(SPOS_SQLScripts script, string sucursal="")
        {
            var resultado = new Dictionary<string, object>();
            //string sqliteConnStr = @"Data Source=C:\Sfspos\Soltec.WindowsServiceSQLite\SQLite\DB\DBSoltec.db;Version=3;";
            #if DEBUG
                DbConnection = "Server=localhost;Database=SPOSAA;Uid=SPV;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
            #else
                DbConnection = "Server=localhost\\spos;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#endif
            var consultaSQL = "";
            var bloquesSQL = script.SQLScript
                .Split(';')
                .Select(b => b.Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();

            using (var connection = new SqlConnection(DbConnection))
            {
                try
                {
                    connection.Open();

                    for (int i = 0; i < bloquesSQL.Count; i++)
                    {
                        var bloque = bloquesSQL[i];

                        // Dividir por líneas
                        var lineas = bloque.Split('\n');

                        // Buscar comentario tipo "--nombre"
                        string nombre = null;
                        foreach (var linea in lineas)
                        {
                            var l = linea.Trim();
                            if (l.StartsWith("--"))
                            {
                                nombre = l.Replace("--", "").Trim();
                                break;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(nombre))
                        {
                            nombre = $"tabla{i + 1}";
                        }

                        consultaSQL = string.Join("\n", lineas.Where(l => !string.IsNullOrWhiteSpace(l)) // evita nulls y líneas vacías
                                                 .Select(l =>
                                                 {
                                                     string line = l.Trim();

                                                     // Si la línea es un comentario completo, ignórala
                                                     if (line.StartsWith("--"))
                                                         return string.Empty;

                                                     // Si tiene comentario en línea, cortar antes de --
                                                     int index = line.IndexOf("--");
                                                     if (index >= 0)
                                                         line = line.Substring(0, index).Trim();

                                                     return line;
                                                 })
                                                 .Where(l => !string.IsNullOrWhiteSpace(l)) // eliminar líneas vacías post-trimming
                                         ).Trim();

                        if (!string.IsNullOrWhiteSpace(consultaSQL))
                        { 
                            //if (!string.IsNullOrEmpty(sucursal))
                            //{
                            //    if (sucursal == "7146")
                            //    {
                            //        Logger.Info($"\n\n{consultaSQL}");
                            //    }
                            //}
                            var filas = (await connection.QueryAsync(consultaSQL, commandTimeout: 1200)).ToList();
                            resultado[nombre] = filas;
                        }
                    }
                }
                catch (Exception ex)
                {
                    
                    Logger.Error($"Ocurrió un error al consultar su información: {ex.Message}, Query: {consultaSQL}");
                    throw new Exception($"Ocurrió un error al consultar su información: {ex.Message}, Query: {consultaSQL}");
                }
            }

            return resultado;
        }




        public static async Task<object> UpdteDataSend(string nombre, object data, bool multiplesTablas)
        {
            string dbPath = @"C:\Sfspos\Soltec.WindowsServiceSQLite\SQLite\DB\DBSoltec.db";
            string sqliteConnStr = $"Data Source={dbPath};Version=3;";
            string tablaDestino = nombre;
            string campoActualizar = "Enviado";
            object valorActualizar = 1;

            int maxRetries = 3;
            int delayMs = 1000;
            int retryCount = 0;

            while (retryCount <= maxRetries)
            {
                using (var connection = new SQLiteConnection(sqliteConnStr))
                {
                    try
                    {
                        connection.Open();
                        var lista = new List<object>();

                        lista = (data as IEnumerable<object>)?.ToList();

                        if (lista == null || !lista.Any())
                        {
                            Console.WriteLine("⚠️ No se encontraron registros.");
                            return data;
                        }

                        // Obtener claves primarias automáticamente
                        var columnasLlave = connection.Query("PRAGMA table_info(" + tablaDestino + ");")
                            .Where(r => Convert.ToInt32(r.pk) > 0)
                            .Select(r => (string)r.name)
                            .ToList();

                        if (!columnasLlave.Any())
                            throw new Exception($"❌ La tabla '{tablaDestino}' no tiene claves primarias definidas.");

                        using (var transaction = connection.BeginTransaction())
                        {
                            foreach (var row in lista)
                            {
                                IDictionary<string, object> dict;

                                if (row is IDictionary<string, object> dic)
                                {
                                    dict = dic;
                                }
                                else
                                {
                                    dict = row.GetType()
                                              .GetProperties()
                                              .ToDictionary(p => p.Name, p => p.GetValue(row));
                                }

                                var whereClause = string.Join(" AND ", columnasLlave.Select(c => $"{c} = @{c}"));
                                string sql = $@"
                            UPDATE {tablaDestino}
                            SET {campoActualizar} = @valor
                            WHERE {whereClause};";

                                var parameters = new DynamicParameters();
                                parameters.Add("valor", valorActualizar);

                                foreach (var key in columnasLlave)
                                {
                                    if (!dict.ContainsKey(key))
                                        throw new Exception($"❌ El resultado no contiene la columna llave '{key}'.");

                                    parameters.Add(key, dict[key]);
                                }

                                connection.Execute(sql, parameters, transaction);
                            }

                            transaction.Commit();
                        }

                        Console.WriteLine($"✅ Se actualizaron {lista.Count} registros en '{tablaDestino}' (columna '{campoActualizar}').");
                        return data;
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("database is locked"))
                        {
                            retryCount++;
                            if (retryCount > maxRetries)
                            {
                                Logger.Error($"❌ Base de datos bloqueada después de {maxRetries} intentos.");
                                break;
                            }

                            Logger.Warning($"⚠️ Intento {retryCount}: la base está bloqueada. Reintentando en {delayMs}ms...");
                            await Task.Delay(delayMs);
                            delayMs *= 2;
                        }
                        else
                        {
                            Logger.Error($"❌ Error inesperado al actualizar '{tablaDestino}': {ex.Message}");
                            break;
                        }
                    }
                    finally
                    {
                        connection.Close();
                    }
                }
            }

            return data;
        }





    }
}
