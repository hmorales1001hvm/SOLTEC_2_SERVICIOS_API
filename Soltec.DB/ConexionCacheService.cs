using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySql.Data.MySqlClient;
using Soltec.Common.Logger;
using Soltec.DB;
using System.Collections.Concurrent;
using System.Data;

public class ConexionCacheService : BackgroundService
{
    private readonly string _mysqlConnection;
    private static ConcurrentDictionary<string, ConexionEmpresa> _cacheConexiones = new();
    private readonly int _cacheUpdateMinutes;
    private static bool _cacheInicializado = false;

    public ConexionCacheService(IConfiguration configuration)
    {
        _mysqlConnection = configuration.GetConnectionString("DbFacturaRealOrquestador");
        if (!int.TryParse(configuration["settingsAPIs:TimeUpdateCacheService"], out _cacheUpdateMinutes))
        {
            _cacheUpdateMinutes = 10;
        }

        Logger.Info($"[Inicio] ConexionCacheService inicializado. Intervalo de actualización: {_cacheUpdateMinutes} minutos");
    }


    public static bool TryGetConexion(string claveSimi, out ConexionEmpresa conexion)
    {
        var start = DateTime.UtcNow;
        conexion = null;

        if (!_cacheInicializado)
        {
            Logger.Warning($"[TryGetConexion] Cache aún no inicializado. Clave solicitada: '{claveSimi}'");
            return false;
        }

        if (string.IsNullOrWhiteSpace(claveSimi))
        {
            Logger.Warning("[TryGetConexion] claveSimi es null o vacía");
            return false;
        }

        // Normalizamos la clave
        var key = claveSimi.Trim().ToUpper();

        Logger.Warning($"[TryGetConexion] SERVER={Environment.MachineName} CacheCount={_cacheConexiones.Count}");


        bool found = _cacheConexiones.TryGetValue(key, out conexion);
        Logger.Info($"Total en cache despues de realizar la búsqueda: {_cacheConexiones.Count}, Sucursal: {key}");

        if (found)
        {
            Logger.Info($"[TryGetConexion] ClaveSimi encontrada: '{key}', IdEmpresa={conexion.IdEmpresa}, DB={conexion.DatabaseName}, CacheCount={_cacheConexiones.Count}, Tiempo={DateTime.UtcNow - start}");
        }
        else
        {
            Logger.Warning($"[TryGetConexion] NO se encontró ClaveSimi '{key}' en cache. CacheCount={_cacheConexiones.Count}, Tiempo={DateTime.UtcNow - start}");
            // Opcional: listar las claves actuales para depuración
            Logger.Debug("[TryGetConexion] Claves actuales en cache: " + string.Join(", ", _cacheConexiones.Keys));
        }

        return found;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Info("[ExecuteAsync] Servicio arrancando...");

        await ActualizarCache(); // Primer llenado del cache
        Logger.Info($"[ExecuteAsync] Cache inicial cargado. Total registros: {_cacheConexiones.Count}");

        while (!stoppingToken.IsCancellationRequested)
        {
            Logger.Info($"[ExecuteAsync] Esperando {_cacheUpdateMinutes} minutos para próxima actualización...");
            await Task.Delay(TimeSpan.FromMinutes(_cacheUpdateMinutes), stoppingToken);

            Logger.Info("[ExecuteAsync] Iniciando actualización de cache...");
            await ActualizarCache();
        }
    }

    private async Task ActualizarCache()
    {
        var nuevoCache = new ConcurrentDictionary<string, ConexionEmpresa>();

        try
        {
            Logger.Info("[ActualizarCache] Conectando a MySQL...");
            using var conn = new MySqlConnection(_mysqlConnection);
            await conn.OpenAsync();
            Logger.Info("[ActualizarCache] Conexión MySQL abierta.");

            const string sql = @"
                SELECT a.HostName, a.UserName, a.Password, a.DatabaseName, s.claveSimi, b.idEmpresa
                FROM soltec2_orquestador_servidormysql_detalle a
                INNER JOIN catempresa b ON a.IdEmpresa = b.idEmpresa
                INNER JOIN sucursal s ON b.idEmpresa = s.idEmpresa";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var conexion = new ConexionEmpresa
                {
                    HostName = reader.IsDBNull("HostName") ? string.Empty : reader.GetString("HostName"),
                    UserName = reader.IsDBNull("UserName") ? string.Empty : reader.GetString("UserName"),
                    Password = reader.IsDBNull("Password") ? string.Empty : reader.GetString("Password"),
                    DatabaseName = reader.IsDBNull("DatabaseName") ? string.Empty : reader.GetString("DatabaseName"),
                    IdEmpresa = reader.IsDBNull("idEmpresa") ? 0 : reader.GetInt32("idEmpresa")
                };

                var claveSimi = reader.IsDBNull("claveSimi") ? string.Empty : reader.GetString("claveSimi").Trim().ToUpper();

                if (!string.IsNullOrWhiteSpace(claveSimi))
                {
                    nuevoCache[claveSimi] = conexion;
                }
            }

            Interlocked.Exchange(ref _cacheConexiones, nuevoCache);
            _cacheInicializado = true; // <-- Marcar cache como listo
            Logger.Warning($"[ActualizarCache] SERVER={Environment.MachineName} Cache cargado con {_cacheConexiones.Count} registros");
  
        }
        catch (Exception ex)
        {
            Logger.Error($"[ActualizarCache] Error al actualizar cache de conexiones MySQL: {ex.Message}", ex.StackTrace);
        }
    }

    // Para depuración externa
    public static int CacheCount => _cacheConexiones.Count;
}
