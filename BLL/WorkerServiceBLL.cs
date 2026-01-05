using Common.Entities;
using DAL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;
using Soltec.Common.LoggerFramework;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;

namespace BLL
{
    public class WorkerServiceBLL
    {
        public IConfiguration Configuration;
        public RequestBLL RequestBLL;
        public ServicioConfig ServicioConfig = new ServicioConfig();

        public static string Sucursal;

        public WorkerServiceBLL(IConfiguration configuration, ILogger<WorkerServiceBLL> logger, ILogger<RequestBLL> loggerRequestBLL)
        {
            Configuration = configuration;
            RequestBLL = new RequestBLL(configuration, loggerRequestBLL);
        }

        public static async Task<string> GetSucursal()
        {

            string DbConnection = "Server=localhost;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
            var sucursal = "";

#if DEBUG
                DbConnection = "Server=localhost;Database=SPOSAA;Uid=SPV;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#else
            DbConnection = "Server=localhost\\spos;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
#endif
            try
            {
                sucursal = await WorkerServiceDAL.GetSucursal(DbConnection);
                if (!string.IsNullOrEmpty(sucursal))
                {
                    Logger.Info($"Sucursal obtenida: {sucursal} con la conexion: {DbConnection}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en posible connection: {ex.Message}\n{DbConnection}");
            }

            return sucursal;
        }

        public static async Task<List<string>> GetLocalSqlConnections()
        {
            Logger.Info("Obteniendo posibles connecion SQL dentro de la maquina de la sucursal.");

            try
            {
                var connections = new List<string>();
                var connectionSposaa = "Server=localhost;Database=SPOSAA;Uid=spv;password=:KuHmnNX0;TrustServerCertificate=true;MultipleActiveResultSets=True;";
                connections.Add(connectionSposaa);

                var instacesName = await GetSqlServerInstanceName();

                if (instacesName.Count > 0)
                {
                    foreach (var instanceName in instacesName)
                    {
                        var possibleConnections = $"Server={instanceName};Database=SPOSAA;Uid=spv;Trusted_Connection=yes";
                        connections.Add(possibleConnections);
                    }
                }

                return connections;
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error al obtener las conecciones locales del SQL Server. {ex.Message}");
                throw;
            }
        }

        public static async Task<List<string>> GetSqlServerInstanceName()
        {
            Logger.Info("Obteniendo instancias de SQL Server dentro de la maquina de la sucursal.");

            try
            {
                var lstInstancesName = new List<string>();

                string keyPath = @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL";

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        string[] instances = key.GetValueNames();
                        if (instances.Length > 0)
                        {
                            foreach (var instance in instances)
                            {
                                var firstInstances = $"{Environment.MachineName}\\{instance}";
                                lstInstancesName.Add(firstInstances);
                            }

                            var secondInstance = $"{Environment.MachineName}";
                            lstInstancesName.Add(secondInstance);
                        }
                    }
                }

                return lstInstancesName;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error al obtener intencias dentro de la maquina de la sucursal: {ex.Message}");
                throw;
            }
        }
    }
}
