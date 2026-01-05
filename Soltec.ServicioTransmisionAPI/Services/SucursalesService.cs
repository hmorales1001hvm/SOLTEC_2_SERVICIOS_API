using ApiDAL;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

public class SucursalesService
{
    private readonly string _mysqlConnectionString;

    public SucursalesService(string mysqlConnectionString)
    {
        _mysqlConnectionString = mysqlConnectionString;
    }

    //public void StartAutoRefresh()
    //{
    //    Task.Run(async () =>
    //    {
    //        while (true)
    //        {
    //            try
    //            {
    //                await RefreshCacheAsync();
    //            }
    //            catch (Exception ex)
    //            {
    //                Console.WriteLine($"Error refrescando cache: {ex.Message}");
    //            }

    //            await Task.Delay(TimeSpan.FromMinutes(2));
    //        }
    //    });
    //}

    //private async Task RefreshCacheAsync()
    //{
    //    using var connection = new MySqlConnection(_mysqlConnectionString);
    //    await connection.OpenAsync();

    //    var query = @"
    //        SELECT A.claveSimi, B.HostName, B.UserName, B.Password, B.DatabaseName
    //        FROM sucursal A
    //        INNER JOIN soltec2_orquestador_servidormysql_detalle B 
    //            ON A.idEmpresa = B.IdEmpresa;";

    //    var sucursales = await connection.QueryAsync<SucursalDbInfo>(query);

    //    // Limpiar y actualizar cache de forma thread-safe
    //    SucursalesCache.Cache.Clear();
    //    foreach (var sucursal in sucursales)
    //    {
    //        SucursalesCache.Cache.TryAdd(sucursal.ClaveSimi, sucursal);
    //    }

    //    Console.WriteLine($"Cache MySQL actualizada a las {DateTime.Now}");
    //}
}
