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
        private bool Ventas = true;
        private bool VentasDesgloseTotales = true;
        private bool VentasImportesProductos = true;
        private bool VentasImpuestos = true;
        private bool VentasImpuestosDetalle = true;
        private bool VentasProductos = true;
        private bool VentasVendedorCuotas = true;
        private bool AmbienteWindows = true;

        string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "desconocida";

        public SetDeTransmisionesDB(IConfiguration configuration, ILogger<SetDeTransmisionesDB> logger, ILogger<ConexionCacheRepository> logger2)
        {
            Configuration = configuration;
            _Logger = logger;
            _Logger2 = logger2;
            IMP = Configuration.GetValue("settingsAPIs:MostrarLogIMP", false);
            INF = Configuration.GetValue("settingsAPIs:MostrarLogINF", false);
            WRN = Configuration.GetValue("settingsAPIs:MostrarLogWRN", false);

            Ventas = Configuration.GetValue("settingsAPIs:Ventas", false);
            VentasDesgloseTotales = Configuration.GetValue("settingsAPIs:VentasDesgloseTotales", false);
            VentasImportesProductos = Configuration.GetValue("settingsAPIs:VentasImportesProductos", false);
            VentasImpuestos = Configuration.GetValue("settingsAPIs:VentasImpuestos", false);
            VentasImpuestosDetalle = Configuration.GetValue("settingsAPIs:VentasImpuestosDetalle", false);
            VentasProductos = Configuration.GetValue("settingsAPIs:VentasProductos", false);
            VentasVendedorCuotas = Configuration.GetValue("settingsAPIs:VentasVendedorCuotas", false);
            AmbienteWindows = Configuration.GetValue("settingsAPIs:AmbienteWindows", false);
            
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
                // 1) VENTAS
                // ============================================================
                if (Ventas)
                {
                    var ventasValidas = dto.Ventas?.Where(v => v.Id_Venta != null).ToList();
                    ventasValidas?.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "Ventas";

                    if (ventasValidas?.Count > 0)
                    {

                        //var table = DataTableAsync(ventasValidas);

                        var sw1 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasValidas, @"
                                                                        CREATE TABLE #TempVentas (
                                                                            FechaOperacion DATETIME NOT NULL,
                                                                            ClaveSimi CHAR(10) NOT NULL,
                                                                            Id_Venta INT NOT NULL,
                                                                            id_usuario_venta VARCHAR(50) NOT NULL,
                                                                            Empleado VARCHAR(100) NOT NULL,
                                                                            idRegistradora INT NOT NULL,
                                                                            idRegistradoraVenta INT NOT NULL,
                                                                            idRegistradoraCobro INT NOT NULL,
                                                                            TipoOperacion INT NOT NULL,
                                                                            FechaHoraVenta DATETIME NOT NULL,
                                                                            TipoVenta INT NOT NULL,
                                                                            IdEmpresa INT NOT NULL,
                                                                            Id_Venta_Referencia VARCHAR(13) NULL
                                                                        );",
                                                                        "#TempVentas",
                                                                        @"
                                                                        INSERT INTO Ventas (
                                                                            FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa,
                                                                            id_usuario_venta, Empleado,
                                                                            idRegistradora, idRegistradoraVenta, idRegistradoraCobro,
                                                                            TipoOperacion, FechaHoraVenta, TipoVenta, Id_Venta_Referencia
                                                                        )
                                                                        SELECT S.FechaOperacion, S.ClaveSimi, S.Id_Venta, S.IdEmpresa,
                                                                            S.id_usuario_venta, S.Empleado,
                                                                            S.idRegistradora, S.idRegistradoraVenta, S.idRegistradoraCobro,
                                                                            S.TipoOperacion, S.FechaHoraVenta, S.TipoVenta, S.Id_Venta_Referencia
                                                                        FROM #TempVentas S
                                                                        WHERE NOT EXISTS (
                                                                            SELECT 1
                                                                            FROM Ventas T
                                                                            WHERE T.FechaOperacion = S.FechaOperacion
                                                                              AND T.ClaveSimi = S.ClaveSimi
                                                                              AND T.Id_Venta = S.Id_Venta
                                                                        );");
                        sw1.Stop();
                        if (IMP)
                            Logger.Important(
                                $"SIMIPET - Proceso {nombreProceso} | Registros: {ventasValidas.Count} | Tiempo: {sw1.ElapsedMilliseconds} ms"
                            );
                    }
                }

                // ============================================================
                // 2) VENTAS PRODUCTOS
                // ============================================================
                if (VentasProductos)
                {
                    var ventasProductos = dto.VentasProductos?.Where(v => v.Id_Venta != null).ToList();
                    ventasProductos?.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasProductos";
                    if (ventasProductos?.Count > 0)
                    {
                        //var table = DataTableAsync(ventasProductos);

                        var sw2 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasProductos, @"CREATE TABLE #TempVentasProductos (
                                                                            FechaOperacion DATETIME NOT NULL,
                                                                            ClaveSimi CHAR(10) NOT NULL,
                                                                            Id_Venta INT NOT NULL,
                                                                            Codigo CHAR(10) NOT NULL,
                                                                            Id_ProductoSAT VARCHAR(20) NOT NULL,
                                                                            TipoOperacion INT NOT NULL,
                                                                            Producto VARCHAR(255) NOT NULL,
                                                                            NoPonderado BIT NOT NULL,
                                                                            Premio BIT NOT NULL,
                                                                            Combo BIT NOT NULL,
                                                                            Inventario BIT NOT NULL,
                                                                            Cantidad DECIMAL(10,2) NOT NULL,
                                                                            Precio DECIMAL(10,2) NOT NULL,
                                                                            IVA DECIMAL(10,2) NOT NULL,
                                                                            Descuento DECIMAL(10,2) NOT NULL,
                                                                            DescuentoPorciento DECIMAL(10,2) NOT NULL,
                                                                            IVA_Porciento DECIMAL(10,2) NOT NULL,
                                                                            IVA_Importe DECIMAL(10,2) NOT NULL,
                                                                            Presentacion VARCHAR(50) NULL,
                                                                            Nivel1 VARCHAR(50) NOT NULL,
                                                                            Nivel2 VARCHAR(50) NOT NULL,
                                                                            Nivel3 VARCHAR(50) NOT NULL,
                                                                            IdEmpresa INT NOT NULL
                                                                        );",
                                                                        "#TempVentasProductos",
                                                                        @"
                                                                        UPDATE T
                                                                        SET
                                                                            T.Id_ProductoSAT     = S.Id_ProductoSAT,
                                                                            T.TipoOperacion      = S.TipoOperacion,
                                                                            T.Producto           = S.Producto,
                                                                            T.NoPonderado        = S.NoPonderado,
                                                                            T.Premio             = S.Premio,
                                                                            T.Combo              = S.Combo,
                                                                            T.Inventario         = S.Inventario,
                                                                            T.Cantidad           = S.Cantidad,
                                                                            T.Precio             = S.Precio,
                                                                            T.IVA                = S.IVA,
                                                                            T.Descuento          = S.Descuento,
                                                                            T.DescuentoPorciento = S.DescuentoPorciento,
                                                                            T.IVA_Porciento      = S.IVA_Porciento,
                                                                            T.IVA_Importe        = S.IVA_Importe,
                                                                            T.Presentacion       = S.Presentacion,
                                                                            T.Nivel1             = S.Nivel1,
                                                                            T.Nivel2             = S.Nivel2,
                                                                            T.Nivel3             = S.Nivel3
                                                                        FROM VentasProductos T
                                                                        JOIN #TempVentasProductos S
                                                                          ON T.FechaOperacion = S.FechaOperacion
                                                                         AND T.ClaveSimi = S.ClaveSimi
                                                                         AND T.Id_Venta = S.Id_Venta
                                                                         AND T.Codigo = S.Codigo;

                                                                        INSERT INTO VentasProductos (
                                                                            FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa, Codigo,
                                                                            Id_ProductoSAT, TipoOperacion, Producto,
                                                                            NoPonderado, Premio, Combo, Inventario,
                                                                            Cantidad, Precio, IVA, Descuento,
                                                                            DescuentoPorciento, IVA_Porciento, IVA_Importe,
                                                                            Presentacion, Nivel1, Nivel2, Nivel3
                                                                        )
                                                                        SELECT S.FechaOperacion, S.ClaveSimi, S.Id_Venta, S.IdEmpresa, S.Codigo,
                                                                            S.Id_ProductoSAT, S.TipoOperacion, S.Producto,
                                                                            S.NoPonderado, S.Premio, S.Combo, S.Inventario,
                                                                            S.Cantidad, S.Precio, S.IVA, S.Descuento,
                                                                            S.DescuentoPorciento, S.IVA_Porciento, S.IVA_Importe,
                                                                            S.Presentacion, S.Nivel1, S.Nivel2, S.Nivel3
                                                                        FROM #TempVentasProductos S
                                                                        WHERE NOT EXISTS (
                                                                            SELECT 1
                                                                            FROM VentasProductos T
                                                                            WHERE T.FechaOperacion = S.FechaOperacion
                                                                              AND T.ClaveSimi = S.ClaveSimi
                                                                              AND T.Id_Venta = S.Id_Venta
                                                                              AND T.Codigo = S.Codigo
                                                                        );");
                        sw2.Stop();
                        if (IMP)
                            Logger.Important(
                                $"SIMIPET - Proceso {nombreProceso} | Registros: {ventasProductos.Count} | Tiempo: {sw2.ElapsedMilliseconds} ms"
                            );
                    }
                }

                if (VentasImpuestos)
                {
                    // 3) VentasImpuestos
                    var ventasImpuestos = dto.VentasImpuestos?.Where(v => v.Id_Venta != null).ToList();
                    ventasImpuestos.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasImpuestos";

                    if (IMP)
                        Logger.Important($"SIMIPET - Procesando VentasImpuestos ({ventasImpuestos?.Count ?? 0})...");

                    if (ventasImpuestos.Count > 0)
                    {
                        //var table = DataTableAsync(ventasImpuestos);

                        var sw3 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasImpuestos, @"CREATE TABLE #TempVentasImpuestos (
                                                                            FechaOperacion DATETIME NOT NULL,
                                                                            ClaveSimi CHAR(10) NOT NULL,
                                                                            Id_Venta INT NOT NULL,
                                                                            Impuesto VARCHAR(10) NOT NULL,
                                                                            TipoFactor VARCHAR(10) NOT NULL,
                                                                            TasaImpuesto NUMERIC(12,2) NOT NULL,
                                                                            ClaveSATImpuesto VARCHAR(10) NOT NULL,
                                                                            BaseImpuesto NUMERIC(12,2) NOT NULL,
                                                                            ImporteImpuesto NUMERIC(12,2) NOT NULL,
                                                                            TipoOperacion INT NOT NULL,
                                                                            IdEmpresa INT NOT NULL
                                                                    );",
                                                                    "#TempVentasImpuestos",
                                                                    @"INSERT INTO VentasImpuestos (
                                                                        FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa,
                                                                        Impuesto, TipoFactor, TasaImpuesto,
                                                                        ClaveSATImpuesto, BaseImpuesto, ImporteImpuesto, TipoOperacion
                                                                    )
                                                                    SELECT S.FechaOperacion, S.ClaveSimi, S.Id_Venta, S.IdEmpresa,
                                                                        S.Impuesto, S.TipoFactor, S.TasaImpuesto,
                                                                        S.ClaveSATImpuesto, S.BaseImpuesto, S.ImporteImpuesto, S.TipoOperacion
                                                                    FROM #TempVentasImpuestos S
                                                                    WHERE NOT EXISTS (
                                                                        SELECT 1
                                                                        FROM VentasImpuestos T
                                                                        WHERE T.FechaOperacion = S.FechaOperacion
                                                                          AND T.ClaveSimi = S.ClaveSimi
                                                                          AND T.Id_Venta = S.Id_Venta
                                                                          AND T.Impuesto = S.Impuesto
                                                                          AND T.TipoFactor = S.TipoFactor
                                                                          AND T.TasaImpuesto = S.TasaImpuesto
                                                                    );");
                        sw3.Stop();

                        if (IMP)
                            Logger.Important(
                                $"SIMIPET - Proceso {nombreProceso} | Registros: {ventasImpuestos.Count} | Tiempo: {sw3.ElapsedMilliseconds} ms"
                            );
                    }
                }

                if (VentasImpuestosDetalle)
                {
                    // 4) VentasImpuestosDetalle
                    var ventasImpuestosDetalle = dto.VentasImpuestosDetalle?.Where(v => v.Id_Venta != null).ToList();
                    ventasImpuestosDetalle.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasImpuestosDetalle";

                    if (IMP)
                        Logger.Important($"SIMIPET - Procesando VentasImpuestosDetalle ({ventasImpuestosDetalle?.Count ?? 0})...");

                    if (ventasImpuestosDetalle.Count > 0)
                    {
                        var sw4 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasImpuestosDetalle, @"CREATE TABLE #TempVentasImpuestosDetalle (
                                                                                    ClaveSimi CHAR(10) NOT NULL,
                                                                                    FechaOperacion DATETIME NOT NULL,
                                                                                    Id_Venta INT NOT NULL,
                                                                                    Id_Producto VARCHAR(10) NOT NULL,
                                                                                    Impuesto VARCHAR(10) NOT NULL,
                                                                                    ClaveImpuesto VARCHAR(10) NOT NULL,
                                                                                    TasaImpuesto NUMERIC(12,2) NOT NULL,
                                                                                    TipoFactor VARCHAR(10) NOT NULL,
                                                                                    Base NUMERIC(12,2) NOT NULL,
                                                                                    ImporteIVA NUMERIC(12,2) NOT NULL,
                                                                                    ImporteVenta NUMERIC(12,2) NOT NULL,
                                                                                    TipoOperacion INT NOT NULL,
                                                                                    IdEmpresa INT NOT NULL
                                                                            );",
                                                                            "#TempVentasImpuestosDetalle",
                                                                            @"
                                                                            INSERT INTO VentasImpuestosDetalle (
                                                                                ClaveSimi, FechaOperacion, Id_Venta, IdEmpresa,
                                                                                Id_Producto, Impuesto, ClaveImpuesto,
                                                                                TasaImpuesto, TipoFactor, Base,
                                                                                ImporteIVA, ImporteVenta, TipoOperacion
                                                                            )
                                                                            SELECT S.ClaveSimi, S.FechaOperacion, S.Id_Venta, S.IdEmpresa,
                                                                                S.Id_Producto, S.Impuesto, S.ClaveImpuesto,
                                                                                S.TasaImpuesto, S.TipoFactor, S.Base,
                                                                                S.ImporteIVA, S.ImporteVenta, S.TipoOperacion
                                                                            FROM #TempVentasImpuestosDetalle S
                                                                            WHERE NOT EXISTS (
                                                                                SELECT 1
                                                                                FROM VentasImpuestosDetalle T
                                                                                WHERE T.FechaOperacion = S.FechaOperacion
                                                                                  AND T.ClaveSimi = S.ClaveSimi
                                                                                  AND T.Id_Venta = S.Id_Venta
                                                                                  AND T.Id_Producto = S.Id_Producto
                                                                                  AND T.Impuesto = S.Impuesto
                                                                            );");
                        sw4.Stop();

                        if (IMP)
                            Logger.Important(
                                $"SIMIPET - Proceso {nombreProceso} | Registros: {ventasImpuestosDetalle.Count} | Tiempo: {sw4.ElapsedMilliseconds} ms");
                    }
                }

                if (VentasDesgloseTotales)
                {
                    // 5) VentasDesgloceTotales
                    var ventasDesgloceTotales = dto.VentasDesgloceTotales?.Where(v => v.Id_Venta != null).ToList();
                    ventasDesgloceTotales.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasDesgloseTotales";

                    if (IMP)
                        Logger.Important($"SIMIPET - Procesando VentasDesgloseTotales ({ventasDesgloceTotales?.Count ?? 0})...");

                    if (ventasDesgloceTotales.Count > 0)
                    {
                        var sw5 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasDesgloceTotales, @"CREATE TABLE #VentasDesgloseTotales (
                                                                            ClaveSimi CHAR(10) NOT NULL,
                                                                            FechaOperacion DATETIME NOT NULL,
                                                                            Id_Venta INT NOT NULL,
                                                                            PrecioSinIVA NUMERIC(12,2) NOT NULL,
                                                                            Importe NUMERIC(12,2) NOT NULL,
                                                                            Descuento NUMERIC(12,2) NOT NULL,
                                                                            Impuestos NUMERIC(12,2) NOT NULL,
                                                                            Total NUMERIC(12,2) NOT NULL,
                                                                            TipoOperacion INT NOT NULL,
                                                                            IdEmpresa INT NOT NULL
                                                                        );",
                                                                        "#VentasDesgloseTotales",
                                                                        @"
                                                                        INSERT INTO VentasDesgloseTotales (
                                                                            ClaveSimi, FechaOperacion, Id_Venta, IdEmpresa,
                                                                            PrecioSinIVA, Importe, Descuento,
                                                                            Impuestos, Total, TipoOperacion
                                                                        )
                                                                        SELECT S.ClaveSimi, S.FechaOperacion, S.Id_Venta, S.IdEmpresa,
                                                                            S.PrecioSinIVA, S.Importe, S.Descuento,
                                                                            S.Impuestos, S.Total, S.TipoOperacion
                                                                        FROM #VentasDesgloseTotales S
                                                                        WHERE NOT EXISTS (
                                                                            SELECT 1
                                                                            FROM VentasDesgloseTotales T
                                                                            WHERE T.FechaOperacion = S.FechaOperacion
                                                                              AND T.ClaveSimi = S.ClaveSimi
                                                                              AND T.Id_Venta = S.Id_Venta
                                                                        );");
                        sw5.Stop();

                        if (IMP)
                            Logger.Important(
                                $"SIMIPET - Proceso {nombreProceso} | Registros: {ventasDesgloceTotales.Count} | Tiempo: {sw5.ElapsedMilliseconds} ms"
                            );
                    }
                }

                if (VentasImportesProductos)
                {
                    // 6) VentasImportesProductos
                    var ventasImportesProductos = dto.VentasImportesProductos?.Where(v => v.Id_Venta != null).ToList();
                    ventasImportesProductos.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasImportesProductos";

                    if (IMP)
                        Logger.Important($"SIMIPET - Procesando VentasImportesProductos ({ventasImportesProductos?.Count ?? 0})...");

                    if (ventasImportesProductos.Count > 0)
                    {
                        var sw6 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasImportesProductos, @"CREATE TABLE #TempVentasImportesProductos (
                                                                                    FechaOperacion DATETIME NOT NULL,
                                                                                    ClaveSimi CHAR(10) NOT NULL,
                                                                                    Id_Venta INT NOT NULL,
                                                                                    Id_Producto VARCHAR(10) NOT NULL,
                                                                                    Precio NUMERIC(12,2) NOT NULL,
                                                                                    PrecioUnitarioNeto NUMERIC(18,4) NOT NULL,
                                                                                    Cantidad INT NOT NULL,
                                                                                    SubtotalNeto NUMERIC(18,4) NOT NULL,
                                                                                    SubtotalConImpuestos NUMERIC(18,4) NOT NULL,
                                                                                    DescuentoNeto NUMERIC(18,4) NOT NULL,
                                                                                    DescuentoConImpuestos NUMERIC(18,4) NOT NULL,
                                                                                    ImporteNeto NUMERIC(12,2) NOT NULL,
                                                                                    ImporteConImpuestos NUMERIC(12,2) NOT NULL,
                                                                                    ImpuestoCalculado NUMERIC(18,4) NOT NULL,
                                                                                    Total NUMERIC(12,2) NOT NULL,
                                                                                    TipoOperacion INT NOT NULL,
                                                                                    IdEmpresa INT NOT NULL
                                                                        );",
                                                                        "#TempVentasImportesProductos",
                                                                        @"INSERT INTO VentasImportesProductos (
                                                                            FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa,
                                                                            Id_Producto, Precio, PrecioUnitarioNeto,
                                                                            Cantidad, SubtotalNeto, SubtotalConImpuestos,
                                                                            DescuentoNeto, DescuentoConImpuestos,
                                                                            ImporteNeto, ImporteConImpuestos,
                                                                            ImpuestoCalculado, Total, TipoOperacion
                                                                        )
                                                                        SELECT S.FechaOperacion, S.ClaveSimi, S.Id_Venta, S.IdEmpresa,
                                                                            S.Id_Producto, S.Precio, S.PrecioUnitarioNeto,
                                                                            S.Cantidad, S.SubtotalNeto, S.SubtotalConImpuestos,
                                                                            S.DescuentoNeto, S.DescuentoConImpuestos,
                                                                            S.ImporteNeto, S.ImporteConImpuestos,
                                                                            S.ImpuestoCalculado, S.Total, S.TipoOperacion
                                                                        FROM #TempVentasImportesProductos S
                                                                        WHERE NOT EXISTS (
                                                                            SELECT 1
                                                                            FROM VentasImportesProductos T
                                                                            WHERE T.FechaOperacion = S.FechaOperacion
                                                                              AND T.ClaveSimi = S.ClaveSimi
                                                                              AND T.Id_Venta = S.Id_Venta
                                                                              AND T.Id_Producto = S.Id_Producto
                                                                        );");

                        sw6.Stop();

                        if (IMP)
                            Logger.Important(
                                $"SIMIPET - Proceso {nombreProceso} | Registros: {ventasImportesProductos.Count} | Tiempo: {sw6.ElapsedMilliseconds} ms"
                            );
                    }
                }

                if (VentasVendedorCuotas)
                {

                    // 7) VentasVendedorCuotas
                    nombreProceso = "VentasVendedorCuotas";

                    if (IMP)
                        Logger.Important($"SIMIPET - Procesando VentasVendedorCuotas ({dto.VentasVendedorCuotas?.Count ?? 0})...");
                    var ventasVendedorCuotasConSucursal = dto.VentasVendedorCuotas
                        .Select(v => new VentasVendedorCuotasDto
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

                    if (ventasVendedorCuotasConSucursal.Count > 0)
                    {
                        var sw7 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasVendedorCuotasConSucursal, @"CREATE TABLE #TempVentasVendedorCuotas (
                                                                                            ClaveSimi VARCHAR(6) NOT NULL,
                                                                                            Fecha DATETIME NOT NULL,
                                                                                            IdVendedor VARCHAR(10) NOT NULL,
                                                                                            Nombre VARCHAR(200) NOT NULL,
                                                                                            ImporteVenta DECIMAL(12,2) NOT NULL,
                                                                                            Transaccionesventa INT NOT NULL,
                                                                                            PorcVenta DECIMAL(12,2) NOT NULL,
                                                                                            ImporteNaturistas DECIMAL(12,2) NOT NULL,
                                                                                            PorcNaturistas DECIMAL(12,2) NOT NULL,
                                                                                            ImporteNocturno DECIMAL(12,2) NOT NULL,
                                                                                            MontoDescuento DECIMAL(12,2) NOT NULL,
                                                                                            Menudeos DECIMAL(12,2) NOT NULL,
                                                                                            MontoIva DECIMAL(12,2) NOT NULL,
                                                                                            IdEmpresa INT NOT NULL
                                                                                );",
                                                                                "#TempVentasVendedorCuotas",
                                                                                @"UPDATE T
                                                                                SET
                                                                                    T.Nombre = S.Nombre,
                                                                                    T.ImporteVenta = S.ImporteVenta,
                                                                                    T.Transaccionesventa = S.Transaccionesventa,
                                                                                    T.PorcVenta = S.PorcVenta,
                                                                                    T.ImporteNaturistas = S.ImporteNaturistas,
                                                                                    T.PorcNaturistas = S.PorcNaturistas,
                                                                                    T.ImporteNocturno = S.ImporteNocturno,
                                                                                    T.MontoDescuento = S.MontoDescuento,
                                                                                    T.Menudeos = S.Menudeos,
                                                                                    T.MontoIva = S.MontoIva
                                                                                FROM VentasVendedorCuotas T
                                                                                JOIN #TempVentasVendedorCuotas S
                                                                                  ON T.ClaveSimi = S.ClaveSimi
                                                                                 AND T.Fecha = S.Fecha
                                                                                 AND T.IdVendedor = S.IdVendedor;

                                                                                INSERT INTO VentasVendedorCuotas (
                                                                                    ClaveSimi, Fecha, IdVendedor, IdEmpresa,
                                                                                    Nombre, ImporteVenta, Transaccionesventa,
                                                                                    PorcVenta, ImporteNaturistas, PorcNaturistas,
                                                                                    ImporteNocturno, MontoDescuento, Menudeos, MontoIva
                                                                                )
                                                                                SELECT S.ClaveSimi, S.Fecha, S.IdVendedor, S.IdEmpresa,
                                                                                    S.Nombre, S.ImporteVenta, S.Transaccionesventa,
                                                                                    S.PorcVenta, S.ImporteNaturistas, S.PorcNaturistas,
                                                                                    S.ImporteNocturno, S.MontoDescuento, S.Menudeos, S.MontoIva
                                                                                FROM #TempVentasVendedorCuotas S
                                                                                WHERE NOT EXISTS (
                                                                                    SELECT 1
                                                                                    FROM VentasVendedorCuotas T
                                                                                    WHERE T.ClaveSimi = S.ClaveSimi
                                                                                      AND T.Fecha = S.Fecha
                                                                                      AND T.IdVendedor = S.IdVendedor
                                                                                );");

                        sw7.Stop();

                        if (IMP)
                            Logger.Important(
                                $"SIMIPET - Proceso {nombreProceso} | Registros: {ventasVendedorCuotasConSucursal.Count} | Tiempo: {sw7.ElapsedMilliseconds} ms"
                            );
                    }
                }


                var registrosValidos = dto.InventarioCosto?
                .Where(i => !string.IsNullOrWhiteSpace(i.ClaveSimi)
                         && !string.IsNullOrWhiteSpace(i.Codigo)
                         && i.FechaFactura != default)
                .ToList();

                if (IMP)
                    Logger.Important($"Procesando Inventario Costo ({registrosValidos?.Count ?? 0})...");

                await BulkExecuteAsync(
                                        connection,
                                        registrosValidos,

                                        @"
                                        CREATE TABLE #TempInventarioCosto (
                                            ClaveSimi CHAR(10) NOT NULL,
                                            Codigo CHAR(10) NOT NULL,
                                            CostoUnitario DECIMAL(12,2) NULL,
                                            FechaFactura DATE NOT NULL,
                                            FechaSurtido DATE NULL
                                        );",

                                        "#TempInventarioCosto",

                                        @"
                                        UPDATE T
                                        SET
                                            T.CostoUnitario = S.CostoUnitario,
                                            T.FechaSurtido  = S.FechaSurtido
                                        FROM Inventario_Costo T
                                        JOIN #TempInventarioCosto S
                                            ON T.ClaveSimi = S.ClaveSimi
                                            AND T.Codigo = S.Codigo
                                            AND T.FechaFactura = S.FechaFactura;

                                        INSERT INTO Inventario_Costo (
                                            ClaveSimi, Codigo, CostoUnitario, FechaFactura, FechaSurtido
                                        )
                                        SELECT S.ClaveSimi, S.Codigo, S.CostoUnitario, S.FechaFactura, S.FechaSurtido
                                        FROM #TempInventarioCosto S
                                        WHERE NOT EXISTS (
                                            SELECT 1
                                            FROM Inventario_Costo T WITH (UPDLOCK, HOLDLOCK)
                                            WHERE T.ClaveSimi = S.ClaveSimi
                                                AND T.Codigo = S.Codigo
                                                AND T.FechaFactura = S.FechaFactura
                                        );
                                        "
                                    );

                if (IMP)
                    Logger.Important("Inventario Costo procesado.");

                var registrosValidos2 = dto.SPOSInventario?
                    .Where(i => !string.IsNullOrWhiteSpace(i.ClaveSimi) && !string.IsNullOrWhiteSpace(i.Codigo))
                    .ToList();
                nombreProceso = " 8) SPOSInventario";
                if (registrosValidos2 != null || registrosValidos2.Count > 0)
                {
                    if (WRN)
                        Logger.Warning("No hay registros válidos para SPOS Inventario SIMIPET.");


                    if (IMP)
                        Logger.Important($"Procesando SPOS Inventario SIMIPET ({registrosValidos2.Count})...");


                    await BulkExecuteAsync( connection,
                                            registrosValidos2,

                                            @"
                                            CREATE TABLE #TempSPOSInventario (
                                                FechaOperacion DATETIME NOT NULL,
                                                ClaveSimi VARCHAR(10) NOT NULL,
                                                Codigo VARCHAR(20) NOT NULL,
                                                Producto VARCHAR(100) NULL,
                                                PrecioVenta DECIMAL(18,2),
                                                ExistenciaInicial INT NULL,
                                                ExistenciaFinal INT NULL,
                                                Entradas INT NULL,
                                                Salidas INT NULL
                                            );",

                                            "#TempSPOSInventario",

                                            @"
                                            UPDATE T
                                            SET
                                                T.ExistenciaInicial = S.ExistenciaInicial,
                                                T.ExistenciaFinal   = S.ExistenciaFinal,
                                                T.Entradas          = S.Entradas,
                                                T.Salidas           = S.Salidas,
                                                T.Producto          = S.Producto,
                                                T.PrecioVenta       = S.PrecioVenta
                                            FROM SPOSInventario T
                                            JOIN #TempSPOSInventario S
                                              ON T.FechaOperacion = S.FechaOperacion
                                             AND T.ClaveSimi = S.ClaveSimi
                                             AND T.Codigo = S.Codigo;

                                            INSERT INTO SPOSInventario (
                                                FechaOperacion, ClaveSimi, Codigo,
                                                ExistenciaInicial, ExistenciaFinal,
                                                Entradas, Salidas, Producto, PrecioVenta
                                            )
                                            SELECT DISTINCT
                                                S.FechaOperacion, S.ClaveSimi, S.Codigo,
                                                S.ExistenciaInicial, S.ExistenciaFinal,
                                                S.Entradas, S.Salidas, S.Producto, S.PrecioVenta
                                            FROM #TempSPOSInventario S
                                            WHERE NOT EXISTS (
                                                SELECT 1
                                                FROM SPOSInventario T WITH (UPDLOCK, HOLDLOCK)
                                                WHERE T.FechaOperacion = S.FechaOperacion
                                                  AND T.ClaveSimi = S.ClaveSimi
                                                  AND T.Codigo = S.Codigo
                                            );
                                            "
                                        );


                    if (IMP)
                        Logger.Important("SPOS Inventario procesado.");
                }


                var registrosValidosFacturas = dto.SPOSFacturas?
                    .Where(i => !string.IsNullOrWhiteSpace(i.ClaveSimi) && !string.IsNullOrWhiteSpace(i.Serie))
                    .ToList();

                var jsonFacturas = JsonSerializer.Serialize(registrosValidosFacturas, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                //Logger.Important($"Registros Facturas:\n{jsonFacturas}");

                nombreProceso = " 8) SPOSFacturas";
                if (registrosValidosFacturas != null || registrosValidosFacturas.Count > 0)
                {
                    if (WRN)
                        Logger.Warning("No hay registros válidos para SPOS Facturas SIMIPET.");

                    if (IMP)
                        Logger.Important($"Procesando SPOS Facturas ({registrosValidosFacturas.Count})...");

                    await BulkExecuteAsync( connection,
                                            registrosValidosFacturas,

                                            @"
                                            CREATE TABLE #TempSPOSFacturas (
                                                FechaOperacion DATETIME NOT NULL,
                                                ClaveSimi VARCHAR(10) NOT NULL,
                                                Serie VARCHAR(20) NOT NULL,
                                                Folio VARCHAR(25) NOT NULL,
                                                Estatus TINYINT NULL,
                                                Electronica BIT NULL,
                                                NotaCredito BIT NULL,
                                                GranTotal DECIMAL(18,2) NULL
                                            );",

                                            "#TempSPOSFacturas",

                                            @"
                                            UPDATE T
                                            SET
                                                T.Estatus      = S.Estatus,
                                                T.Electronica  = S.Electronica,
                                                T.NotaCredito  = S.NotaCredito,
                                                T.GranTotal    = S.GranTotal
                                            FROM SPOSFacturas T
                                            JOIN #TempSPOSFacturas S
                                              ON T.FechaOperacion = S.FechaOperacion
                                             AND T.ClaveSimi = S.ClaveSimi
                                             AND T.Serie = S.Serie
                                             AND T.Folio = S.Folio;

                                            INSERT INTO SPOSFacturas (
                                                FechaOperacion, ClaveSimi, Serie, Folio,
                                                Estatus, Electronica, NotaCredito, GranTotal
                                            )
                                            SELECT DISTINCT
                                                S.FechaOperacion, S.ClaveSimi, S.Serie, S.Folio,
                                                S.Estatus, S.Electronica, S.NotaCredito, S.GranTotal
                                            FROM #TempSPOSFacturas S
                                            WHERE NOT EXISTS (
                                                SELECT 1
                                                FROM SPOSFacturas T WITH (UPDLOCK, HOLDLOCK)
                                                WHERE T.FechaOperacion = S.FechaOperacion
                                                  AND T.ClaveSimi = S.ClaveSimi
                                                  AND T.Serie = S.Serie
                                                  AND T.Folio = S.Folio
                                            );
                                            "
                                        );


                    if (IMP)
                        Logger.Important("SPOS Facturas procesadas.");
                }



                // Commit transaction al terminar todo
                //transaction.Commit();
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
                    //transaction.Rollback();
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
                // 1) VENTAS
                // ============================================================
                if (Ventas)
                {
                    var ventasValidas = dto.Ventas?.Where(v => v.Id_Venta != null).ToList();
                    ventasValidas?.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "Ventas";

                    if (ventasValidas?.Count > 0)
                    {
                        var table = DataTableAsync(ventasValidas);
                        using SqlCommand cmd = new SqlCommand("dbo.SP_UpsertVentasVendedorCuotas", connection);

                        cmd.CommandType = CommandType.StoredProcedure;

                        // Asociar el DataTable como parámetro TVP
                        SqlParameter tvpParam = cmd.Parameters.AddWithValue("@VentasVendedorCuotas", table);
                        tvpParam.SqlDbType = SqlDbType.Structured;
                        tvpParam.TypeName = "dbo.TvpVentasVendedorCuotas";
                        cmd.ExecuteNonQuery();

                        //await BulkExecuteAsync(connection, ventasValidas, @"
                        //                                                CREATE TABLE #TempVentas (
                        //                                                    FechaOperacion DATETIME NOT NULL,
                        //                                                    ClaveSimi CHAR(10) NOT NULL,
                        //                                                    Id_Venta INT NOT NULL,
                        //                                                    id_usuario_venta VARCHAR(50) NOT NULL,
                        //                                                    Empleado VARCHAR(100) NOT NULL,
                        //                                                    idRegistradora INT NOT NULL,
                        //                                                    idRegistradoraVenta INT NOT NULL,
                        //                                                    idRegistradoraCobro INT NOT NULL,
                        //                                                    TipoOperacion INT NOT NULL,
                        //                                                    FechaHoraVenta DATETIME NOT NULL,
                        //                                                    TipoVenta INT NOT NULL,
                        //                                                    IdEmpresa INT NOT NULL,
                        //                                                    Id_Venta_Referencia VARCHAR(13) NULL
                        //                                                );",
                        //                                                "#TempVentas",
                        //                                                @"
                        //                                                INSERT INTO Ventas (
                        //                                                    FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa,
                        //                                                    id_usuario_venta, Empleado,
                        //                                                    idRegistradora, idRegistradoraVenta, idRegistradoraCobro,
                        //                                                    TipoOperacion, FechaHoraVenta, TipoVenta, Id_Venta_Referencia
                        //                                                )
                        //                                                SELECT S.FechaOperacion, S.ClaveSimi, S.Id_Venta, S.IdEmpresa,
                        //                                                    S.id_usuario_venta, S.Empleado,
                        //                                                    S.idRegistradora, S.idRegistradoraVenta, S.idRegistradoraCobro,
                        //                                                    S.TipoOperacion, S.FechaHoraVenta, S.TipoVenta, S.Id_Venta_Referencia
                        //                                                FROM #TempVentas S
                        //                                                WHERE NOT EXISTS (
                        //                                                    SELECT 1
                        //                                                    FROM Ventas T
                        //                                                    WHERE T.FechaOperacion = S.FechaOperacion
                        //                                                      AND T.ClaveSimi = S.ClaveSimi
                        //                                                      AND T.Id_Venta = S.Id_Venta
                        //                                                );");
                    }
                }

                // ============================================================
                // 2) VENTAS PRODUCTOS
                // ============================================================
                if (VentasProductos)
                {
                    var ventasProductos = dto.VentasProductos?.Where(v => v.Id_Venta != null).ToList();
                    ventasProductos?.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasProductos";
                    if (ventasProductos?.Count > 0)
                    {
                        var sw2 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasProductos, @"CREATE TABLE #TempVentasProductos (
                                                                            FechaOperacion DATETIME NOT NULL,
                                                                            ClaveSimi CHAR(10) NOT NULL,
                                                                            Id_Venta INT NOT NULL,
                                                                            Codigo CHAR(10) NOT NULL,
                                                                            Id_ProductoSAT VARCHAR(20) NOT NULL,
                                                                            TipoOperacion INT NOT NULL,
                                                                            Producto VARCHAR(255) NOT NULL,
                                                                            NoPonderado BIT NOT NULL,
                                                                            Premio BIT NOT NULL,
                                                                            Combo BIT NOT NULL,
                                                                            Inventario BIT NOT NULL,
                                                                            Cantidad DECIMAL(10,2) NOT NULL,
                                                                            Precio DECIMAL(10,2) NOT NULL,
                                                                            IVA DECIMAL(10,2) NOT NULL,
                                                                            Descuento DECIMAL(10,2) NOT NULL,
                                                                            DescuentoPorciento DECIMAL(10,2) NOT NULL,
                                                                            IVA_Porciento DECIMAL(10,2) NOT NULL,
                                                                            IVA_Importe DECIMAL(10,2) NOT NULL,
                                                                            Presentacion VARCHAR(50) NULL,
                                                                            Nivel1 VARCHAR(50) NOT NULL,
                                                                            Nivel2 VARCHAR(50) NOT NULL,
                                                                            Nivel3 VARCHAR(50) NOT NULL,
                                                                            IdEmpresa INT NOT NULL
                                                                        );",
                                                                        "#TempVentasProductos",
                                                                        @"
                                                                        UPDATE T
                                                                        SET
                                                                            T.Id_ProductoSAT     = S.Id_ProductoSAT,
                                                                            T.TipoOperacion      = S.TipoOperacion,
                                                                            T.Producto           = S.Producto,
                                                                            T.NoPonderado        = S.NoPonderado,
                                                                            T.Premio             = S.Premio,
                                                                            T.Combo              = S.Combo,
                                                                            T.Inventario         = S.Inventario,
                                                                            T.Cantidad           = S.Cantidad,
                                                                            T.Precio             = S.Precio,
                                                                            T.IVA                = S.IVA,
                                                                            T.Descuento          = S.Descuento,
                                                                            T.DescuentoPorciento = S.DescuentoPorciento,
                                                                            T.IVA_Porciento      = S.IVA_Porciento,
                                                                            T.IVA_Importe        = S.IVA_Importe,
                                                                            T.Presentacion       = S.Presentacion,
                                                                            T.Nivel1             = S.Nivel1,
                                                                            T.Nivel2             = S.Nivel2,
                                                                            T.Nivel3             = S.Nivel3
                                                                        FROM VentasProductos T
                                                                        JOIN #TempVentasProductos S
                                                                          ON T.FechaOperacion = S.FechaOperacion
                                                                         AND T.ClaveSimi = S.ClaveSimi
                                                                         AND T.Id_Venta = S.Id_Venta
                                                                         AND T.Codigo = S.Codigo;

                                                                        INSERT INTO VentasProductos (
                                                                            FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa, Codigo,
                                                                            Id_ProductoSAT, TipoOperacion, Producto,
                                                                            NoPonderado, Premio, Combo, Inventario,
                                                                            Cantidad, Precio, IVA, Descuento,
                                                                            DescuentoPorciento, IVA_Porciento, IVA_Importe,
                                                                            Presentacion, Nivel1, Nivel2, Nivel3
                                                                        )
                                                                        SELECT S.FechaOperacion, S.ClaveSimi, S.Id_Venta, S.IdEmpresa, S.Codigo,
                                                                            S.Id_ProductoSAT, S.TipoOperacion, S.Producto,
                                                                            S.NoPonderado, S.Premio, S.Combo, S.Inventario,
                                                                            S.Cantidad, S.Precio, S.IVA, S.Descuento,
                                                                            S.DescuentoPorciento, S.IVA_Porciento, S.IVA_Importe,
                                                                            S.Presentacion, S.Nivel1, S.Nivel2, S.Nivel3
                                                                        FROM #TempVentasProductos S
                                                                        WHERE NOT EXISTS (
                                                                            SELECT 1
                                                                            FROM VentasProductos T
                                                                            WHERE T.FechaOperacion = S.FechaOperacion
                                                                              AND T.ClaveSimi = S.ClaveSimi
                                                                              AND T.Id_Venta = S.Id_Venta
                                                                              AND T.Codigo = S.Codigo
                                                                        );");


                        sw2.Stop();
                        if (IMP)
                            Logger.Important(
                                $"Proceso {nombreProceso} | Registros: {ventasProductos.Count} | Tiempo: {sw2.ElapsedMilliseconds} ms"
                            );
                    }
                }

                if (VentasImpuestos)
                {
                    // 3) VentasImpuestos
                    var ventasImpuestos = dto.VentasImpuestos?.Where(v => v.Id_Venta != null).ToList();
                    ventasImpuestos.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasImpuestos";

                    if (IMP)
                        Logger.Important($"Procesando VentasImpuestos ({ventasImpuestos?.Count ?? 0})...");

                    if (ventasImpuestos.Count > 0)
                    {
                        var sw3 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasImpuestos, @"CREATE TABLE #TempVentasImpuestos (
                                                                            FechaOperacion DATETIME NOT NULL,
                                                                            ClaveSimi CHAR(10) NOT NULL,
                                                                            Id_Venta INT NOT NULL,
                                                                            Impuesto VARCHAR(10) NOT NULL,
                                                                            TipoFactor VARCHAR(10) NOT NULL,
                                                                            TasaImpuesto NUMERIC(12,2) NOT NULL,
                                                                            ClaveSATImpuesto VARCHAR(10) NOT NULL,
                                                                            BaseImpuesto NUMERIC(12,2) NOT NULL,
                                                                            ImporteImpuesto NUMERIC(12,2) NOT NULL,
                                                                            TipoOperacion INT NOT NULL,
                                                                            IdEmpresa INT NOT NULL
                                                                    );",
                                                                    "#TempVentasImpuestos",
                                                                    @"INSERT INTO VentasImpuestos (
                                                                        FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa,
                                                                        Impuesto, TipoFactor, TasaImpuesto,
                                                                        ClaveSATImpuesto, BaseImpuesto, ImporteImpuesto, TipoOperacion
                                                                    )
                                                                    SELECT S.FechaOperacion, S.ClaveSimi, S.Id_Venta, S.IdEmpresa,
                                                                        S.Impuesto, S.TipoFactor, S.TasaImpuesto,
                                                                        S.ClaveSATImpuesto, S.BaseImpuesto, S.ImporteImpuesto, S.TipoOperacion
                                                                    FROM #TempVentasImpuestos S
                                                                    WHERE NOT EXISTS (
                                                                        SELECT 1
                                                                        FROM VentasImpuestos T
                                                                        WHERE T.FechaOperacion = S.FechaOperacion
                                                                          AND T.ClaveSimi = S.ClaveSimi
                                                                          AND T.Id_Venta = S.Id_Venta
                                                                          AND T.Impuesto = S.Impuesto
                                                                          AND T.TipoFactor = S.TipoFactor
                                                                          AND T.TasaImpuesto = S.TasaImpuesto
                                                                    );");
                        sw3.Stop();

                        if (IMP)
                            Logger.Important(
                                $"Proceso {nombreProceso} | Registros: {ventasImpuestos.Count} | Tiempo: {sw3.ElapsedMilliseconds} ms"
                            );
                    }
                }

                if (VentasImpuestosDetalle)
                {
                    // 4) VentasImpuestosDetalle
                    var ventasImpuestosDetalle = dto.VentasImpuestosDetalle?.Where(v => v.Id_Venta != null).ToList();
                    ventasImpuestosDetalle.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasImpuestosDetalle";

                    if (IMP)
                        Logger.Important($"Procesando VentasImpuestosDetalle ({ventasImpuestosDetalle?.Count ?? 0})...");

                    if (ventasImpuestosDetalle.Count > 0)
                    {
                        var sw4 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasImpuestosDetalle, @"CREATE TABLE #TempVentasImpuestosDetalle (
                                                                                    ClaveSimi CHAR(10) NOT NULL,
                                                                                    FechaOperacion DATETIME NOT NULL,
                                                                                    Id_Venta INT NOT NULL,
                                                                                    Id_Producto VARCHAR(10) NOT NULL,
                                                                                    Impuesto VARCHAR(10) NOT NULL,
                                                                                    ClaveImpuesto VARCHAR(10) NOT NULL,
                                                                                    TasaImpuesto NUMERIC(12,2) NOT NULL,
                                                                                    TipoFactor VARCHAR(10) NOT NULL,
                                                                                    Base NUMERIC(12,2) NOT NULL,
                                                                                    ImporteIVA NUMERIC(12,2) NOT NULL,
                                                                                    ImporteVenta NUMERIC(12,2) NOT NULL,
                                                                                    TipoOperacion INT NOT NULL,
                                                                                    IdEmpresa INT NOT NULL
                                                                            );",
                                                                            "#TempVentasImpuestosDetalle",
                                                                            @"
                                                                            INSERT INTO VentasImpuestosDetalle (
                                                                                ClaveSimi, FechaOperacion, Id_Venta, IdEmpresa,
                                                                                Id_Producto, Impuesto, ClaveImpuesto,
                                                                                TasaImpuesto, TipoFactor, Base,
                                                                                ImporteIVA, ImporteVenta, TipoOperacion
                                                                            )
                                                                            SELECT S.ClaveSimi, S.FechaOperacion, S.Id_Venta, S.IdEmpresa,
                                                                                S.Id_Producto, S.Impuesto, S.ClaveImpuesto,
                                                                                S.TasaImpuesto, S.TipoFactor, S.Base,
                                                                                S.ImporteIVA, S.ImporteVenta, S.TipoOperacion
                                                                            FROM #TempVentasImpuestosDetalle S
                                                                            WHERE NOT EXISTS (
                                                                                SELECT 1
                                                                                FROM VentasImpuestosDetalle T
                                                                                WHERE T.FechaOperacion = S.FechaOperacion
                                                                                  AND T.ClaveSimi = S.ClaveSimi
                                                                                  AND T.Id_Venta = S.Id_Venta
                                                                                  AND T.Id_Producto = S.Id_Producto
                                                                                  AND T.Impuesto = S.Impuesto
                                                                            );");
                        sw4.Stop();

                        if (IMP)
                            Logger.Important(
                                $"Proceso {nombreProceso} | Registros: {ventasImpuestosDetalle.Count} | Tiempo: {sw4.ElapsedMilliseconds} ms");
                    }
                }

                if (VentasDesgloseTotales)
                {
                    // 5) VentasDesgloceTotales
                    var ventasDesgloceTotales = dto.VentasDesgloceTotales?.Where(v => v.Id_Venta != null).ToList();
                    ventasDesgloceTotales.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasDesgloseTotales";

                    if (IMP)
                        Logger.Important($"Procesando VentasDesgloseTotales ({ventasDesgloceTotales?.Count ?? 0})...");

                    if (ventasDesgloceTotales.Count > 0)
                    {
                        var sw5 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasDesgloceTotales, @"CREATE TABLE #VentasDesgloseTotales (
                                                                            ClaveSimi CHAR(10) NOT NULL,
                                                                            FechaOperacion DATETIME NOT NULL,
                                                                            Id_Venta INT NOT NULL,
                                                                            PrecioSinIVA NUMERIC(12,2) NOT NULL,
                                                                            Importe NUMERIC(12,2) NOT NULL,
                                                                            Descuento NUMERIC(12,2) NOT NULL,
                                                                            Impuestos NUMERIC(12,2) NOT NULL,
                                                                            Total NUMERIC(12,2) NOT NULL,
                                                                            TipoOperacion INT NOT NULL,
                                                                            IdEmpresa INT NOT NULL
                                                                        );",
                                                                        "#VentasDesgloseTotales",
                                                                        @"
                                                                        INSERT INTO VentasDesgloseTotales (
                                                                            ClaveSimi, FechaOperacion, Id_Venta, IdEmpresa,
                                                                            PrecioSinIVA, Importe, Descuento,
                                                                            Impuestos, Total, TipoOperacion
                                                                        )
                                                                        SELECT S.ClaveSimi, S.FechaOperacion, S.Id_Venta, S.IdEmpresa,
                                                                            S.PrecioSinIVA, S.Importe, S.Descuento,
                                                                            S.Impuestos, S.Total, S.TipoOperacion
                                                                        FROM #VentasDesgloseTotales S
                                                                        WHERE NOT EXISTS (
                                                                            SELECT 1
                                                                            FROM VentasDesgloseTotales T
                                                                            WHERE T.FechaOperacion = S.FechaOperacion
                                                                              AND T.ClaveSimi = S.ClaveSimi
                                                                              AND T.Id_Venta = S.Id_Venta
                                                                        );");
                        sw5.Stop();

                        if (IMP)
                            Logger.Important(
                                $"Proceso {nombreProceso} | Registros: {ventasDesgloceTotales.Count} | Tiempo: {sw5.ElapsedMilliseconds} ms"
                            );
                    }
                }

                if (VentasImportesProductos)
                {
                    // 6) VentasImportesProductos
                    var ventasImportesProductos = dto.VentasImportesProductos?.Where(v => v.Id_Venta != null).ToList();
                    ventasImportesProductos.ForEach(x => x.IdEmpresa = idEmpresa);
                    nombreProceso = "VentasImportesProductos";

                    if (IMP)
                        Logger.Important($"Procesando VentasImportesProductos ({ventasImportesProductos?.Count ?? 0})...");

                    if (ventasImportesProductos.Count > 0)
                    {
                        var sw6 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasImportesProductos, @"CREATE TABLE #TempVentasImportesProductos (
                                                                                    FechaOperacion DATETIME NOT NULL,
                                                                                    ClaveSimi CHAR(10) NOT NULL,
                                                                                    Id_Venta INT NOT NULL,
                                                                                    Id_Producto VARCHAR(10) NOT NULL,
                                                                                    Precio NUMERIC(12,2) NOT NULL,
                                                                                    PrecioUnitarioNeto NUMERIC(18,4) NOT NULL,
                                                                                    Cantidad INT NOT NULL,
                                                                                    SubtotalNeto NUMERIC(18,4) NOT NULL,
                                                                                    SubtotalConImpuestos NUMERIC(18,4) NOT NULL,
                                                                                    DescuentoNeto NUMERIC(18,4) NOT NULL,
                                                                                    DescuentoConImpuestos NUMERIC(18,4) NOT NULL,
                                                                                    ImporteNeto NUMERIC(12,2) NOT NULL,
                                                                                    ImporteConImpuestos NUMERIC(12,2) NOT NULL,
                                                                                    ImpuestoCalculado NUMERIC(18,4) NOT NULL,
                                                                                    Total NUMERIC(12,2) NOT NULL,
                                                                                    TipoOperacion INT NOT NULL,
                                                                                    IdEmpresa INT NOT NULL
                                                                        );",
                                                                        "#TempVentasImportesProductos",
                                                                        @"INSERT INTO VentasImportesProductos (
                                                                            FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa,
                                                                            Id_Producto, Precio, PrecioUnitarioNeto,
                                                                            Cantidad, SubtotalNeto, SubtotalConImpuestos,
                                                                            DescuentoNeto, DescuentoConImpuestos,
                                                                            ImporteNeto, ImporteConImpuestos,
                                                                            ImpuestoCalculado, Total, TipoOperacion
                                                                        )
                                                                        SELECT S.FechaOperacion, S.ClaveSimi, S.Id_Venta, S.IdEmpresa,
                                                                            S.Id_Producto, S.Precio, S.PrecioUnitarioNeto,
                                                                            S.Cantidad, S.SubtotalNeto, S.SubtotalConImpuestos,
                                                                            S.DescuentoNeto, S.DescuentoConImpuestos,
                                                                            S.ImporteNeto, S.ImporteConImpuestos,
                                                                            S.ImpuestoCalculado, S.Total, S.TipoOperacion
                                                                        FROM #TempVentasImportesProductos S
                                                                        WHERE NOT EXISTS (
                                                                            SELECT 1
                                                                            FROM VentasImportesProductos T
                                                                            WHERE T.FechaOperacion = S.FechaOperacion
                                                                              AND T.ClaveSimi = S.ClaveSimi
                                                                              AND T.Id_Venta = S.Id_Venta
                                                                              AND T.Id_Producto = S.Id_Producto
                                                                        );");

                        sw6.Stop();

                        if (IMP)
                            Logger.Important(
                                $"Proceso {nombreProceso} | Registros: {ventasImportesProductos.Count} | Tiempo: {sw6.ElapsedMilliseconds} ms"
                            );
                    }
                }

                if (VentasVendedorCuotas)
                {

                    // 7) VentasVendedorCuotas
                    nombreProceso = "VentasVendedorCuotas";

                    if (IMP)
                        Logger.Important($"Procesando VentasVendedorCuotas ({dto.VentasVendedorCuotas?.Count ?? 0})...");
                    var ventasVendedorCuotasConSucursal = dto.VentasVendedorCuotas
                        .Select(v => new VentasVendedorCuotasDto
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

                    if (ventasVendedorCuotasConSucursal.Count > 0)
                    {
                        var sw7 = Stopwatch.StartNew();
                        await BulkExecuteAsync(connection, ventasVendedorCuotasConSucursal, @"CREATE TABLE #TempVentasVendedorCuotas (
                                                                                            ClaveSimi VARCHAR(6) NOT NULL,
                                                                                            Fecha DATETIME NOT NULL,
                                                                                            IdVendedor VARCHAR(10) NOT NULL,
                                                                                            Nombre VARCHAR(200) NOT NULL,
                                                                                            ImporteVenta DECIMAL(12,2) NOT NULL,
                                                                                            Transaccionesventa INT NOT NULL,
                                                                                            PorcVenta DECIMAL(12,2) NOT NULL,
                                                                                            ImporteNaturistas DECIMAL(12,2) NOT NULL,
                                                                                            PorcNaturistas DECIMAL(12,2) NOT NULL,
                                                                                            ImporteNocturno DECIMAL(12,2) NOT NULL,
                                                                                            MontoDescuento DECIMAL(12,2) NOT NULL,
                                                                                            Menudeos DECIMAL(12,2) NOT NULL,
                                                                                            MontoIva DECIMAL(12,2) NOT NULL,
                                                                                            IdEmpresa INT NOT NULL
                                                                                );",
                                                                                "#TempVentasVendedorCuotas",
                                                                                @"UPDATE T
                                                                                SET
                                                                                    T.Nombre = S.Nombre,
                                                                                    T.ImporteVenta = S.ImporteVenta,
                                                                                    T.Transaccionesventa = S.Transaccionesventa,
                                                                                    T.PorcVenta = S.PorcVenta,
                                                                                    T.ImporteNaturistas = S.ImporteNaturistas,
                                                                                    T.PorcNaturistas = S.PorcNaturistas,
                                                                                    T.ImporteNocturno = S.ImporteNocturno,
                                                                                    T.MontoDescuento = S.MontoDescuento,
                                                                                    T.Menudeos = S.Menudeos,
                                                                                    T.MontoIva = S.MontoIva
                                                                                FROM VentasVendedorCuotas T
                                                                                JOIN #TempVentasVendedorCuotas S
                                                                                  ON T.ClaveSimi = S.ClaveSimi
                                                                                 AND T.Fecha = S.Fecha
                                                                                 AND T.IdVendedor = S.IdVendedor;

                                                                                INSERT INTO VentasVendedorCuotas (
                                                                                    ClaveSimi, Fecha, IdVendedor, IdEmpresa,
                                                                                    Nombre, ImporteVenta, Transaccionesventa,
                                                                                    PorcVenta, ImporteNaturistas, PorcNaturistas,
                                                                                    ImporteNocturno, MontoDescuento, Menudeos, MontoIva
                                                                                )
                                                                                SELECT S.ClaveSimi, S.Fecha, S.IdVendedor, S.IdEmpresa,
                                                                                    S.Nombre, S.ImporteVenta, S.Transaccionesventa,
                                                                                    S.PorcVenta, S.ImporteNaturistas, S.PorcNaturistas,
                                                                                    S.ImporteNocturno, S.MontoDescuento, S.Menudeos, S.MontoIva
                                                                                FROM #TempVentasVendedorCuotas S
                                                                                WHERE NOT EXISTS (
                                                                                    SELECT 1
                                                                                    FROM VentasVendedorCuotas T
                                                                                    WHERE T.ClaveSimi = S.ClaveSimi
                                                                                      AND T.Fecha = S.Fecha
                                                                                      AND T.IdVendedor = S.IdVendedor
                                                                                );");

                        sw7.Stop();

                        if (IMP)
                            Logger.Important(
                                $"Proceso {nombreProceso} | Registros: {ventasVendedorCuotasConSucursal.Count} | Tiempo: {sw7.ElapsedMilliseconds} ms"
                            );
                    }
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


        private async Task BulkExecuteAsync<T>(SqlConnection connection, IEnumerable<T> data, string tempTableSql, string tempTableName, string executionSql, int batchSizeOverride = -1, int maxRetries = 3)
        {
            if (data == null) return;

            var list = data as IList<T> ?? data.ToList();
            if (!list.Any()) return;
            int effectiveBatch = batchSizeOverride > 0 ? batchSizeOverride : batchSize;
            int attempt = 0;
            while (true)
            {
                try
                {
                    using (var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS {tempTableName};", connection))
                    {
                        await dropCmd.ExecuteNonQueryAsync();
                    }
                    using (var createCmd = new SqlCommand(tempTableSql, connection))
                    {
                        createCmd.CommandTimeout = 120;
                        await createCmd.ExecuteNonQueryAsync();
                    }

                    var table = ToDataTable(list);
                    int totalRows = table.Rows.Count;

                    for (int i = 0; i < totalRows; i += effectiveBatch)
                    {
                        var rows = table.AsEnumerable()
                                        .Skip(i)
                                        .Take(effectiveBatch)
                                        .CopyToDataTable();

                        using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null))
                        {
                            bulk.DestinationTableName = tempTableName;
                            bulk.BulkCopyTimeout = 0;
                            bulk.BatchSize = effectiveBatch;
                            bulk.EnableStreaming = true;

                            await bulk.WriteToServerAsync(rows);
                        }
                    }

                    using (var execCmd = new SqlCommand(executionSql, connection))
                    {
                        execCmd.CommandTimeout = 600;
                        await execCmd.ExecuteNonQueryAsync();
                    }

                    using (var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS {tempTableName};", connection))
                    {
                        await dropCmd.ExecuteNonQueryAsync();
                    }
                    break;
                }
                catch (SqlException ex) when (ex.Number == 1205 && attempt < maxRetries)
                {
                    attempt++;

                    int delay = 200 * attempt; 
                    await Task.Delay(delay);

                    if (attempt >= maxRetries)
                        throw;
                }
            }
        }





        //public async Task SincronizaSetDeTransmisionesSQLServerUltimo(ProcesosOnLine data)
        //{
        //    var conexionCacheService = new ConexionCacheService(Configuration);
        //    var conexionEmpresa = await conexionCacheService.GetConexionSqlServerAsync(data.Sucursal);

        //    if (conexionEmpresa == null)
        //    {
        //        Logger.Warning($"No se encontró configuración para ClaveSimi: {data.Sucursal}");
        //        return;
        //    }

        //    using var connection = new SqlConnection(conexionEmpresa.ConnectionString);
        //    Logger.Important($"{conexionEmpresa.ConnectionString}");


        //    var nombreProceso = string.Empty;
        //    var idEmpresa = conexionEmpresa.IdEmpresa;

        //    if (IMP)
        //        Logger.Important($"Id Empresa a procesar: {idEmpresa.ToString()}, proceso: {data.NombreProceso}");

        //    try
        //    {
        //        await connection.OpenAsync();

        //        if (IMP)
        //            Logger.Important($"Iniciando SincronizaSetDeTransmisionesSQLServer - Sucursal: {data.Sucursal}");
        //        var dto = JsonSerializer.Deserialize<SalesDataDto>(data.Json) ?? new SalesDataDto();

        //        if (Ventas)
        //        {
        //            //// 1) Ventas
        //            var ventasValidas = dto.Ventas?.Where(v => v.Id_Venta != null).ToList();
        //            ventasValidas.ForEach(x => x.IdEmpresa = idEmpresa);
        //            nombreProceso = "Ventas";

        //            if (IMP)
        //                Logger.Important($"Procesando Ventas ({ventasValidas?.Count ?? 0})...");


        //            if (ventasValidas.Count > 0)
        //            {
        //                var sw = Stopwatch.StartNew();
        //                await BulkMergeAsync(connection, ventasValidas, @"
        //                                                        CREATE TABLE #TempVentas (
        //                                                            FechaOperacion DATETIME NOT NULL,
        //                                                            ClaveSimi CHAR(10) NOT NULL,
        //                                                            Id_Venta INT NOT NULL,
        //                                                            id_usuario_venta VARCHAR(50) NOT NULL,
        //                                                            Empleado VARCHAR(100) NOT NULL,
        //                                                            idRegistradora INT NOT NULL,
        //                                                            idRegistradoraVenta INT NOT NULL,
        //                                                            idRegistradoraCobro INT NOT NULL,
        //                                                            TipoOperacion INT NOT NULL,
        //                                                            FechaHoraVenta DATETIME NOT NULL,
        //                                                            TipoVenta INT NOT NULL,
        //                                                            IdEmpresa INT NOT NULL
        //                                                        );
        //                                                        ",
        //                                                                    "#TempVentas",
        //                                                                    @"
        //                                                        MERGE INTO Ventas AS target
        //                                                        USING #TempVentas AS source
        //                                                        ON target.FechaOperacion = source.FechaOperacion
        //                                                           AND target.ClaveSimi = source.ClaveSimi
        //                                                           AND target.Id_Venta = source.Id_Venta
        //                                                        WHEN NOT MATCHED THEN
        //                                                            INSERT (FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa, id_usuario_venta, Empleado,
        //                                                                    idRegistradora, idRegistradoraVenta, idRegistradoraCobro, TipoOperacion,
        //                                                                    FechaHoraVenta, TipoVenta)
        //                                                            VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.IdEmpresa, source.id_usuario_venta, source.Empleado,
        //                                                                    source.idRegistradora, source.idRegistradoraVenta, source.idRegistradoraCobro, source.TipoOperacion,
        //                                                                    source.FechaHoraVenta, source.TipoVenta);");

        //                sw.Stop();

        //                if (IMP)
        //                    Logger.Important(
        //                        $"Proceso {nombreProceso} | Registros: {ventasValidas.Count} | Tiempo: {sw.ElapsedMilliseconds} ms"
        //                    );
        //            }
        //        }



        //        if (VentasProductos)
        //        {
        //            // 2) VentasProductos
        //            var ventasProductos = dto.VentasProductos?.Where(v => v.Id_Venta != null).ToList();
        //            ventasProductos.ForEach(x => x.IdEmpresa = idEmpresa);
        //            nombreProceso = "VentasProductos";


        //            if (IMP)
        //                Logger.Important($"Procesando VentasProductos ({ventasProductos?.Count ?? 0})...");

        //            if (ventasProductos.Count > 0)
        //            {
        //                var sw2 = Stopwatch.StartNew();
        //                await BulkMergeAsync(connection, ventasProductos, $@"
        //                                                            CREATE TABLE #TempVentasProductos (
        //                                                                FechaOperacion DATETIME NOT NULL,
        //                                                                ClaveSimi CHAR(10) NOT NULL,
        //                                                                Id_Venta INT NOT NULL,
        //                                                                Codigo CHAR(10) NOT NULL,
        //                                                                Id_ProductoSAT VARCHAR(20) NOT NULL,
        //                                                                TipoOperacion INT NOT NULL,
        //                                                                Producto VARCHAR(255) NOT NULL,
        //                                                                NoPonderado BIT NOT NULL,
        //                                                                Premio BIT NOT NULL,
        //                                                                Combo BIT NOT NULL,
        //                                                                Inventario BIT NOT NULL,
        //                                                                Cantidad DECIMAL(10,2) NOT NULL,
        //                                                                Precio DECIMAL(10,2) NOT NULL,
        //                                                                IVA DECIMAL(10,2) NOT NULL,
        //                                                                Descuento DECIMAL(10,2) NOT NULL,
        //                                                                DescuentoPorciento DECIMAL(10,2) NOT NULL,
        //                                                                IVA_Porciento DECIMAL(10,2) NOT NULL,
        //                                                                IVA_Importe DECIMAL(10,2) NOT NULL,
        //                                                                Presentacion VARCHAR(50) NULL,
        //                                                                Nivel1 VARCHAR(50) NOT NULL,
        //                                                                Nivel2 VARCHAR(50) NOT NULL,
        //                                                                Nivel3 VARCHAR(50) NOT NULL,
        //                                                                IdEmpresa INT NOT NULL
        //                                                            );",
        //                                                                            "#TempVentasProductos",
        //                                                                            $@"
        //                                                            MERGE INTO VentasProductos AS target
        //                                                            USING #TempVentasProductos AS source
        //                                                            ON target.FechaOperacion = source.FechaOperacion
        //                                                               AND target.ClaveSimi = source.ClaveSimi
        //                                                               AND target.Id_Venta = source.Id_Venta
        //                                                               AND target.Codigo = source.Codigo
        //                                                            WHEN MATCHED THEN
        //                                                                UPDATE SET
        //                                                                    target.Id_ProductoSAT     = source.Id_ProductoSAT,
        //                                                                    target.TipoOperacion      = source.TipoOperacion,
        //                                                                    target.Producto           = source.Producto,
        //                                                                    target.NoPonderado        = source.NoPonderado,
        //                                                                    target.Premio             = source.Premio,
        //                                                                    target.Combo              = source.Combo,
        //                                                                    target.Inventario         = source.Inventario,
        //                                                                    target.Cantidad           = source.Cantidad,
        //                                                                    target.Precio             = source.Precio,
        //                                                                    target.IVA                = source.IVA,
        //                                                                    target.Descuento          = source.Descuento,
        //                                                                    target.DescuentoPorciento = source.DescuentoPorciento,
        //                                                                    target.IVA_Porciento      = source.IVA_Porciento,
        //                                                                    target.IVA_Importe        = source.IVA_Importe,
        //                                                                    target.Presentacion       = source.Presentacion,
        //                                                                    target.Nivel1             = source.Nivel1,
        //                                                                    target.Nivel2             = source.Nivel2,
        //                                                                    target.Nivel3             = source.Nivel3
        //                                                            WHEN NOT MATCHED THEN
        //                                                                INSERT (FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa, Codigo, Id_ProductoSAT, TipoOperacion, Producto,
        //                                                                        NoPonderado, Premio, Combo, Inventario, Cantidad, Precio, IVA, Descuento,
        //                                                                        DescuentoPorciento, IVA_Porciento, IVA_Importe, Presentacion, Nivel1, Nivel2, Nivel3)
        //                                                                VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.IdEmpresa, source.Codigo, source.Id_ProductoSAT, source.TipoOperacion, source.Producto,
        //                                                                        source.NoPonderado, source.Premio, source.Combo, source.Inventario, source.Cantidad, source.Precio, source.IVA, source.Descuento,
        //                                                                        source.DescuentoPorciento, source.IVA_Porciento, source.IVA_Importe, source.Presentacion, source.Nivel1, source.Nivel2, source.Nivel3);");


        //                sw2.Stop();

        //                if (IMP)
        //                    Logger.Important(
        //                        $"Proceso {nombreProceso} | Registros: {ventasProductos.Count} | Tiempo: {sw2.ElapsedMilliseconds} ms"
        //                    );

        //            }
        //        }

        //        if (VentasImpuestos)
        //        {
        //            // 3) VentasImpuestos
        //            var ventasImpuestos = dto.VentasImpuestos?.Where(v => v.Id_Venta != null).ToList();
        //            ventasImpuestos.ForEach(x => x.IdEmpresa = idEmpresa);
        //            nombreProceso = "VentasImpuestos";

        //            if (IMP)
        //                Logger.Important($"Procesando VentasImpuestos ({ventasImpuestos?.Count ?? 0})...");

        //            if (ventasImpuestos.Count > 0)
        //            {
        //                var sw3 = Stopwatch.StartNew();
        //                await BulkMergeAsync(connection, ventasImpuestos, $@"
        //                                                            CREATE TABLE #TempVentasImpuestos (
        //                                                                FechaOperacion DATETIME NOT NULL,
        //                                                                ClaveSimi CHAR(10) NOT NULL,
        //                                                                Id_Venta INT NOT NULL,
        //                                                                Impuesto VARCHAR(10) NOT NULL,
        //                                                                TipoFactor VARCHAR(10) NOT NULL,
        //                                                                TasaImpuesto NUMERIC(12,2) NOT NULL,
        //                                                                ClaveSATImpuesto VARCHAR(10) NOT NULL,
        //                                                                BaseImpuesto NUMERIC(12,2) NOT NULL,
        //                                                                ImporteImpuesto NUMERIC(12,2) NOT NULL,
        //                                                                TipoOperacion INT NOT NULL,
        //                                                                IdEmpresa INT NOT NULL
        //                                                            );",
        //                                                                            "#TempVentasImpuestos",
        //                                                                            $@"
        //                                                            MERGE INTO VentasImpuestos AS target
        //                                                            USING #TempVentasImpuestos AS source
        //                                                            ON target.FechaOperacion = source.FechaOperacion
        //                                                               AND target.ClaveSimi = source.ClaveSimi
        //                                                               AND target.Id_Venta = source.Id_Venta
        //                                                               AND target.Impuesto = source.Impuesto
        //                                                               AND target.TipoFactor = source.TipoFactor
        //                                                               AND target.TasaImpuesto = source.TasaImpuesto
        //                                                            WHEN NOT MATCHED THEN
        //                                                                INSERT (FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa, Impuesto, TipoFactor, TasaImpuesto, ClaveSATImpuesto, BaseImpuesto, ImporteImpuesto, TipoOperacion)
        //                                                                VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.IdEmpresa, source.Impuesto, source.TipoFactor, source.TasaImpuesto, source.ClaveSATImpuesto, source.BaseImpuesto, source.ImporteImpuesto, source.TipoOperacion);");


        //                sw3.Stop();

        //                if (IMP)
        //                    Logger.Important(
        //                        $"Proceso {nombreProceso} | Registros: {ventasImpuestos.Count} | Tiempo: {sw3.ElapsedMilliseconds} ms"
        //                    );
        //            }
        //        }


        //        if (VentasImpuestosDetalle)
        //        {
        //            // 4) VentasImpuestosDetalle
        //            var ventasImpuestosDetalle = dto.VentasImpuestosDetalle?.Where(v => v.Id_Venta != null).ToList();
        //            ventasImpuestosDetalle.ForEach(x => x.IdEmpresa = idEmpresa);
        //            nombreProceso = "VentasImpuestosDetalle";

        //            if (IMP)
        //                Logger.Important($"Procesando VentasImpuestosDetalle ({ventasImpuestosDetalle?.Count ?? 0})...");

        //            if (ventasImpuestosDetalle.Count > 0)
        //            {
        //                var sw4 = Stopwatch.StartNew();
        //                await BulkMergeAsync(connection, ventasImpuestosDetalle, $@"
        //                                                                    CREATE TABLE #TempVentasImpuestosDetalle (
        //                                                                        ClaveSimi CHAR(10) NOT NULL,
        //                                                                        FechaOperacion DATETIME NOT NULL,
        //                                                                        Id_Venta INT NOT NULL,
        //                                                                        Id_Producto VARCHAR(10) NOT NULL,
        //                                                                        Impuesto VARCHAR(10) NOT NULL,
        //                                                                        ClaveImpuesto VARCHAR(10) NOT NULL,
        //                                                                        TasaImpuesto NUMERIC(12,2) NOT NULL,
        //                                                                        TipoFactor VARCHAR(10) NOT NULL,
        //                                                                        Base NUMERIC(12,2) NOT NULL,
        //                                                                        ImporteIVA NUMERIC(12,2) NOT NULL,
        //                                                                        ImporteVenta NUMERIC(12,2) NOT NULL,
        //                                                                        TipoOperacion INT NOT NULL,
        //                                                                        IdEmpresa INT NOT NULL
        //                                                                    );",
        //                                                                                    "#TempVentasImpuestosDetalle",
        //                                                                                    $@"
        //                                                                    MERGE INTO VentasImpuestosDetalle AS target
        //                                                                    USING #TempVentasImpuestosDetalle AS source
        //                                                                    ON target.FechaOperacion = source.FechaOperacion
        //                                                                       AND target.ClaveSimi = source.ClaveSimi
        //                                                                       AND target.Id_Venta = source.Id_Venta
        //                                                                       AND target.Id_Producto = source.Id_Producto
        //                                                                       AND target.Impuesto = source.Impuesto
        //                                                                    WHEN NOT MATCHED THEN
        //                                                                        INSERT (ClaveSimi, FechaOperacion, Id_Venta, IdEmpresa, Id_Producto, Impuesto, ClaveImpuesto, TasaImpuesto, TipoFactor, Base, ImporteIVA, ImporteVenta, TipoOperacion)
        //                                                                        VALUES (source.ClaveSimi, source.FechaOperacion, source.Id_Venta, source.IdEmpresa, source.Id_Producto, source.Impuesto, source.ClaveImpuesto, source.TasaImpuesto, source.TipoFactor, source.Base, source.ImporteIVA, source.ImporteVenta, source.TipoOperacion);");

        //                sw4.Stop();

        //                if (IMP)
        //                    Logger.Important(
        //                        $"Proceso {nombreProceso} | Registros: {ventasImpuestosDetalle.Count} | Tiempo: {sw4.ElapsedMilliseconds} ms"
        //                    );
        //            }
        //        }

        //        if (VentasDesgloseTotales)
        //        {
        //            // 5) VentasDesgloceTotales
        //            var ventasDesgloceTotales = dto.VentasDesgloceTotales?.Where(v => v.Id_Venta != null).ToList();
        //            ventasDesgloceTotales.ForEach(x => x.IdEmpresa = idEmpresa);
        //            nombreProceso = "VentasDesgloseTotales";

        //            if (IMP)
        //                Logger.Important($"Procesando VentasDesgloseTotales ({ventasDesgloceTotales?.Count ?? 0})...");

        //            if (ventasDesgloceTotales.Count > 0)
        //            {
        //                var sw5 = Stopwatch.StartNew();
        //                await BulkMergeAsync(connection, ventasDesgloceTotales, $@"
        //                                                                CREATE TABLE #VentasDesgloseTotales (
        //                                                                    ClaveSimi CHAR(10) NOT NULL,
        //                                                                    FechaOperacion DATETIME NOT NULL,
        //                                                                    Id_Venta INT NOT NULL,
        //                                                                    PrecioSinIVA NUMERIC(12,2) NOT NULL,
        //                                                                    Importe NUMERIC(12,2) NOT NULL,
        //                                                                    Descuento NUMERIC(12,2) NOT NULL,
        //                                                                    Impuestos NUMERIC(12,2) NOT NULL,
        //                                                                    Total NUMERIC(12,2) NOT NULL,
        //                                                                    TipoOperacion INT NOT NULL,
        //                                                                    IdEmpresa INT NOT NULL
        //                                                                );",
        //                                                                                "#VentasDesgloseTotales",
        //                                                                                $@"
        //                                                                MERGE INTO VentasDesgloseTotales AS target
        //                                                                USING #VentasDesgloseTotales AS source
        //                                                                ON target.FechaOperacion = source.FechaOperacion
        //                                                                   AND target.ClaveSimi = source.ClaveSimi
        //                                                                   AND target.Id_Venta = source.Id_Venta
        //                                                                WHEN NOT MATCHED THEN
        //                                                                    INSERT (ClaveSimi, FechaOperacion, Id_Venta, IdEmpresa, PrecioSinIVA, Importe, Descuento, Impuestos, Total, TipoOperacion)
        //                                                                    VALUES (source.ClaveSimi, source.FechaOperacion, source.Id_Venta, source.IdEmpresa, source.PrecioSinIVA, source.Importe, source.Descuento, source.Impuestos, source.Total, source.TipoOperacion);");

        //                sw5.Stop();

        //                if (IMP)
        //                    Logger.Important(
        //                        $"Proceso {nombreProceso} | Registros: {ventasDesgloceTotales.Count} | Tiempo: {sw5.ElapsedMilliseconds} ms"
        //                    );
        //            }
        //        }

        //        if (VentasImportesProductos)
        //        {

        //            // 6) VentasImportesProductos
        //            var ventasImportesProductos = dto.VentasImportesProductos?.Where(v => v.Id_Venta != null).ToList();
        //            ventasImportesProductos.ForEach(x => x.IdEmpresa = idEmpresa);
        //            nombreProceso = "VentasImportesProductos";

        //            if (IMP)
        //                Logger.Important($"Procesando VentasImportesProductos ({ventasImportesProductos?.Count ?? 0})...");

        //            if (ventasImportesProductos.Count > 0)
        //            {
        //                var sw6 = Stopwatch.StartNew();
        //                await BulkMergeAsync(connection, ventasImportesProductos, $@"
        //                                                                    CREATE TABLE #TempVentasImportesProductos (
        //                                                                        FechaOperacion DATETIME NOT NULL,
        //                                                                        ClaveSimi CHAR(10) NOT NULL,
        //                                                                        Id_Venta INT NOT NULL,
        //                                                                        Id_Producto VARCHAR(10) NOT NULL,
        //                                                                        Precio NUMERIC(12,2) NOT NULL,
        //                                                                        PrecioUnitarioNeto NUMERIC(18,4) NOT NULL,
        //                                                                        Cantidad INT NOT NULL,
        //                                                                        SubtotalNeto NUMERIC(18,4) NOT NULL,
        //                                                                        SubtotalConImpuestos NUMERIC(18,4) NOT NULL,
        //                                                                        DescuentoNeto NUMERIC(18,4) NOT NULL,
        //                                                                        DescuentoConImpuestos NUMERIC(18,4) NOT NULL,
        //                                                                        ImporteNeto NUMERIC(12,2) NOT NULL,
        //                                                                        ImporteConImpuestos NUMERIC(12,2) NOT NULL,
        //                                                                        ImpuestoCalculado NUMERIC(18,4) NOT NULL,
        //                                                                        Total NUMERIC(12,2) NOT NULL,
        //                                                                        TipoOperacion INT NOT NULL,
        //                                                                        IdEmpresa INT NOT NULL
        //                                                                    );",
        //                                                                                    "#TempVentasImportesProductos",
        //                                                                                    $@"
        //                                                                    MERGE INTO VentasImportesProductos AS target
        //                                                                    USING #TempVentasImportesProductos AS source
        //                                                                    ON target.FechaOperacion = source.FechaOperacion
        //                                                                       AND target.ClaveSimi = source.ClaveSimi
        //                                                                       AND target.Id_Venta = source.Id_Venta
        //                                                                       AND target.Id_Producto = source.Id_Producto
        //                                                                    WHEN NOT MATCHED THEN
        //                                                                        INSERT (FechaOperacion, ClaveSimi, Id_Venta, IdEmpresa, Id_Producto, Precio, PrecioUnitarioNeto, Cantidad,
        //                                                                                SubtotalNeto, SubtotalConImpuestos, DescuentoNeto, DescuentoConImpuestos, ImporteNeto, ImporteConImpuestos,
        //                                                                                ImpuestoCalculado, Total, TipoOperacion)
        //                                                                        VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.IdEmpresa, source.Id_Producto, source.Precio, source.PrecioUnitarioNeto, source.Cantidad,
        //                                                                                source.SubtotalNeto, source.SubtotalConImpuestos, source.DescuentoNeto, source.DescuentoConImpuestos, source.ImporteNeto, source.ImporteConImpuestos,
        //                                                                                source.ImpuestoCalculado, source.Total, source.TipoOperacion);");

        //                sw6.Stop();

        //                if (IMP)
        //                    Logger.Important(
        //                        $"Proceso {nombreProceso} | Registros: {ventasImportesProductos.Count} | Tiempo: {sw6.ElapsedMilliseconds} ms"
        //                    );
        //            }
        //        }

        //        if (VentasVendedorCuotas)
        //        {

        //            // 7) VentasVendedorCuotas
        //            nombreProceso = "VentasVendedorCuotas";

        //            if (IMP)
        //                Logger.Important($"Procesando VentasVendedorCuotas ({dto.VentasVendedorCuotas?.Count ?? 0})...");
        //            var ventasVendedorCuotasConSucursal = dto.VentasVendedorCuotas
        //                .Select(v => new VentasVendedorCuotasDto
        //                {
        //                    ClaveSimi = data.Sucursal,
        //                    Fecha = v.Fecha,
        //                    IdVendedor = v.IdVendedor,
        //                    Nombre = v.Nombre,
        //                    ImporteVenta = v.ImporteVenta,
        //                    Transaccionesventa = v.Transaccionesventa,
        //                    PorcVenta = v.PorcVenta,
        //                    ImporteNaturistas = v.ImporteNaturistas,
        //                    PorcNaturistas = v.PorcNaturistas,
        //                    ImporteNocturno = v.ImporteNocturno,
        //                    MontoDescuento = v.MontoDescuento,
        //                    Menudeos = v.Menudeos,
        //                    MontoIva = v.MontoIva,
        //                    IdEmpresa = idEmpresa
        //                }).ToList();

        //            if (ventasVendedorCuotasConSucursal.Count > 0)
        //            {
        //                var sw7 = Stopwatch.StartNew();
        //                await BulkMergeAsync(connection, ventasVendedorCuotasConSucursal, @"
        //                                                                            CREATE TABLE #TempVentasVendedorCuotas (
        //                                                                                ClaveSimi VARCHAR(6) NOT NULL,
        //                                                                                Fecha DATETIME NOT NULL,
        //                                                                                IdVendedor VARCHAR(10) NOT NULL,
        //                                                                                Nombre VARCHAR(200) NOT NULL,
        //                                                                                ImporteVenta DECIMAL(12,2) NOT NULL,
        //                                                                                Transaccionesventa INT NOT NULL,
        //                                                                                PorcVenta DECIMAL(12,2) NOT NULL,
        //                                                                                ImporteNaturistas DECIMAL(12,2) NOT NULL,
        //                                                                                PorcNaturistas DECIMAL(12,2) NOT NULL,
        //                                                                                ImporteNocturno DECIMAL(12,2) NOT NULL,
        //                                                                                MontoDescuento DECIMAL(12,2) NOT NULL,
        //                                                                                Menudeos DECIMAL(12,2) NOT NULL,
        //                                                                                MontoIva DECIMAL(12,2) NOT NULL,
        //                                                                                IdEmpresa INT NOT NULL
        //                                                                            );",
        //                                                                                            "#TempVentasVendedorCuotas",
        //                                                                                            @"
        //                                                                            MERGE INTO VentasVendedorCuotas AS target
        //                                                                            USING #TempVentasVendedorCuotas AS source
        //                                                                            ON target.ClaveSimi = source.ClaveSimi
        //                                                                               AND target.Fecha = source.Fecha
        //                                                                               AND target.IdVendedor = source.IdVendedor
        //                                                                            WHEN MATCHED THEN UPDATE SET
        //                                                                                target.Nombre = source.Nombre,
        //                                                                                target.ImporteVenta = source.ImporteVenta,
        //                                                                                target.Transaccionesventa = source.Transaccionesventa,
        //                                                                                target.PorcVenta = source.PorcVenta,
        //                                                                                target.ImporteNaturistas = source.ImporteNaturistas,
        //                                                                                target.PorcNaturistas = source.PorcNaturistas,
        //                                                                                target.ImporteNocturno = source.ImporteNocturno,
        //                                                                                target.MontoDescuento = source.MontoDescuento,
        //                                                                                target.Menudeos = source.Menudeos,
        //                                                                                target.MontoIva = source.MontoIva
        //                                                                            WHEN NOT MATCHED THEN
        //                                                                                INSERT (ClaveSimi, Fecha, IdVendedor, IdEmpresa, Nombre, ImporteVenta, Transaccionesventa, PorcVenta,
        //                                                                                        ImporteNaturistas, PorcNaturistas, ImporteNocturno, MontoDescuento, Menudeos, MontoIva)
        //                                                                                VALUES (source.ClaveSimi, source.Fecha, source.IdVendedor, source.IdEmpresa, source.Nombre, source.ImporteVenta, source.Transaccionesventa, source.PorcVenta,
        //                                                                                        source.ImporteNaturistas, source.PorcNaturistas, source.ImporteNocturno, source.MontoDescuento, source.Menudeos, source.MontoIva);");
        //                sw7.Stop();

        //                if (IMP)
        //                    Logger.Important(
        //                        $"Proceso {nombreProceso} | Registros: {ventasVendedorCuotasConSucursal.Count} | Tiempo: {sw7.ElapsedMilliseconds} ms"
        //                    );
        //            }
        //        }

        //        DateTime? fechaValida = null;
        //        // Aquí viene la fecha de la sucursal.
        //        if (data.TicketsFaltantes != null)
        //        {
        //            if (DateTime.TryParse(data.TicketsFaltantes.ToString(), out DateTime parsedFecha))
        //            {
        //                fechaValida = parsedFecha;
        //            }
        //        }

        //        string sql;
        //        object parametros;

        //        if (fechaValida.HasValue)
        //        {
        //            // Si la fecha es válida, la usamos
        //            sql = @"
        //                    MERGE SucursalTransmision AS target
        //                    USING (SELECT @ClaveSimi AS ClaveSimi) AS source
        //                    ON target.ClaveSimi = source.ClaveSimi

        //                    WHEN MATCHED THEN
        //                        UPDATE SET FechaHoraTransmision = @FechaHoraTransmision

        //                    WHEN NOT MATCHED THEN
        //                        INSERT (ClaveSimi, FechaHoraTransmision)
        //                        VALUES (@ClaveSimi, @FechaHoraTransmision);";

        //            parametros = new
        //            {
        //                ClaveSimi = data.Sucursal,
        //                FechaHoraTransmision = fechaValida.Value
        //            };
        //        }
        //        else
        //        {
        //            sql = @"
        //                    MERGE SucursalTransmision AS target
        //                    USING (SELECT @ClaveSimi AS ClaveSimi) AS source
        //                    ON target.ClaveSimi = source.ClaveSimi

        //                    WHEN MATCHED THEN
        //                        UPDATE SET FechaHoraTransmision = GETDATE()

        //                    WHEN NOT MATCHED THEN
        //                        INSERT (ClaveSimi, FechaHoraTransmision)
        //                        VALUES (@ClaveSimi, GETDATE());
        //                ";

        //            parametros = new
        //            {
        //                ClaveSimi = data.Sucursal
        //            };
        //        }
        //        await connection.ExecuteAsync(sql, parametros);

        //        if (IMP)
        //            Logger.Important("Finalizando SQL Server");
        //        connection.Dispose();


        //        //MySQL
        //        await _mysqlSemaphore.WaitAsync();
        //        try
        //        {
        //            var connectionString = Configuration.GetConnectionString("DbFacturaRealOrquestador");
        //            using (var conn = new MySqlConnection(connectionString))
        //            {
        //                await conn.OpenAsync();

        //                // Ejecutar SP con Dapper en la MISMA conexión
        //                var parameter = new DynamicParameters();
        //                parameter.Add("@pSucursal", data.Sucursal, DbType.String);
        //                parameter.Add("@pVersion1", data.Ver1, DbType.String);
        //                parameter.Add("@pVersion2", data.Ver2, DbType.String);
        //                parameter.Add("@pVersion3", data.Ver3, DbType.String);
        //                parameter.Add("@pVersion4", ".", DbType.String);
        //                parameter.Add("@pVersion5", ".", DbType.String);
        //                parameter.Add("@pVersion6", ".", DbType.String);

        //                await conn.ExecuteAsync(
        //                    "usp_ActualizaSucursalesEnLinea",
        //                    parameter,
        //                    commandType: CommandType.StoredProcedure,
        //                    commandTimeout: 1200
        //                );
        //                await conn.CloseAsync();
        //            }
        //        }
        //        finally
        //        {
        //            _mysqlSemaphore.Release();
        //        }
        //        if (IMP)
        //            Logger.Important($"Sincronización completada correctamente.");
        //    }
        //    catch (Exception ex)
        //    {
        //        try
        //        {
        //            Logger.Error($"PROCESO_ERROR_{data.Sucursal}: {nombreProceso} - ERROR EN EL SERVER: {conexionEmpresa.ConnectionString} , {ex.Message}");
        //            throw;
        //        }
        //        catch (Exception rollEx)
        //        {
        //            Logger.Error($"PROCESO_ERROR_{data.Sucursal}: {nombreProceso} - ERROR EN EL SERVER: {conexionEmpresa.ConnectionString}, {ex.Message}");
        //            throw;
        //        }
        //    }
        //}



        private async Task BulkMergeAsync<T>(
                                                SqlConnection connection,
                                                //SqlTransaction transaction,
                                                IEnumerable<T> data,
                                                string tempTableSql,
                                                string tempTableName,
                                                string mergeSql,
        int batchSizeOverride = -1)
        {
            if (data == null) return;
            var list = data as IList<T> ?? data.ToList();
            if (!list.Any()) return;

            int effectiveBatch = batchSizeOverride > 0 ? batchSizeOverride : batchSize;

            // Crear tabla temporal en la sesión (dentro de la transacción)
            using (var createCmd = new SqlCommand(tempTableSql, connection))
            {
                createCmd.CommandTimeout = 120;
                await createCmd.ExecuteNonQueryAsync();
            }

            // Convertir a DataTable (prop names => column names)
            var table = ToDataTable(list);

            // Cargar en bloques
            int totalRows = table.Rows.Count;
            int processed = 0;
            for (int i = 0; i < totalRows; i += effectiveBatch)
            {
                var rows = table.AsEnumerable().Skip(i).Take(effectiveBatch).CopyToDataTable();
                using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, null))
                {
                    bulk.DestinationTableName = tempTableName;
                    bulk.BulkCopyTimeout = 0; // sin timeout (ajusta si lo deseas)
                    bulk.EnableStreaming = true;
                    await bulk.WriteToServerAsync(rows);
                }
                processed += rows.Rows.Count;
                //Logger.Important($"    Cargadas {processed}/{totalRows} filas en {tempTableName}");
            }

            // Ejecutar MERGE
            using (var mergeCmd = new SqlCommand(mergeSql, connection))
            {
                mergeCmd.CommandTimeout = 600;
                int affected = await mergeCmd.ExecuteNonQueryAsync();
                //Logger.Important($"    MERGE completado en {tempTableName} - filas afectadas (ExecuteNonQuery): {affected}");
            }

            // Borrar temp table (opcional, pero limpio)
            using (var dropCmd = new SqlCommand($"DROP TABLE IF EXISTS {tempTableName};", connection))
            {
                await dropCmd.ExecuteNonQueryAsync();
            }
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
                    //$"TrustServerCertificate=True;" +
                    //$"Connection Timeout=60;";

        //public string ConnectionString =>
        //            $"Server={HostName},{Puerto};" +
        //            $"Database={DatabaseName};" +
        //            $"User Id={UserName};" +
        //            $"Password={Password};" +
        //            $"TrustServerCertificate=True;" +
        //            $"Connect Timeout=60;" +
        //            $"Max Pool Size=1000;" +
        //            $"Min Pool Size=10;";
    }

}














