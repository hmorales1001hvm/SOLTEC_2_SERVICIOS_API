using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using AppCommon.Entities;
using Dapper;
using AppCommon.Venta;
using Microsoft.Extensions.Logging;
using Soltec.Common.LoggerFramework;
using System.Xml;
using System.Collections;

namespace AppDAL
{
	public class SoltecAppDAL
	{
		private readonly IConfiguration Configuration;

		public static string DbConnection = "Server=localhost;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";

		//		public SoltecAppDAL(IConfiguration configuration)
		//        {
		//            Configuration = configuration;
		//#if DEBUG
		//            DbConnection = "Server=DESKTOP-6BRAUS9;Database=SPOSAA;Uid=sa;password=Howmanyfingers12345;TrustServerCertificate=true;MultipleActiveResultSets=True;";
		//#else
		//            DbConnection = "Server=localhost;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
		//#endif
		//        }




		public async Task<GetVentaSucursal> GetVentaSucursal(string scriptTexto)
		{
			var getVentaSucursal = new GetVentaSucursal();


			DbConnection = "Server=localhost;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
			using (var connection = new SqlConnection(DbConnection))
			{
				try
				{
					connection.Open();
					getVentaSucursal = connection.QueryFirstOrDefault<GetVentaSucursal>(scriptTexto, commandTimeout: 420);
					connection.Close();
				}
				catch (Exception ex)
				{
					connection.Close();
					Logger.Error($"Ocurrió un error al ejecutar el script: {scriptTexto}.\nError: {ex.Message}");
				}
			}



			return getVentaSucursal;
		}

		public static async Task<object> DataScript(string sqlScript, int valorIncrementoDecremento)
		{
			var data = new List<object>();

			//DbConnection = "Server=localhost;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
            //DbConnection = "Server=localhost\\spos;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";

			#if DEBUG
						DbConnection = "Server=DESKTOP-6BRAUS9;Database=SPOSAA;Uid=sa;password=Howmanyfingers12345;TrustServerCertificate=true;MultipleActiveResultSets=True;";
			#else
									DbConnection = "Server=localhost\\spos;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
			#endif


			if (valorIncrementoDecremento != 0)
				sqlScript = sqlScript.Replace("{ValorIncrementoDecremento}", valorIncrementoDecremento.ToString());

			using (var connection = new SqlConnection(DbConnection))
			{
				try
				{
					connection.Open();
					data = (connection.Query<object>(sqlScript, commandTimeout: 10000)).ToList();
					connection.Close();
				}
				catch (Exception ex)
				{
					connection.Close();
					Logger.Error($"Ocurrió un error al ejecutar el script: {sqlScript}.\nError: {ex.Message}");
				}
			}

			return data;
        }



        //        public static async Task<Dictionary<string, object>> DataScriptJSON(string sqlScript, int valorIncrementoDecremento)
        //        {
        //#if DEBUG
        //            DbConnection = "Server=DESKTOP-6BRAUS9;Database=SPOSAA;Uid=sa;password=Howmanyfingers12345;TrustServerCertificate=true;MultipleActiveResultSets=True;";
        //#else
        //									DbConnection = "Server=localhost\\spos;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
        //#endif


        //            if (valorIncrementoDecremento != 0)
        //                sqlScript = sqlScript.Replace("{ValorIncrementoDecremento}", valorIncrementoDecremento.ToString());

        //            var resultado = new Dictionary<string, object>();
        //            using (var connection = new SqlConnection(DbConnection))
        //            {
        //                // Extraer etiquetas a partir de los comentarios "--nombre"
        //                var nombres = sqlScript.Split(';')
        //                                 .Select(b => b.Trim())
        //                                 .Where(b => !string.IsNullOrWhiteSpace(b))
        //                                 .Select(b =>
        //                                 {
        //                                     var lines = b.Split('\n');
        //                                     var nameLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("--"));
        //                                     return nameLine?.Replace("--", "").Trim() ?? $"tabla";
        //                                 })
        //                                 .ToList();

        //                using (var multi = connection.QueryMultiple(sqlScript, commandTimeout: 10000))
        //                {
        //                    int index = 0;

        //                    while (!multi.IsConsumed && index < nombres.Count)
        //                    {
        //                        var rows = multi.Read().ToList(); // dinámico
        //                        resultado[nombres[index]] = rows;
        //                        index++;
        //                    }
        //                }
        //            }

        //            return resultado;

        //        }




        public static async Task<Dictionary<string, object>> DataScriptMultipleSQLServer(SPOS_SQLScripts script)
        {
            var resultado = new Dictionary<string, object>();
#if DEBUG
                DbConnection = "Server=localhost;Database=SPOSAA;Uid=SPV;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#else
            DbConnection = "Server=localhost\\spos;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#endif

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

                        var lineas = bloque.Split('\n');

                        
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

                        var consultaSQL = string.Join("\n", lineas.Where(l => !string.IsNullOrWhiteSpace(l)) 
                                                 .Select(l =>
                                                 {
                                                     string line = l.Trim();

                                                     if (line.StartsWith("--"))
                                                         return string.Empty;

                                                     int index = line.IndexOf("--");
                                                     if (index >= 0)
                                                         line = line.Substring(0, index).Trim();

                                                     return line;
                                                 })
                                                 .Where(l => !string.IsNullOrWhiteSpace(l)) 
                                         ).Trim();

                        if (!string.IsNullOrWhiteSpace(consultaSQL))
                        {
                            var filas = (await connection.QueryAsync(consultaSQL, commandTimeout: 420)).ToList();
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



        public async Task<List<VentaGpoConPremio>> GetVentaGpoConPremio(string scriptTexto)
		{
			var fechaActual = DateTime.Now.ToString("yyyyMMdd");

			DbConnection = "Server=localhost;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
			var getVentaSucursal = new List<VentaGpoConPremio>();


			using (var connection = new SqlConnection(DbConnection))
			{
				try
				{
					connection.Open();

					var parameters = new DynamicParameters();
					parameters.Add("@FechaInicial", fechaActual, DbType.String);
					parameters.Add("@FechaFinal", fechaActual, DbType.String);
					parameters.Add("@Caja", 0, DbType.Int32);
					parameters.Add("@Opcion1", 1, DbType.Int32);
					parameters.Add("@Opcion2", 0, DbType.Int32);

					getVentaSucursal = connection.Query<VentaGpoConPremio>(scriptTexto, parameters, commandType: CommandType.StoredProcedure, commandTimeout: 420).ToList();
					connection.Close();
				}
				catch (Exception ex)
				{
					connection.Close();
					Logger.Error($"Ocurrió un error al ejecutar el script: {scriptTexto}.\nError: {ex.Message}");
				}
			}
			return getVentaSucursal;
		}

	}
}
