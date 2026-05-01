using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Soltec.Common.Logger;
using Soltec.Entities.Ventas;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Soltec.DB
{

    public class SetDeTransmisionesDB
    {

        private readonly IConfiguration Configuration;
        private readonly ILogger<SetDeTransmisionesDB> _Logger;
        private readonly ILogger<ConexionCacheRepository> _Logger2;
        private int batchSize = 1500;
        private bool IMP = false;
        private bool INF = false;
        private bool WRN = false;
        //private bool Ventas = true;
        //private bool VentasDesgloseTotales = true;
        //private bool VentasImportesProductos = true;
        //private bool VentasImpuestos = true;
        //private bool VentasImpuestosDetalle = true;
        //private bool VentasProductos = true;
        //private bool VentasVendedorCuotas = true;
        //private bool AmbienteWindows = true;

        string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "desconocida";

        public SetDeTransmisionesDB(IConfiguration configuration, ILogger<SetDeTransmisionesDB> logger, ILogger<ConexionCacheRepository> logger2)
        {
            Configuration = configuration;
            _Logger = logger;
            _Logger2 = logger2;
            IMP = Configuration.GetValue("settingsAPIs:MostrarLogIMP", false);
            INF = Configuration.GetValue("settingsAPIs:MostrarLogINF", false);
            WRN = Configuration.GetValue("settingsAPIs:MostrarLogWRN", false);

            //Ventas = Configuration.GetValue("settingsAPIs:Ventas", false);
            //VentasDesgloseTotales = Configuration.GetValue("settingsAPIs:VentasDesgloseTotales", false);
            //VentasImportesProductos = Configuration.GetValue("settingsAPIs:VentasImportesProductos", false);
            //VentasImpuestos = Configuration.GetValue("settingsAPIs:VentasImpuestos", false);
            //VentasImpuestosDetalle = Configuration.GetValue("settingsAPIs:VentasImpuestosDetalle", false);
            //VentasProductos = Configuration.GetValue("settingsAPIs:VentasProductos", false);
            //VentasVendedorCuotas = Configuration.GetValue("settingsAPIs:VentasVendedorCuotas", false);
            //AmbienteWindows = Configuration.GetValue("settingsAPIs:AmbienteWindows", false);
            
        }

        private static readonly SemaphoreSlim _sqlSemaphore = new SemaphoreSlim(80); // SQL Server
        private static readonly SemaphoreSlim _mysqlSemaphore = new SemaphoreSlim(50); // MySQL

        /// <summary>
        /// Método para sincronizar datos MySQL o SQL Server.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SincronizaScriptUltimo(ProcesosOnLine data)
        {
            string proceso = data.NombreProceso;

            Logger.Info($"Versión: 1.0.0.1");

            await _sqlSemaphore.WaitAsync();
            try
            {
                if (INF)
                    Logger.Info($"Nombre del proceso a ejecutar: {data.NombreProceso}");

                if (data.NombreProceso.Trim() == "Ventas_VentasProductos")
                {
                    //if (IMP)
                    //    Logger.Important($"Carga de la sucursal: {data.Sucursal} - SincronizaSetDeTransmisionesSQLServer - {data.NombreProceso}");

                    await SincronizaSetDeTransmisionesSQLServerUltimo(data);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(
                    $"{proceso} - Error en SincronizaScript para la Sucursal: {data.Sucursal}. {ex.Message}");
                throw;
            }
            finally
            {
                _sqlSemaphore.Release();
            }
        }



        public async Task ActualizarEstatusHistorico(string sucursal)
        {
            try
            {
                if (INF)
                    Logger.Info($"Actualizando sucursal {sucursal} a ESTATUS='RECIBIDO' en soltec2_Historicos");

                var connectionString = Configuration.GetConnectionString("DbFacturaRealOrquestador");
                var connectionStringSIMIPET = Configuration.GetConnectionString("DbSimiPET");
                string cs = sucursal.Trim().Contains("VF") ? connectionStringSIMIPET : connectionString;

                using var connection = new MySqlConnection(cs);
                await connection.OpenAsync();

                string sql = @"UPDATE soltec2_Historicos 
                       SET Estatus = @Estatus, FechaRecibido = SYSDATE()
                       WHERE ClaveSimi = @Sucursal AND Estatus = 'PENDIENTE'";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Estatus", "RECIBIDO");
                cmd.Parameters.AddWithValue("@Sucursal", sucursal);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                if (INF)
                    Logger.Info($"Filas actualizadas: {rowsAffected}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error al actualizar la sucursal RECIBIDO histórico: {ex.Message}");
                throw;
            }
        }




        /// <summary>
        /// Método para ejecutar procesos de carga SIMI PET
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task SincronizaScript_SimiPET(ProcesosOnLine data)
        {
            string connString = $"Server={data.HostName};Database={data.DatabaseName};User Id={data.UserName};Password={data.Password};TrustServerCertificate=True;Connect Timeout=180;";
            using var connection = new SqlConnection(connString);
            string nombreProceso = "";
            await connection.OpenAsync();
            int idEmpresa = 0;
            
            try
            {
                if (IMP)
                    Logger.Important($"Iniciando SIMIPET - Sucursal: {data.Sucursal}");

                // Deserializar DTO raíz
                var dto = JsonSerializer.Deserialize<SalesDataDto>(data.Json) ?? new SalesDataDto();
                // ============================================================
                // Preparar datos válidos y asignar IdEmpresa
                // ============================================================
                var ventasValidas = dto.Ventas?.Where(v => v.Id_Venta != null).ToList();
                ventasValidas?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasProductos = dto.VentasProductos?.Where(v => v.Id_Venta != null).ToList();
                ventasProductos?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasImpuestos = dto.VentasImpuestos?.Where(v => v.Id_Venta != null).ToList();
                ventasImpuestos?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasImpuestosDetalle = dto.VentasImpuestosDetalle?.Where(v => v.Id_Venta != null).ToList();
                ventasImpuestosDetalle?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasDesgloceTotales = dto.VentasDesgloceTotales?.Where(v => v.Id_Venta != null).ToList();
                ventasDesgloceTotales?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasImportesProductos = dto.VentasImportesProductos?.Where(v => v.Id_Venta != null).ToList();
                ventasImportesProductos?.ForEach(x => x.IdEmpresa = idEmpresa);

                var inventarioCosto = dto.InventarioCosto ?
                                                            .Where(i => !string.IsNullOrWhiteSpace(i.ClaveSimi)
                                                                     && !string.IsNullOrWhiteSpace(i.Codigo)
                                                                     && i.FechaFactura != default)
                                                            .ToList();

                
                var sposInventario = dto.SPOSInventario?.ToList();

                var sposFacturas = dto.SPOSFacturas?.ToList();

                var ventasVendedorCuotasConSucursal = dto.VentasVendedorCuotas
                    ?.Select(v => new VentasVendedorCuotasDto
                    {
                        ClaveSimi = data.Sucursal,
                        Fecha = v.Fecha,
                        IdVendedor = v.IdVendedor,
                        Nombre = v.Nombre,
                        ImporteVenta = v.ImporteVenta,
                        Transaccionesventa = v.Transaccionesventa,
                        PorcVenta = v.PorcVenta,
                        ImporteNaturistas = v.ImporteNaturistas,
                        PorcNaturistas = v.PorcNaturistas,
                        ImporteNocturno = v.ImporteNocturno,
                        MontoDescuento = v.MontoDescuento,
                        Menudeos = v.Menudeos,
                        MontoIva = v.MontoIva,
                        IdEmpresa = idEmpresa
                    }).ToList();

                // ============================================================
                // Ejecutar SP maestro solo si hay al menos 1 tabla con datos
                // ============================================================
                bool hayDatos = (ventasValidas?.Count > 0) ||
                                (ventasProductos?.Count > 0) ||
                                (ventasImpuestos?.Count > 0) ||
                                (ventasImpuestosDetalle?.Count > 0) ||
                                (ventasDesgloceTotales?.Count > 0) ||
                                (ventasImportesProductos?.Count > 0) ||
                                (ventasVendedorCuotasConSucursal?.Count > 0) ||
                                (inventarioCosto?.Count > 0) ||
                                (sposInventario?.Count > 0) ||
                                (sposFacturas?.Count > 0);

                if (hayDatos)
                {
                    var sw = Stopwatch.StartNew();

                    using var cmd = new SqlCommand("dbo.usp_OrquestadorVentasMaestro", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 1200
                    };

                    if (ventasValidas?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasValidas);
                        cmd.Parameters.AddWithValue("@Ventas", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@Ventas"].TypeName = "dbo.TvpVentas";
                    }

                    if (ventasProductos?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasProductos);
                        cmd.Parameters.AddWithValue("@VentasProductos", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasProductos"].TypeName = "dbo.TvpVentasProductos";
                    }

                    if (ventasImpuestos?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasImpuestos);
                        cmd.Parameters.AddWithValue("@VentasImpuestos", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasImpuestos"].TypeName = "dbo.TvpVentasImpuestos";
                    }

                    if (ventasImpuestosDetalle?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasImpuestosDetalle);
                        cmd.Parameters.AddWithValue("@VentasImpuestosDetalle", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasImpuestosDetalle"].TypeName = "dbo.TvpVentasImpuestosDetalle";
                    }

                    if (ventasDesgloceTotales?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasDesgloceTotales);
                        cmd.Parameters.AddWithValue("@VentasDesgloseTotales", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasDesgloseTotales"].TypeName = "dbo.TvpVentasDesgloseTotales";
                    }

                    if (ventasImportesProductos?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasImportesProductos);
                        cmd.Parameters.AddWithValue("@VentasImportesProductos", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasImportesProductos"].TypeName = "dbo.TvpVentasImportesProductos";
                    }

                    if (ventasVendedorCuotasConSucursal?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasVendedorCuotasConSucursal);
                        cmd.Parameters.AddWithValue("@VentasVendedorCuotas", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasVendedorCuotas"].TypeName = "dbo.TvpVentasVendedorCuotas";
                    }

                    if (inventarioCosto?.Count > 0)
                    {
                        var table = await DataTableAsync(inventarioCosto);
                        cmd.Parameters.AddWithValue("@InventarioCosto", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@InventarioCosto"].TypeName = "dbo.TvpInventarioCosto";
                    }

                    if (sposInventario?.Count > 0)
                    {
                        var table = await DataTableAsync(sposInventario);
                        cmd.Parameters.AddWithValue("@SPOSInventario", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@SPOSInventario"].TypeName = "dbo.TvpSPOSInventario";
                    }

                    if (sposFacturas?.Count > 0)
                    {
                        var table = await DataTableAsync(sposFacturas);
                        cmd.Parameters.AddWithValue("@SPOSFacturas", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@SPOSFacturas"].TypeName = "dbo.TvpSPOSFacturas";
                    }

                    await cmd.ExecuteNonQueryAsync();

                    sw.Stop();
                    if (IMP)
                        Logger.Important($"Proceso usp_OrquestadorVentasMaestro | Tiempo total: {sw.ElapsedMilliseconds} ms");
                }
                else
                {
                    if (IMP)
                        Logger.Important("No hay datos para procesar en ninguna tabla, SP maestro no ejecutado.");
                }

                #region Comentarios
                #endregion

                // Commit transaction al terminar todo
                if (IMP)
                    Logger.Important($"Sincronización completada correctamente.");

                //// Actualizar versiones
                var connectionString = Configuration.GetConnectionString("DbSimiPET");
                using var cnx = new MySqlConnection(connectionString);
                await cnx.OpenAsync();
                var parameter = new DynamicParameters();
                parameter.Add("@pSucursal", data.Sucursal, DbType.String);
                parameter.Add("@pVersion1", data.Ver1, DbType.String);
                parameter.Add("@pVersion2", data.Ver2, DbType.String);
                parameter.Add("@pVersion3", data.Ver3, DbType.String);

                parameter.Add("@pVersion4", data.Ver4, DbType.String);
                parameter.Add("@pVersion5", data.Ver5, DbType.String);
                parameter.Add("@pVersion6", data.Ver6, DbType.String);

                await cnx.ExecuteAsync(
                    "usp_ActualizaSucursalesEnLinea",
                    parameter,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 1200);

                cnx.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (IMP)
                    Logger.Important($"Se actualizan versiones SIMI PET.");

            }
            catch (Exception ex)
            {
                try
                {
                    if (IMP)
                        Logger.Important($"ERROR EN EL SERVER SIMIPET: {nombreProceso} -  {connString} , {ex.Message}");
                    throw;
                }
                catch (Exception rollEx)
                {
                    if (IMP)
                        Logger.Important($"ERROR EN EL SERVER SIMIPET: {nombreProceso} -  {connString} , {ex.Message}");
                    throw;
                }
            }
            finally
            {
                connection.CloseAsync();
            }
        }

        // The given ColumnMapping does not match up with any column in the source or destination.

        public async Task ActualizaSucursalTransmision(ProcesosOnLine data)
        {
            var conexionCacheService = new ConexionCacheService(Configuration);
            var conexionEmpresa = await conexionCacheService.GetConexionSqlServerAsync(data.Sucursal);

            if (conexionEmpresa == null)
            {
                Logger.Warning($"No se encontró configuración para ClaveSimi: {data.Sucursal}");
                return;
            }

            using var connection = new SqlConnection(conexionEmpresa.ConnectionString);

            try
            {
                await connection.OpenAsync();

                DateTime? fechaValida = null;

                // Aquí viene la fecha de la sucursal.
                if (data.TicketsFaltantes != null)
                {
                    if (DateTime.TryParse(data.TicketsFaltantes.ToString(), out DateTime parsedFecha))
                    {
                        fechaValida = parsedFecha;
                    }
                }

                string sql;
                object parametros;

                if (fechaValida.HasValue)
                {
                    // Si la fecha es válida, la usamos
                    sql = @"
                            MERGE SucursalTransmision AS target
                            USING (SELECT @ClaveSimi AS ClaveSimi) AS source
                            ON target.ClaveSimi = source.ClaveSimi

                            WHEN MATCHED THEN
                                UPDATE SET FechaHoraTransmision = @FechaHoraTransmision

                            WHEN NOT MATCHED THEN
                                INSERT (ClaveSimi, FechaHoraTransmision)
                                VALUES (@ClaveSimi, @FechaHoraTransmision);";

                    parametros = new
                    {
                        ClaveSimi = data.Sucursal,
                        FechaHoraTransmision = fechaValida.Value
                    };
                }
                else
                {
                    sql = @"
                            MERGE SucursalTransmision AS target
                            USING (SELECT @ClaveSimi AS ClaveSimi) AS source
                            ON target.ClaveSimi = source.ClaveSimi

                            WHEN MATCHED THEN
                                UPDATE SET FechaHoraTransmision = GETDATE()

                            WHEN NOT MATCHED THEN
                                INSERT (ClaveSimi, FechaHoraTransmision)
                                VALUES (@ClaveSimi, GETDATE());";

                    parametros = new
                    {
                        ClaveSimi = data.Sucursal
                    };
                }
                await connection.ExecuteAsync(sql, parametros);
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.Message}");
                throw;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }



        public async Task SincronizaSetDeTransmisionesSQLServerUltimo(ProcesosOnLine data)
        {
            var conexionCacheService = new ConexionCacheService(Configuration);
            var conexionEmpresa = await conexionCacheService.GetConexionSqlServerAsync(data.Sucursal);

            if (conexionEmpresa == null)
            {
                Logger.Warning($"No se encontró configuración para ClaveSimi: {data.Sucursal}");
                return;
            }

            using var connection = new SqlConnection(conexionEmpresa.ConnectionString);
            var nombreProceso = string.Empty;
            var idEmpresa = conexionEmpresa.IdEmpresa;

            if (idEmpresa == 19)
                Logger.Important($"Procesando el cliente: {idEmpresa}, sucursal: {data.Sucursal}");

            //if (idEmpresa != 236 && idEmpresa != 39)
            //    return;

            try
            {
                await connection.OpenAsync();
                var dto = JsonSerializer.Deserialize<SalesDataDto>(data.Json) ?? new SalesDataDto();

                // ============================================================
                // Preparar datos válidos y asignar IdEmpresa
                // ============================================================
                var ventasValidas = dto.Ventas?.Where(v => v.Id_Venta != null).ToList();
                ventasValidas?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasProductos = dto.VentasProductos?.Where(v => v.Id_Venta != null).ToList();
                ventasProductos?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasImpuestos = dto.VentasImpuestos?.Where(v => v.Id_Venta != null).ToList();
                ventasImpuestos?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasImpuestosDetalle = dto.VentasImpuestosDetalle?.Where(v => v.Id_Venta != null).ToList();
                ventasImpuestosDetalle?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasDesgloceTotales = dto.VentasDesgloceTotales?.Where(v => v.Id_Venta != null).ToList();
                ventasDesgloceTotales?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasImportesProductos = dto.VentasImportesProductos?.Where(v => v.Id_Venta != null).ToList();
                ventasImportesProductos?.ForEach(x => x.IdEmpresa = idEmpresa);

                var ventasVendedorCuotasConSucursal = dto.VentasVendedorCuotas
                    ?.Select(v => new VentasVendedorCuotasDto
                    {
                        ClaveSimi = data.Sucursal,
                        Fecha = v.Fecha,
                        IdVendedor = v.IdVendedor,
                        Nombre = v.Nombre,
                        ImporteVenta = v.ImporteVenta,
                        Transaccionesventa = v.Transaccionesventa,
                        PorcVenta = v.PorcVenta,
                        ImporteNaturistas = v.ImporteNaturistas,
                        PorcNaturistas = v.PorcNaturistas,
                        ImporteNocturno = v.ImporteNocturno,
                        MontoDescuento = v.MontoDescuento,
                        Menudeos = v.Menudeos,
                        MontoIva = v.MontoIva,
                        IdEmpresa = idEmpresa
                    }).ToList();

                // ============================================================
                // Ejecutar SP maestro solo si hay al menos 1 tabla con datos
                // ============================================================
                bool hayDatos = (ventasValidas?.Count > 0) ||
                                (ventasProductos?.Count > 0) ||
                                (ventasImpuestos?.Count > 0) ||
                                (ventasImpuestosDetalle?.Count > 0) ||
                                (ventasDesgloceTotales?.Count > 0) ||
                                (ventasImportesProductos?.Count > 0) ||
                                (ventasVendedorCuotasConSucursal?.Count > 0);

                if (hayDatos)
                {
                    var sw = Stopwatch.StartNew();

                    using var cmd = new SqlCommand("dbo.usp_OrquestadorVentasMaestro", connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        CommandTimeout = 800
                    };

                    if (ventasValidas?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasValidas);
                        cmd.Parameters.AddWithValue("@Ventas", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@Ventas"].TypeName = "dbo.TvpVentas";
                    }

                    if (ventasProductos?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasProductos);
                        cmd.Parameters.AddWithValue("@VentasProductos", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasProductos"].TypeName = "dbo.TvpVentasProductos";
                    }

                    if (ventasImpuestos?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasImpuestos);
                        cmd.Parameters.AddWithValue("@VentasImpuestos", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasImpuestos"].TypeName = "dbo.TvpVentasImpuestos";
                    }

                    if (ventasImpuestosDetalle?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasImpuestosDetalle);
                        cmd.Parameters.AddWithValue("@VentasImpuestosDetalle", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasImpuestosDetalle"].TypeName = "dbo.TvpVentasImpuestosDetalle";
                    }

                    if (ventasDesgloceTotales?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasDesgloceTotales);
                        cmd.Parameters.AddWithValue("@VentasDesgloseTotales", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasDesgloseTotales"].TypeName = "dbo.TvpVentasDesgloseTotales";
                    }

                    if (ventasImportesProductos?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasImportesProductos);
                        cmd.Parameters.AddWithValue("@VentasImportesProductos", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasImportesProductos"].TypeName = "dbo.TvpVentasImportesProductos";
                    }

                    if (ventasVendedorCuotasConSucursal?.Count > 0)
                    {
                        var table = await DataTableAsync(ventasVendedorCuotasConSucursal);
                        cmd.Parameters.AddWithValue("@VentasVendedorCuotas", table).SqlDbType = SqlDbType.Structured;
                        cmd.Parameters["@VentasVendedorCuotas"].TypeName = "dbo.TvpVentasVendedorCuotas";
                    }

                    await cmd.ExecuteNonQueryAsync();

                    sw.Stop();
                    if (IMP)
                        Logger.Important($"Proceso usp_OrquestadorVentasMaestro | Tiempo total: {sw.ElapsedMilliseconds} ms");
                }
                else
                {
                    if (IMP)
                        Logger.Important("No hay datos para procesar en ninguna tabla, SP maestro no ejecutado.");
                }


                // ============================================================
                // SucursalTransmision (SIN MERGE)
                // ============================================================
                DateTime? fechaValida = null;

                if (data.TicketsFaltantes != null &&
                    DateTime.TryParse(data.TicketsFaltantes.ToString(), out DateTime parsedFecha))
                {
                    fechaValida = parsedFecha;
                }

                string sql;

                if (fechaValida.HasValue)
                {
                    sql = @"
                            UPDATE SucursalTransmision
                            SET FechaHoraTransmision = @FechaHoraTransmision
                            WHERE ClaveSimi = @ClaveSimi;

                            IF @@ROWCOUNT = 0
                            BEGIN
                                INSERT INTO SucursalTransmision (ClaveSimi, FechaHoraTransmision)
                                VALUES (@ClaveSimi, @FechaHoraTransmision);
                            END";
                }
                else
                {
                    sql = @"
                            UPDATE SucursalTransmision
                            SET FechaHoraTransmision = GETDATE()
                            WHERE ClaveSimi = @ClaveSimi;

                            IF @@ROWCOUNT = 0
                            BEGIN
                                INSERT INTO SucursalTransmision (ClaveSimi, FechaHoraTransmision)
                                VALUES (@ClaveSimi, GETDATE());
                            END";
                }

                await connection.ExecuteAsync(sql, new
                {
                    ClaveSimi = data.Sucursal,
                    FechaHoraTransmision = fechaValida
                });

                if (idEmpresa == 263 || idEmpresa == 39 || idEmpresa==19)
                    Logger.Important($"Sincronización completada correctamente, empresa: {idEmpresa}.");
                else
                    Logger.Important("Sincronización completada correctamente.");

                await connection.CloseAsync();



                //MySQL
                await _mysqlSemaphore.WaitAsync();
                try
                {
                    var connectionString = Configuration.GetConnectionString("DbFacturaRealOrquestador");
                    using (var conn = new MySqlConnection(connectionString))
                    {
                        await conn.OpenAsync();

                        // Ejecutar SP con Dapper en la MISMA conexión
                        var parameter = new DynamicParameters();
                        parameter.Add("@pSucursal", data.Sucursal, DbType.String);
                        parameter.Add("@pVersion1", data.Ver1, DbType.String);
                        parameter.Add("@pVersion2", data.Ver2, DbType.String);
                        parameter.Add("@pVersion3", data.Ver3, DbType.String);
                        parameter.Add("@pVersion4", ".", DbType.String);
                        parameter.Add("@pVersion5", ".", DbType.String);
                        parameter.Add("@pVersion6", ".", DbType.String);

                        await conn.ExecuteAsync(
                            "usp_ActualizaSucursalesEnLinea",
                            parameter,
                            commandType: CommandType.StoredProcedure,
                            commandTimeout: 1200
                        );
                        await conn.CloseAsync();
                    }
                }
                finally
                {
                    _mysqlSemaphore.Release();
                }

            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                Logger.Warning($"Deadlock detectado en sucursal {data.Sucursal}. Reintentando...");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error -  Cliente:{idEmpresa} - Sucursal: {data.Sucursal}: {ex.Message}");
                throw;
            }
        }

        private async Task<DataTable> DataTableAsync<T>(IEnumerable<T> data)
        {
            if (data == null) return null;

            var list = data as IList<T> ?? data.ToList();
            if (!list.Any()) return null;
            int attempt = 0;
            var table = ToDataTable(list);
            int totalRows = table.Rows.Count;

            return table;

        }
                

        // ============================
        // Util: convertir lista genérica a DataTable
        // ============================
        private static DataTable ToDataTable<T>(IEnumerable<T> items)
        {
            var dt = new DataTable();
            var props = typeof(T).GetProperties();

            // Column names must match exactly the properties used in the temp table
            foreach (var p in props)
            {
                var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                dt.Columns.Add(p.Name, type);
            }

            foreach (var item in items)
            {
                var values = props.Select(p => p.GetValue(item) ?? DBNull.Value).ToArray();
                dt.Rows.Add(values);
            }

            return dt;
        }



        public async Task SincronizaScriptOrquestador(ProcesosOnLine data)
        {
            var proceso = string.Empty;
            int totalProcesados = 0;
            try
            {
                var connectionString = Configuration.GetConnectionString("DbFacturaRealOrquestador");

                Logger.Info($"SET DE VENTAS EN LINEA - Inicia proceso ejecutado desde el Orquestador - Sucursal: {data.Sucursal}");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                int idSucursal = data.IdSucursal > 0 ? data.IdSucursal : await GetIdSucursalAsync(connection, data.Sucursal);

                // Mapear JSON a DTO
                var dto = System.Text.Json.JsonSerializer.Deserialize<VentasLineaRootDto>(data.Json) ?? new VentasLineaRootDto();
                var ventas = dto.Soltec2EnlineaVentas ?? new List<VentasLineaDto>();

                // Crear tabla temporal
                string createTempVentas = @"CREATE TEMPORARY TABLE VentasTMP (
                                        FechaOperacion DATETIME,
                                        Total DECIMAL(18,4),
                                        tickets INT,
                                        IdSucursal INT
                                    );";
                using (var cmd = new MySqlCommand(createTempVentas, connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // Llenar DataTable
                DataTable dtV = new DataTable();
                dtV.Columns.Add("FechaOperacion", typeof(DateTime));
                dtV.Columns.Add("Total", typeof(decimal));
                dtV.Columns.Add("tickets", typeof(int));
                dtV.Columns.Add("IdSucursal", typeof(int));

                foreach (var v in ventas)
                {
                    dtV.Rows.Add(v.FechaOperacion, v.Total, v.Tickets, idSucursal);
                }

                // Bulk insert a la tabla temporal
                var bulkV = new MySqlBulkCopy(connection);
                bulkV.DestinationTableName = "VentasTMP";
                bulkV.BulkCopyTimeout = 1200;
                bulkV.WriteToServer(dtV);


                // Merge en tabla final incluyendo FechaRegistro
                string merge = @"
                                INSERT INTO soltec2_enlinea_ventas (FechaOperacion, IdSucursal, Total, tickets, FechaRegistro, fechaControl)
                                SELECT FechaOperacion, IdSucursal, Total, tickets, NOW(), NOW()
                                FROM VentasTMP AS src
                                ON DUPLICATE KEY UPDATE
                                    Total = VALUES(Total),
                                    tickets = VALUES(tickets),
                                    fechaControl = NOW();";

                using (var cmd = new MySqlCommand(merge, connection))
                {
                    cmd.CommandTimeout = 50000;
                    totalProcesados = await cmd.ExecuteNonQueryAsync();
                }
                if (INF)
                    Logger.Info($"Ventas en línea procesadas correctamente. Total registros: {totalProcesados}");
                await connection.CloseAsync();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en SincronizaScriptOrquestador --> MySQL para la Sucursal: {data.Sucursal}, Proceso: {proceso}. Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtiene el ID de la Sucursal con base a la Clave Simi
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="claveSimi"></param>
        /// <returns></returns>
        private async Task<int> GetIdSucursalAsync(MySqlConnection conn, string claveSimi)
        {
            const string query = "SELECT IdSucursal FROM sucursal WHERE ClaveSimi = @claveSimi";

            await using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@claveSimi", claveSimi);

            var result = await cmd.ExecuteScalarAsync();

            return result != null ? Convert.ToInt32(result) : 0;
        }



    }

    public class ConexionEmpresa
    {
        public string HostName { get; set; }
        public string DatabaseName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Puerto { get; set; }
        public int IdEmpresa { get; set; }
        public string ConnectionString =>
                    $"Server={HostName};" +
                    $"Database={DatabaseName};" +
                    $"User Id={UserName};" +
                    $"Password={Password};";
    }

}














