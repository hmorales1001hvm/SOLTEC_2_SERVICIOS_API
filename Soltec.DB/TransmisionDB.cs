using Soltec.Entities.Transmision;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using System.Data;
using System;
using Soltec.Common.Logger;
using Microsoft.Extensions.Logging;

namespace Soltec.DB
{
    public class TransmisionDB
    {
        private readonly IConfiguration Configuration;
        private readonly ILogger<TransmisionDB> Logger;
        public TransmisionDB(IConfiguration configuration, ILogger<TransmisionDB> logger)
        {
            Configuration = configuration;
            Logger = logger;
        }
               
       
        class SPOS_EnLinea()
        {
            public string ClaveSimi  { get; set; }
        }


        public async Task MarkOnLine(MarkOnlineBody markOnlineBody)
        {
            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaRealOrquestador").Value;

            try
            {
                using var connection = new MySqlConnection(dbConnection);
                await connection.OpenAsync();

                var parameter = new DynamicParameters();
                parameter.Add("@pSucursal", markOnlineBody.Sucursal, DbType.String);
                parameter.Add("@pVersion1", markOnlineBody.Version1, DbType.String);
                parameter.Add("@pVersion2", markOnlineBody.Version2, DbType.String);
                parameter.Add("@pVersion3", markOnlineBody.Version3, DbType.String);
                parameter.Add("@pVersion4", markOnlineBody.Version4, DbType.String);
                parameter.Add("@pVersion5", markOnlineBody.Version5, DbType.String);
                parameter.Add("@pVersion6", markOnlineBody.Version6, DbType.String);

                await connection.ExecuteAsync(
                    "usp_ActualizaSucursalesEnLinea",
                    parameter,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 600);
                await connection.CloseAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ocurrió un error al procesar: usp_ActualizaSucursalesEnLinea.\n{ex.Message}");
            }
        }


        public async Task<IEnumerable<ServicioProcesos>> GetProcesos()
        {
            var query = "SELECT Id, PathDirectory, NombreProcesoActualizador, NombreProcesoEjecucion FROM Enlinea_Servicio_Procesos;";
            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaReal").Value;

            try
            {
                //using var connection = new MySqlConnection(dbConnection);
                //await connection.OpenAsync();

                //var servicios = await connection.QueryAsync<ServicioProcesos>(
                //    query,
                //    commandTimeout: 600
                //);
                //await connection.CloseAsync();

                //return servicios;
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ocurrió un error al procesar: GetProcesos.\n{ex.Message}");
                return Enumerable.Empty<ServicioProcesos>();
            }
        }


        public async Task<IEnumerable<VersionesApp>> GetVersionesApp()
        {
            var query = @"SELECT Id, NombreSistema, VersionSistema, PathArchivoConfig, 
                          NombrePaquete, PathDestinoPaquete, PathArchivoEXE 
                          FROM enlinea_versiones_apps WHERE Activo = 0;";

            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaRealOrquestador").Value;

            try
            {
                var connection = new MySqlConnection(dbConnection);
                await connection.OpenAsync();

                var getVersiones = await connection.QueryAsync<VersionesApp>(
                    query,
                    commandTimeout: 600
                );
                Logger.LogInformation($"Total de versiones encontradas: {getVersiones.Count()}");
                await connection.CloseAsync();
                return getVersiones;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ocurrió un error al obtener las versiones: {query}\n{ex.Message}");
                return Enumerable.Empty<VersionesApp>();
            }
        }


        public async Task<IEnumerable<VersionesApp>> ObtieneVersiones()
        {
            var query = @"SELECT    Id, 
                                    NombreSistema, 
                                    VersionSistema, 
                                    PathArchivoConfig, 
                                    NombrePaquete, 
                                    PathDestinoPaquete, 
                                    PathArchivoEXE 
                          FROM enlinea_versiones_apps WHERE Activo = 1;";

            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaRealOrquestador").Value;

            try
            {
                using var connection = new MySqlConnection(dbConnection);
                await connection.OpenAsync();

                var getVersiones = await connection.QueryAsync<VersionesApp>(
                    query,
                    commandTimeout: 600
                );
                Logger.LogInformation($"Total de versiones encontradas: {getVersiones.Count()}");
                await connection.CloseAsync();
                return getVersiones;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ocurrió un error al obtener las versiones: {query}\n{ex.Message}");
                return Enumerable.Empty<VersionesApp>();
            }
        }


        public async Task<IEnumerable<VersionesApp>> ObtieneVersiones_SimiPET()
        {
            var query = @"SELECT    Id, 
                                    NombreSistema, 
                                    VersionSistema, 
                                    PathArchivoConfig, 
                                    NombrePaquete, 
                                    PathDestinoPaquete, 
                                    PathArchivoEXE 
                          FROM enlinea_versiones_apps WHERE Activo = 1;";

            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbSimiPET").Value;

            try
            {
                using var connection = new MySqlConnection(dbConnection);
                await connection.OpenAsync();

                var getVersiones = await connection.QueryAsync<VersionesApp>(
                    query,
                    commandTimeout: 600
                );
                Logger.LogInformation($"Total de versiones encontradas: {getVersiones.Count()}");
                await connection.CloseAsync();
                return getVersiones;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ocurrió un error al obtener las versiones: {query}\n{ex.Message}");
                return Enumerable.Empty<VersionesApp>();
            }
        }


        public async Task<IEnumerable<MonitorDeApps>> GetMonitorDeApps()
        {
            var query = "SELECT * FROM MonitorDeApps WHERE Activo = 1";

            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaRealOrquestador").Value;

            try
            {
                using var connection = new MySqlConnection(dbConnection);
                await connection.OpenAsync();

                var getVersiones = await connection.QueryAsync<MonitorDeApps>(
                    query,
                    commandTimeout: 600
                );
                
                await connection.CloseAsync();
                return getVersiones;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ocurrió un error al obtener las versiones: {query}\n{ex.Message}");
                return Enumerable.Empty<MonitorDeApps>();
            }
        }

    }
}
