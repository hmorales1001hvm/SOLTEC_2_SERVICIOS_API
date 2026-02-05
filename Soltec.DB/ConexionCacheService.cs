using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySql.Data.MySqlClient;
using Soltec.Common.Logger;
using Soltec.DB;
using System.Collections.Concurrent;
using System.Configuration;

public class ConexionCacheService : BackgroundService
{
    private readonly string _mysqlConnection;
    private static ConcurrentDictionary<string, ConexionEmpresa> _cacheConexiones = new ConcurrentDictionary<string, ConexionEmpresa>();
    private readonly int _cacheUpdateMinutes;
    public ConexionCacheService(IConfiguration configuration)
    {
        _mysqlConnection = configuration.GetConnectionString("DbFacturaRealOrquestador");
        if (!int.TryParse(configuration["settingsAPIs:TimeUpdateCacheService"], out _cacheUpdateMinutes))
        {
            _cacheUpdateMinutes = 10;
        }
    }

    //public static bool TryGetConexion(string claveSimi, out ConexionEmpresa conexion)
    //{
    //    return _cacheConexiones.TryGetValue(claveSimi, out conexion);
    //}

    public static bool TryGetConexion(string claveSimi, out ConexionEmpresa conexion)
    {
        var key = claveSimi?.Trim().ToUpper();

        if (string.IsNullOrEmpty(key))
        {
            conexion = null;
            return false;
        }

        return _cacheConexiones.TryGetValue(key, out conexion);
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        
        await ActualizarCache(); 

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(_cacheUpdateMinutes), stoppingToken);
            await ActualizarCache();
        }
    }


    private async Task ActualizarCache()
    {
        var nuevoCache = new ConcurrentDictionary<string, ConexionEmpresa>();

        try
        {
            using var conn = new MySqlConnection(_mysqlConnection);
            await conn.OpenAsync();

            const string sql = @"SELECT a.HostName, a.UserName, a.Password, a.DatabaseName, s.claveSimi, b.idEmpresa
                                 FROM soltec2_orquestador_servidormysql_detalle a
                                 INNER JOIN catempresa b ON a.IdEmpresa = b.idEmpresa
                                 INNER JOIN sucursal s ON b.idEmpresa = s.idEmpresa";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            int hostNameIdx = reader.GetOrdinal("HostName");
            int userNameIdx = reader.GetOrdinal("UserName");
            int passwordIdx = reader.GetOrdinal("Password");
            int databaseIdx = reader.GetOrdinal("DatabaseName");
            int claveSimiIdx = reader.GetOrdinal("claveSimi");
            int idEmpresax = reader.GetOrdinal("idEmpresa");

            while (await reader.ReadAsync())
            {
                var conexion = new ConexionEmpresa
                {
                    HostName = reader.IsDBNull(hostNameIdx) ? string.Empty : reader.GetString(hostNameIdx),
                    UserName = reader.IsDBNull(userNameIdx) ? string.Empty : reader.GetString(userNameIdx),
                    Password = reader.IsDBNull(passwordIdx) ? string.Empty : reader.GetString(passwordIdx),
                    DatabaseName = reader.IsDBNull(databaseIdx) ? string.Empty : reader.GetString(databaseIdx),
                    IdEmpresa = reader.IsDBNull(idEmpresax) ? 0 : reader.GetInt32(idEmpresax)

                };

                //var claveSimi = reader.IsDBNull(claveSimiIdx)
                //    ? string.Empty
                //    : reader.GetString(claveSimiIdx);

                var claveSimi = reader.IsDBNull(claveSimiIdx)
                    ? string.Empty
                    : reader.GetString(claveSimiIdx).Trim().ToUpper();

                if (!string.IsNullOrWhiteSpace(claveSimi))
                {
                    nuevoCache[claveSimi] = conexion;
                }
            }

            // Swap atómico del cache
            Interlocked.Exchange(ref _cacheConexiones, nuevoCache);

            Logger.Info($"Cache de conexiones actualizado. Total registros: {nuevoCache.Count}");
        }
        catch (Exception ex)
        {
            // MUY IMPORTANTE: nunca romper el servicio
            Logger.Error(ex.Message, "Error al actualizar el cache de conexiones MySQL");
            // El cache anterior permanece activo
        }
    }

}
