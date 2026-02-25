using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Soltec.DB;
using System.Configuration;

public class ConexionCacheRepository
{
    private readonly IConfiguration Configuration;
    private readonly ILogger<ConexionCacheRepository> _logger;
    private bool IMP = false;
    private bool INF = false;
    private bool WRN = false;

    public ConexionCacheRepository(IConfiguration configuration,
        ILogger<ConexionCacheRepository> logger)
    {
         _logger = logger;
        Configuration = configuration;

        IMP = bool.Parse(Configuration["settingsAPIs:MostrarLogIMP"] ?? "false");
        INF = bool.Parse(Configuration["settingsAPIs:MostrarLogINF"] ?? "false");
        WRN = bool.Parse(Configuration["settingsAPIs:MostrarLogWRN"] ?? "false");
    }

    public async Task<ConexionEmpresa?> GetConexionAsync(string sucursal)
    {
        if (string.IsNullOrWhiteSpace(sucursal))
        {
            
            _logger.LogError("Sucursal nula o vacía");
            return null;
        }

        var clave = sucursal.Trim().ToUpper();
        const string sql = @"
            SELECT HostName, UserName, Password, DatabaseName, Puerto, IdEmpresa
            FROM CacheConexiones
            WHERE ClaveSimi = @ClaveSimi
            LIMIT 1";

        try
        {
            var connectionString = Configuration.GetConnectionString("DbFacturaRealOrquestador");
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var conexion = await connection.QueryFirstOrDefaultAsync<ConexionEmpresa>(
                sql,
                new { ClaveSimi = clave });

            if (conexion == null)
            {
                if (WRN)
                    _logger.LogWarning(
                    "No existe CacheConexiones para ClaveSimi {ClaveSimi}",
                    clave);
            }
            else
            {
                if (INF)
                    _logger.LogInformation(
                    "CacheConexiones encontrada para ClaveSimi {ClaveSimi} (DB={DatabaseName}, IdEmpresa={IdEmpresa})",
                    clave, conexion.DatabaseName, conexion.IdEmpresa);
            }

            return conexion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error al consultar CacheConexiones para ClaveSimi {ClaveSimi}",
                clave);
            return null;
        }
    }
}
