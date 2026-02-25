using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySql.Data.MySqlClient;
using Soltec.Common.Logger;
using Soltec.DB;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;

public class ConexionCacheService : BackgroundService
{
    private readonly string _mysqlConnection;
    private readonly int _cacheUpdateMinutes;
    private readonly bool _ambienteWindows;


    // Cache en memoria desde MySQL
    private static ConcurrentDictionary<string, ConexionEmpresa> _cacheConexiones = new();
    private static bool _cacheInicializado = false;
    private static ConcurrentDictionary<string, ConexionEmpresa> _cacheDesdeJson = new();

    // Ruta Linux
    private static string CacheDir = "";
    private static string CachePath = Path.Combine(CacheDir, "CacheConexiones.json");
    private static string TempPath = Path.Combine(CacheDir, "CacheConexiones.tmp.json");

    public ConexionCacheService(IConfiguration configuration)
    {
        _mysqlConnection = configuration.GetConnectionString("DbFacturaRealOrquestador") ?? throw new Exception("Connection string no configurada");

        if (!int.TryParse(configuration["settingsAPIs:TimeUpdateCacheService"], out _cacheUpdateMinutes))
            _cacheUpdateMinutes = 10;


        if (!bool.TryParse(configuration["settingsAPIs:AmbienteWindows"], out _ambienteWindows))
            _ambienteWindows = false;

        CacheDir = string.IsNullOrWhiteSpace(configuration["settingsAPIs:PathCache"])
        ? "/var/cache/app"  
        : configuration["settingsAPIs:PathCache"].Trim();

        CachePath = Path.Combine(CacheDir, "CacheConexiones.json");
        TempPath = Path.Combine(CacheDir, "CacheConexiones.tmp.json");

        //Logger.Info($"[Inicio] ConexionCacheService inicializado. Intervalo de actualización: {_cacheUpdateMinutes} minutos");

        if (!Directory.Exists(CacheDir))
            Directory.CreateDirectory(CacheDir);

        // Cargar JSON al iniciar para fallback
        CargarCacheDesdeJson();
    }

    #region Cache MySQL

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Info("[ExecuteAsync] Servicio arrancando...");

        // Primer llenado
        await ActualizarCache();

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
                SELECT a.HostName, a.HostNamePublic, a.UserName, a.Password, a.DatabaseName, s.claveSimi, CAST(puerto AS VARCHAR(50)) Puerto, b.idEmpresa
                FROM soltec2_orquestador_servidormysql_detalle a
                INNER JOIN catempresa b ON a.IdEmpresa = b.idEmpresa
                INNER JOIN sucursal s ON b.idEmpresa = s.idEmpresa WHERE a.Activo = 1";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var hostName = string.Empty;
                if (_ambienteWindows)
                    hostName = reader.GetString("HostNamePublic");
                else
                    hostName = reader.GetString("HostName");

                var conexion = new ConexionEmpresa
                    {

                        HostName = hostName,
                        UserName = reader.IsDBNull("UserName") ? string.Empty : reader.GetString("UserName"),
                        Password = reader.IsDBNull("Password") ? string.Empty : reader.GetString("Password"),
                        Puerto = reader.IsDBNull("puerto") ? "1433" : reader.GetString("puerto").ToString(),
                        DatabaseName = reader.IsDBNull("DatabaseName") ? string.Empty : reader.GetString("DatabaseName"),
                        IdEmpresa = reader.IsDBNull("idEmpresa") ? 0 : reader.GetInt32("idEmpresa")
                    };

                var claveSimi = reader.IsDBNull("claveSimi") ? string.Empty : reader.GetString("claveSimi").Trim().ToUpper();

                if (!string.IsNullOrWhiteSpace(claveSimi))
                    nuevoCache[claveSimi] = conexion;
            }

            // Swap atómico
            Interlocked.Exchange(ref _cacheConexiones, nuevoCache);
            _cacheInicializado = true;

            Logger.Info($"[ActualizarCache] Cache cargado con {_cacheConexiones.Count} registros");

            // Guardar JSON seguro
            GuardarCacheComoJson(nuevoCache);
        }
        catch (Exception ex)
        {
            Logger.Error($"[ActualizarCache] Error al actualizar cache desde MySQL: {ex.Message}", ex.StackTrace);
        }
    }

    private void GuardarCacheComoJson(ConcurrentDictionary<string, ConexionEmpresa> cache)
    {
        try
        {
            // Serializa JSON sin escapar caracteres especiales
            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            // Escribir archivo temporal primero
            File.WriteAllText(TempPath, json);

            // Reemplazo atómico del archivo original
            File.Move(TempPath, CachePath, true);

            Logger.Info($"[GuardarCacheComoJson] Archivo JSON actualizado ({cache.Count} registros). Ruta: {CachePath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"[GuardarCacheComoJson] Error al guardar JSON: {ex.Message}", ex.StackTrace);
        }
    }


    #endregion

    #region Cache JSON (fallback)

    public void CargarCacheDesdeJson()
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                Logger.Warning($"[CargarCacheDesdeJson] No existe el archivo {CachePath}");
                return;
            }

            var json = File.ReadAllText(CachePath);
            var cache = JsonSerializer.Deserialize<Dictionary<string, ConexionEmpresa>>(json);

            if (cache != null)
            {
                _cacheDesdeJson = new ConcurrentDictionary<string, ConexionEmpresa>(cache);
                //Logger.Info($"[CargarCacheDesdeJson] Cache cargado desde JSON ({_cacheDesdeJson.Count} registros)");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[CargarCacheDesdeJson] Error al leer JSON: {ex.Message}", ex.StackTrace);
        }
    }

    #endregion

    #region Obtener conexión SQL Server

    public async Task<ConexionEmpresa> GetConexionSqlServerAsync(string claveSimi)
    {
        if (string.IsNullOrWhiteSpace(claveSimi))
            return null;

        var key = claveSimi.Trim().ToUpper();

        if (!_cacheDesdeJson.TryGetValue(key, out var conexion))
        {
            Logger.Warning($"[GetConexionSqlServerAsync] No se encontró ClaveSimi '{key}' en el cache JSON");
            return null;
        }

        return conexion;
       
    }

    #endregion

    #region Lectura directa del cache MySQL

    public static bool TryGetConexion(string claveSimi, out ConexionEmpresa conexion)
    {
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

        var key = claveSimi.Trim().ToUpper();
        return _cacheConexiones.TryGetValue(key, out conexion);
    }

    #endregion

    // Para depuración externa
    public static int CacheCount => _cacheConexiones.Count;
}











//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Hosting;
//using MySql.Data.MySqlClient;
//using Soltec.Common.Logger;
//using Soltec.DB;
//using System.Collections.Concurrent;
//using System.Data;

//public class ConexionCacheService : BackgroundService
//{
//    private readonly string _mysqlConnection;
//    private static ConcurrentDictionary<string, ConexionEmpresa> _cacheConexiones = new();
//    private readonly int _cacheUpdateMinutes;
//    private static bool _cacheInicializado = false;

//    public ConexionCacheService(IConfiguration configuration)
//    {
//        ////_mysqlConnection = configuration.GetConnectionString("DbFacturaRealOrquestador");
//        ////if (!int.TryParse(configuration["settingsAPIs:TimeUpdateCacheService"], out _cacheUpdateMinutes))
//        ////{
//        ////    _cacheUpdateMinutes = 10;
//        ////}

//        ////Logger.Info($"[Inicio] ConexionCacheService inicializado. Intervalo de actualización: {_cacheUpdateMinutes} minutos");
//    }


//    public static bool TryGetConexion(string claveSimi, out ConexionEmpresa conexion)
//    {
//        var start = DateTime.UtcNow;
//        conexion = null;

//        if (!_cacheInicializado)
//        {
//            Logger.Warning($"[TryGetConexion] Cache aún no inicializado. Clave solicitada: '{claveSimi}'");
//            return false;
//        }

//        if (string.IsNullOrWhiteSpace(claveSimi))
//        {
//            Logger.Warning("[TryGetConexion] claveSimi es null o vacía");
//            return false;
//        }

//        // Normalizamos la clave
//        var key = claveSimi.Trim().ToUpper();

//        Logger.Warning($"[TryGetConexion] SERVER={Environment.MachineName} CacheCount={_cacheConexiones.Count}");


//        bool found = _cacheConexiones.TryGetValue(key, out conexion);
//        Logger.Info($"Total en cache despues de realizar la búsqueda: {_cacheConexiones.Count}, Sucursal: {key}");

//        if (found)
//        {
//            Logger.Info($"[TryGetConexion] ClaveSimi encontrada: '{key}', IdEmpresa={conexion.IdEmpresa}, DB={conexion.DatabaseName}, CacheCount={_cacheConexiones.Count}, Tiempo={DateTime.UtcNow - start}");
//        }
//        else
//        {
//            Logger.Warning($"[TryGetConexion] NO se encontró ClaveSimi '{key}' en cache. CacheCount={_cacheConexiones.Count}, Tiempo={DateTime.UtcNow - start}");
//            // Opcional: listar las claves actuales para depuración
//            Logger.Debug("[TryGetConexion] Claves actuales en cache: " + string.Join(", ", _cacheConexiones.Keys));
//        }

//        return found;
//    }


//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        //Logger.Info("[ExecuteAsync] Servicio arrancando...");

//        //await ActualizarCache(); // Primer llenado del cache
//        //Logger.Info($"[ExecuteAsync] Cache inicial cargado. Total registros: {_cacheConexiones.Count}");

//        //while (!stoppingToken.IsCancellationRequested)
//        //{
//        //    Logger.Info($"[ExecuteAsync] Esperando {_cacheUpdateMinutes} minutos para próxima actualización...");
//        //    await Task.Delay(TimeSpan.FromMinutes(_cacheUpdateMinutes), stoppingToken);

//        //    Logger.Info("[ExecuteAsync] Iniciando actualización de cache...");
//        //    await ActualizarCache();
//        //}
//    }

//    private async Task ActualizarCache()
//    {
//        var nuevoCache = new ConcurrentDictionary<string, ConexionEmpresa>();

//        try
//        {
//            Logger.Info("[ActualizarCache] Conectando a MySQL...");
//            using var conn = new MySqlConnection(_mysqlConnection);
//            await conn.OpenAsync();
//            Logger.Info("[ActualizarCache] Conexión MySQL abierta.");

//            const string sql = @"
//                SELECT a.HostName, a.UserName, a.Password, a.DatabaseName, s.claveSimi, a.puerto, b.idEmpresa
//                FROM soltec2_orquestador_servidormysql_detalle a
//                INNER JOIN catempresa b ON a.IdEmpresa = b.idEmpresa
//                INNER JOIN sucursal s ON b.idEmpresa = s.idEmpresa";

//            using var cmd = new MySqlCommand(sql, conn);
//            using var reader = await cmd.ExecuteReaderAsync();

//            while (await reader.ReadAsync())
//            {
//                var conexion = new ConexionEmpresa
//                {
//                    HostName = reader.IsDBNull("HostName") ? string.Empty : reader.GetString("HostName"),
//                    UserName = reader.IsDBNull("UserName") ? string.Empty : reader.GetString("UserName"),
//                    Password = reader.IsDBNull("Password") ? string.Empty : reader.GetString("Password"),
//                    DatabaseName = reader.IsDBNull("DatabaseName") ? string.Empty : reader.GetString("DatabaseName"),
//                    IdEmpresa = reader.IsDBNull("idEmpresa") ? 0 : reader.GetInt32("idEmpresa")
//                };

//                var claveSimi = reader.IsDBNull("claveSimi") ? string.Empty : reader.GetString("claveSimi").Trim().ToUpper();

//                if (!string.IsNullOrWhiteSpace(claveSimi))
//                {
//                    nuevoCache[claveSimi] = conexion;
//                }
//            }

//            Interlocked.Exchange(ref _cacheConexiones, nuevoCache);
//            _cacheInicializado = true; // <-- Marcar cache como listo
//            Logger.Warning($"[ActualizarCache] SERVER={Environment.MachineName} Cache cargado con {_cacheConexiones.Count} registros");

//        }
//        catch (Exception ex)
//        {
//            Logger.Error($"[ActualizarCache] Error al actualizar cache de conexiones MySQL: {ex.Message}", ex.StackTrace);
//        }
//    }

//    // Para depuración externa
//    public static int CacheCount => _cacheConexiones.Count;
//}
