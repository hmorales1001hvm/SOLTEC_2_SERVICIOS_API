using ApiCommon.Entities.Transmision;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using System.Data;
using System;
using Soltec.Common.Logger;
using Microsoft.Extensions.Logging;

namespace ApiDAL
{
    public class TransmisionDAL
    {
        private readonly IConfiguration Configuration;
        private readonly ILogger<TransmisionDAL> Logger;
        public TransmisionDAL(IConfiguration configuration, ILogger<TransmisionDAL> logger)
        {
            Configuration = configuration;
            Logger = logger;
        }

        
        public async Task<ServicioConfig> GetConfiguracion(string sucursal)
        {
            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaRealOrquestador").Value;

            var queryExistSucursal = "SELECT ClaveSimi FROM enlinea_servicio_configuracion WHERE ClaveSimi = @Sucursal;";
            var queryRegistraSucursal = "INSERT INTO enlinea_servicio_configuracion (ClaveSimi) VALUES (@Sucursal);";
            var query = @"SELECT    A.Id, A.ClaveSimi, A.TiempoVerificaProceso, A.TiempoEjecutaApp, 
                                    A.TiempoEsperaError, A.HabilitaLog 
                          FROM enlinea_servicio_configuracion A 
                          INNER JOIN sucursal B ON A.ClaveSimi = B.claveSimi 
                          INNER JOIN catempresa C ON B.idEmpresa = C.idEmpresa 
                  WHERE A.ClaveSimi = @Sucursal;";

            try
            {
                using var connection = new MySqlConnection(dbConnection);
                await connection.OpenAsync();

                var existSucursal = await connection.QueryFirstOrDefaultAsync<string>(
                    queryExistSucursal, new { Sucursal = sucursal }, commandTimeout: 600);

                if (string.IsNullOrEmpty(existSucursal))
                {
                    await connection.ExecuteAsync(queryRegistraSucursal, new { Sucursal = sucursal }, commandTimeout: 600);
                }

                var serviceConfig = await connection.QueryFirstOrDefaultAsync<ServicioConfig>(
                    query, new { Sucursal = sucursal }, commandTimeout: 600);

                return serviceConfig ?? new ServicioConfig(); // Previene retorno nulo
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ocurrió un error al procesar configuración de sucursal '{sucursal}': {ex.Message}");
                return new ServicioConfig(); // Devuelve objeto vacío en caso de error
            }
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
