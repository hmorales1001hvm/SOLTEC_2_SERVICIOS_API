using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Soltec.ApiCommon.Entities.Ventas;
using Soltec.Common.Logger;
using System.Data;
using System.Text.Json;

namespace ApiDAL
{

    public class SetDeTransmisionesDAL
    {
        private readonly IConfiguration Configuration;
        private readonly ILogger<SetDeTransmisionesDAL> _Logger;
        private int batchSize = 1500;

        public SetDeTransmisionesDAL(IConfiguration configuration, ILogger<SetDeTransmisionesDAL> logger)
        {
            Configuration = configuration;
            _Logger = logger;
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

            await _sqlSemaphore.WaitAsync(); 
            try
            {
                Logger.Info($"Nombre del proceso a ejecutar: {data.NombreProceso}");

                if (data.NombreProceso.Trim() == "Ventas_VentasProductos")
                {
                    Logger.Important($"Carga de la sucursal: {data.Sucursal} - SincronizaSetDeTransmisionesSQLServer - {data.NombreProceso}");

                    await SincronizaSetDeTransmisionesSQLServerUltimo(data);
                }
                else
                {
                    await SincronizaScriptOrquestador(data);
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
                Logger.Info($"Actualizando sucursal {sucursal} a ESTATUS='RECIBIDO' en soltec2_Historicos");

                var connectionString = Configuration.GetConnectionString("DbFacturaRealOrquestador");

                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                string sql = "UPDATE soltec2_Historicos SET Estatus = @Estatus, FechaRecibido=SYSDATE() WHERE ClaveSimi = @Sucursal AND Estatus='PENDIENTE'";

                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Estatus", "RECIBIDO");
                cmd.Parameters.AddWithValue("@Sucursal", sucursal);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();
                Logger.Info($"Filas actualizadas: {rowsAffected}");

                await connection.CloseAsync();
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
            string connString = $"Server={data.HostName};Database={data.DatabaseName};User Id={data.UserName};Password={data.Password};TrustServerCertificate=True;Connect Timeout=60;";
            using var connection = new SqlConnection(connString);
            await connection.OpenAsync();

            try
            {
                Logger.Important($"Iniciando SincronizaSetDeTransmisionesSQLServer_SimiPET - Sucursal: {data.Sucursal}");

                Logger.Info(data.Json);

                // Deserializar DTO raíz
                var dto = JsonSerializer.Deserialize<SalesDataDto>(data.Json) ?? new SalesDataDto();

                // 1) Ventas
                var ventasValidas = dto.Ventas?.Where(v => v.Id_Venta != null).ToList();
                Logger.Important($"Procesando Ventas ({ventasValidas?.Count ?? 0})...");
                await BulkMergeAsync(connection, ventasValidas, @"
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
                    Procesado SMALLINT NOT NULL,
                    FechaHoraVenta DATETIME NOT NULL,
                    TipoVenta INT NOT NULL
                );",
                    "#TempVentas",
                    @"
                MERGE INTO Ventas AS target
                USING #TempVentas AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                WHEN NOT MATCHED THEN
                    INSERT (FechaOperacion, ClaveSimi, Id_Venta, id_usuario_venta, Empleado,
                            idRegistradora, idRegistradoraVenta, idRegistradoraCobro, TipoOperacion,
                            Procesado, FechaHoraVenta, TipoVenta)
                    VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.id_usuario_venta, source.Empleado,
                            source.idRegistradora, source.idRegistradoraVenta, source.idRegistradoraCobro, source.TipoOperacion,
                            source.Procesado, source.FechaHoraVenta, source.TipoVenta);");

                Logger.Important($"Ventas procesadas.");
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // 2) VentasProductos
                var ventasProductos = dto.VentasProductos?.Where(v => v.Id_Venta != null).ToList();
                Logger.Important($"Procesando VentasProductos ({ventasProductos?.Count ?? 0})...");

                await BulkMergeAsync(connection, ventasProductos, @"
                CREATE TABLE #TempVentasProductos (
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
                    Nivel3 VARCHAR(50) NOT NULL
                );",
                    "#TempVentasProductos",
                    @"
                        MERGE INTO VentasProductos AS target
                        USING #TempVentasProductos AS source
                        ON target.FechaOperacion = source.FechaOperacion
                           AND target.ClaveSimi = source.ClaveSimi
                           AND target.Id_Venta = source.Id_Venta
                           AND target.Codigo = source.Codigo

                        WHEN MATCHED THEN
                            UPDATE SET
                                target.Id_ProductoSAT     = source.Id_ProductoSAT,
                                target.TipoOperacion      = source.TipoOperacion,
                                target.Producto           = source.Producto,
                                target.NoPonderado        = source.NoPonderado,
                                target.Premio             = source.Premio,
                                target.Combo              = source.Combo,
                                target.Inventario         = source.Inventario,
                                target.Cantidad           = source.Cantidad,
                                target.Precio             = source.Precio,
                                target.IVA                = source.IVA,
                                target.Descuento          = source.Descuento,
                                target.DescuentoPorciento = source.DescuentoPorciento,
                                target.IVA_Porciento      = source.IVA_Porciento,
                                target.IVA_Importe        = source.IVA_Importe,
                                target.Presentacion       = source.Presentacion,
                                target.Nivel1             = source.Nivel1,
                                target.Nivel2             = source.Nivel2,
                                target.Nivel3             = source.Nivel3

                        WHEN NOT MATCHED THEN
                            INSERT (
                                FechaOperacion, ClaveSimi, Id_Venta, Codigo,
                                Id_ProductoSAT, TipoOperacion, Producto,
                                NoPonderado, Premio, Combo, Inventario,
                                Cantidad, Precio, IVA, Descuento,
                                DescuentoPorciento, IVA_Porciento, IVA_Importe,
                                Presentacion, Nivel1, Nivel2, Nivel3
                            )
                            VALUES (
                                source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.Codigo,
                                source.Id_ProductoSAT, source.TipoOperacion, source.Producto,
                                source.NoPonderado, source.Premio, source.Combo, source.Inventario,
                                source.Cantidad, source.Precio, source.IVA, source.Descuento,
                                source.DescuentoPorciento, source.IVA_Porciento, source.IVA_Importe,
                                source.Presentacion, source.Nivel1, source.Nivel2, source.Nivel3
                            );
                ");

                Logger.Important($"VentasProductos procesadas.");
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // 3) VentasImpuestos
                var ventasImpuestos = dto.VentasImpuestos?.Where(v => v.Id_Venta != null).ToList();

                Logger.Important($"Procesando VentasImpuestos ({ventasImpuestos?.Count ?? 0})...");
                await BulkMergeAsync(connection, ventasImpuestos, @"
                CREATE TABLE #TempVentasImpuestos (
                    FechaOperacion DATETIME NOT NULL,
                    ClaveSimi CHAR(10) NOT NULL,
                    Id_Venta INT NOT NULL,
                    Impuesto VARCHAR(10) NOT NULL,
                    TipoFactor VARCHAR(10) NOT NULL,
                    TasaImpuesto NUMERIC(12,2) NOT NULL,
                    ClaveSATImpuesto VARCHAR(10) NOT NULL,
                    BaseImpuesto NUMERIC(12,2) NOT NULL,
                    ImporteImpuesto NUMERIC(12,2) NOT NULL,
                    TipoOperacion INT NOT NULL
                );",
                    "#TempVentasImpuestos",
                    @"
                MERGE INTO VentasImpuestos AS target
                USING #TempVentasImpuestos AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                   AND target.Impuesto = source.Impuesto
                   AND target.TipoFactor = source.TipoFactor
                   AND target.TasaImpuesto = source.TasaImpuesto
                WHEN NOT MATCHED THEN
                    INSERT (FechaOperacion, ClaveSimi, Id_Venta, Impuesto, TipoFactor, TasaImpuesto, ClaveSATImpuesto, BaseImpuesto, ImporteImpuesto, TipoOperacion)
                    VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.Impuesto, source.TipoFactor, source.TasaImpuesto, source.ClaveSATImpuesto, source.BaseImpuesto, source.ImporteImpuesto, source.TipoOperacion);");

                Logger.Important($"VentasImpuestos procesadas.");
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // 4) VentasImpuestosDetalle
                var ventasImpuestosDetalle = dto.VentasImpuestosDetalle?.Where(v => v.Id_Venta != null).ToList();
                Logger.Important($"Procesando VentasImpuestosDetalle ({ventasImpuestosDetalle?.Count ?? 0})...");
                await BulkMergeAsync(connection, ventasImpuestosDetalle, @"
                CREATE TABLE #TempVentasImpuestosDetalle (
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
                    TipoOperacion INT NOT NULL
                );",
                    "#TempVentasImpuestosDetalle",
                    @"
                MERGE INTO VentasImpuestosDetalle AS target
                USING #TempVentasImpuestosDetalle AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                   AND target.Id_Producto = source.Id_Producto
                   AND target.Impuesto = source.Impuesto
                WHEN NOT MATCHED THEN
                    INSERT (ClaveSimi, FechaOperacion, Id_Venta, Id_Producto, Impuesto, ClaveImpuesto, TasaImpuesto, TipoFactor, Base, ImporteIVA, ImporteVenta, TipoOperacion)
                    VALUES (source.ClaveSimi, source.FechaOperacion, source.Id_Venta, source.Id_Producto, source.Impuesto, source.ClaveImpuesto, source.TasaImpuesto, source.TipoFactor, source.Base, source.ImporteIVA, source.ImporteVenta, source.TipoOperacion);");

                Logger.Important($"VentasImpuestosDetalle procesadas.");
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // 5) VentasDesgloceTotales
                var ventasDesgloceTotales = dto.VentasDesgloceTotales?.Where(v => v.Id_Venta!=null).ToList();
                Logger.Important($"Procesando VentasDesgloseTotales ({ventasDesgloceTotales?.Count ?? 0})...");
                await BulkMergeAsync(connection, ventasDesgloceTotales, @"
                CREATE TABLE #VentasDesgloseTotales (
                    ClaveSimi CHAR(10) NOT NULL,
                    FechaOperacion DATETIME NOT NULL,
                    Id_Venta INT NOT NULL,
                    PrecioSinIVA NUMERIC(12,2) NOT NULL,
                    Importe NUMERIC(12,2) NOT NULL,
                    Descuento NUMERIC(12,2) NOT NULL,
                    Impuestos NUMERIC(12,2) NOT NULL,
                    Total NUMERIC(12,2) NOT NULL,
                    TipoOperacion INT NOT NULL
                );",
                    "#VentasDesgloseTotales",
                    @"
                MERGE INTO VentasDesgloseTotales AS target
                USING #VentasDesgloseTotales AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                WHEN NOT MATCHED THEN
                    INSERT (ClaveSimi, FechaOperacion, Id_Venta, PrecioSinIVA, Importe, Descuento, Impuestos, Total, TipoOperacion)
                    VALUES (source.ClaveSimi, source.FechaOperacion, source.Id_Venta, source.PrecioSinIVA, source.Importe, source.Descuento, source.Impuestos, source.Total, source.TipoOperacion);");

                Logger.Important($"VentasDesgloseTotales procesadas.");
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // 6) VentasImportesProductos
                var ventasImportesProductos = dto.VentasImportesProductos?.Where(v => v.Id_Venta != null).ToList();
                Logger.Important($"Procesando VentasImportesProductos ({ventasImportesProductos?.Count ?? 0})...");
                await BulkMergeAsync(connection, ventasImportesProductos, @"
                CREATE TABLE #TempVentasImportesProductos (
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
                    TipoOperacion INT NOT NULL
                );",
                    "#TempVentasImportesProductos",
                    @"
                MERGE INTO VentasImportesProductos AS target
                USING #TempVentasImportesProductos AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                   AND target.Id_Producto = source.Id_Producto
                WHEN NOT MATCHED THEN
                    INSERT (FechaOperacion, ClaveSimi, Id_Venta, Id_Producto, Precio, PrecioUnitarioNeto, Cantidad,
                            SubtotalNeto, SubtotalConImpuestos, DescuentoNeto, DescuentoConImpuestos, ImporteNeto, ImporteConImpuestos,
                            ImpuestoCalculado, Total, TipoOperacion)
                    VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.Id_Producto, source.Precio, source.PrecioUnitarioNeto, source.Cantidad,
                            source.SubtotalNeto, source.SubtotalConImpuestos, source.DescuentoNeto, source.DescuentoConImpuestos, source.ImporteNeto, source.ImporteConImpuestos,
                            source.ImpuestoCalculado, source.Total, source.TipoOperacion);");

                Logger.Important($"VentasImportesProductos procesadas.");
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // 7) VentasVendedorCuotas
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
                    MontoIva = v.MontoIva
                }).ToList();


                await BulkMergeAsync(connection, ventasVendedorCuotasConSucursal, @"
                CREATE TABLE #TempVentasVendedorCuotas (
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
                    MontoIva DECIMAL(12,2) NOT NULL
                );",
                    "#TempVentasVendedorCuotas",
                    @"
                MERGE INTO VentasVendedorCuotas AS target
                USING #TempVentasVendedorCuotas AS source
                ON target.ClaveSimi = source.ClaveSimi AND target.Fecha = source.Fecha AND target.IdVendedor = source.IdVendedor
                WHEN NOT MATCHED THEN
                    INSERT (ClaveSimi, Fecha, IdVendedor, Nombre, ImporteVenta, Transaccionesventa, PorcVenta, ImporteNaturistas, PorcNaturistas, ImporteNocturno, MontoDescuento, Menudeos, MontoIva)
                    VALUES (source.ClaveSimi, source.Fecha, source.IdVendedor, source.Nombre, source.ImporteVenta, source.Transaccionesventa, source.PorcVenta, source.ImporteNaturistas, source.PorcNaturistas, source.ImporteNocturno, source.MontoDescuento, source.Menudeos, source.MontoIva);");

                Logger.Important($"VentasVendedorCuotas procesadas.");
                GC.Collect();
                GC.WaitForPendingFinalizers();


                var registrosValidos = dto.InventarioCosto?
                .Where(i => !string.IsNullOrWhiteSpace(i.ClaveSimi)
                         && !string.IsNullOrWhiteSpace(i.Codigo)
                         && i.FechaFactura != default)
                .ToList();

                Logger.Important($"Procesando Inventario Costo ({registrosValidos?.Count ?? 0})...");

                await BulkMergeAsync(connection, registrosValidos, @"
                                                                    CREATE TABLE #TempInventarioCosto (
                                                                        ClaveSimi CHAR(10) NOT NULL,
                                                                        Codigo CHAR(10) NOT NULL,
                                                                        CostoUnitario DECIMAL(12, 2) NULL,
                                                                        FechaFactura DATE NOT NULL,
                                                                        FechaSurtido DATE NULL
                                                                    );",
                                                                                    "#TempInventarioCosto",
                                                                                    @"
                                                                    MERGE INTO Inventario_Costo AS target
                                                                    USING #TempInventarioCosto AS source
                                                                    ON target.ClaveSimi = source.ClaveSimi
                                                                       AND target.Codigo = source.Codigo
                                                                       AND target.FechaFactura = source.FechaFactura
                                                                    WHEN MATCHED THEN
                                                                        UPDATE SET 
                                                                            target.CostoUnitario = source.CostoUnitario,
                                                                            target.FechaSurtido = source.FechaSurtido
                                                                    WHEN NOT MATCHED THEN
                                                                        INSERT (ClaveSimi, Codigo, CostoUnitario, FechaFactura, FechaSurtido)
                                                                        VALUES (source.ClaveSimi, source.Codigo, source.CostoUnitario, source.FechaFactura, source.FechaSurtido);
                                                                    ");

                Logger.Important("Inventario Costo procesado.");
                GC.Collect();
                GC.WaitForPendingFinalizers();

                var registrosValidos2 = dto.SPOSInventario?
                    .Where(i => !string.IsNullOrWhiteSpace(i.ClaveSimi) && !string.IsNullOrWhiteSpace(i.Codigo))
                    .ToList();

                if (registrosValidos2 != null || registrosValidos2.Count > 0)
                {
                    Logger.Warning("No hay registros válidos para SPOS Inventario SIMIPET.");



                    Logger.Important($"Procesando SPOS Inventario SIMIPET ({registrosValidos2.Count})...");

                    await BulkMergeAsync(connection, registrosValidos2, @"
                        CREATE TABLE #TempSPOSInventario (
                            FechaOperacion DATETIME NOT NULL,
                            ClaveSimi VARCHAR(10) NOT NULL,
                            Codigo VARCHAR(20) NOT NULL,
                            Producto VARCHAR(100) NULL,
                            PrecioVenta DECIMAL(18,2),
                            ExistenciaInicial INT NULL,
                            ExistenciaFinal INT NULL,
                            Entradas INT NULL,
                            Salidas INT NULL);",
                                            "#TempSPOSInventario",
                                            @"
                        MERGE INTO SPOSInventario AS target
                        USING (SELECT DISTINCT * FROM #TempSPOSInventario) AS source
                        ON target.FechaOperacion = source.FechaOperacion
                           AND target.ClaveSimi = source.ClaveSimi
                           AND target.Codigo = source.Codigo
                        WHEN MATCHED THEN
                            UPDATE SET
                                target.ExistenciaInicial = source.ExistenciaInicial,
                                target.ExistenciaFinal = source.ExistenciaFinal,
                                target.Entradas = source.Entradas,
                                target.Salidas = source.Salidas,
                                target.Producto = source.Producto,
                                target.PrecioVenta = source.PrecioVenta
                        WHEN NOT MATCHED THEN
                            INSERT (FechaOperacion, ClaveSimi, Codigo, ExistenciaInicial, ExistenciaFinal, Entradas, Salidas, Producto, PrecioVenta)
                            VALUES (source.FechaOperacion, source.ClaveSimi, source.Codigo, source.ExistenciaInicial, 
                                    source.ExistenciaFinal, source.Entradas, source.Salidas, source.Producto, source.PrecioVenta);
                    ");

                    Logger.Important("SPOS Inventario procesado.");
                }


                var registrosValidosFacturas = dto.SPOSFacturas?
                    .Where(i => !string.IsNullOrWhiteSpace(i.ClaveSimi) && !string.IsNullOrWhiteSpace(i.Serie))
                    .ToList();

                var jsonFacturas = JsonSerializer.Serialize(registrosValidosFacturas, new JsonSerializerOptions
                {
                    WriteIndented = true 
                });
                Logger.Important($"Registros Facturas:\n{jsonFacturas}");

                if (registrosValidosFacturas != null || registrosValidosFacturas.Count > 0)
                {
                    Logger.Warning("No hay registros válidos para SPOS Facturas SIMIPET.");

                    Logger.Important($"Procesando SPOS Facturas ({registrosValidosFacturas.Count})...");

                    await BulkMergeAsync(connection, registrosValidosFacturas, @"
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
                    MERGE INTO SPOSFacturas AS target
                    USING (SELECT DISTINCT * FROM #TempSPOSFacturas) AS source
                    ON target.FechaOperacion = source.FechaOperacion
                       AND target.ClaveSimi = source.ClaveSimi
                       AND target.Serie = source.Serie
                       AND target.Folio = source.Folio
                    WHEN MATCHED THEN
                        UPDATE SET
                            target.Estatus = source.Estatus,
                            target.Electronica = source.Electronica,
                            target.NotaCredito = source.NotaCredito,
                            target.GranTotal = source.GranTotal
                    WHEN NOT MATCHED THEN
                        INSERT (FechaOperacion, ClaveSimi, Serie, Folio, Estatus, Electronica, NotaCredito, GranTotal)
                        VALUES (source.FechaOperacion, source.ClaveSimi, source.Serie, source.Folio,
                                source.Estatus, source.Electronica, source.NotaCredito, source.GranTotal);
                    ");

                    Logger.Important("SPOS Facturas procesadas.");
                }



                // Commit transaction al terminar todo
                //transaction.Commit();
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

                Logger.Important($"Se actualizan versiones SIMI PET.");

            }
            catch (Exception ex)
            {
                try
                {
                    //transaction.Rollback();
                    Logger.Important($"Transacción revertida SIMIPET. {data.NombreProceso} - {data.Sucursal} - JSON - {data.Json}");
                    throw;
                }
                catch (Exception rollEx)
                {
                    Logger.Important($"Error al hacer rollback SIMIPET: {rollEx.Message} - JSON - {data.Json}");
                    throw;
                }

                Logger.Important($"Error en SincronizaScript_SimiPET: {ex.Message}\n{ex.StackTrace} - JSON - {data.Json}");
                throw;
            }
            finally
            {
                connection.CloseAsync();
            }
        }


        public async Task ActualizaSucursalTransmision(ProcesosOnLine data)
        {
            string connString = $"Server={data.HostName};Database={data.DatabaseName};User Id={data.UserName};Password={data.Password};TrustServerCertificate=True;Connect Timeout=60;";

            using var connection = new SqlConnection(connString);

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
                Logger.Important($"{ex.Message}");
                throw;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }


        public async Task SincronizaSetDeTransmisionesSQLServerUltimo(ProcesosOnLine data)
        {


            string connString = $"Server={data.HostName};Database={data.DatabaseName};User Id={data.UserName};Password={data.Password};TrustServerCertificate=True;Connect Timeout=60;Max Pool Size=300;Min Pool Size=10;";
            using var connection = new SqlConnection(connString);

            var nombreProceso = string.Empty;
            
            // Inicia transacción
            //using var transaction = connection.BeginTransaction();
            try
            {
                await connection.OpenAsync();

                Logger.Important($"Iniciando SincronizaSetDeTransmisionesSQLServer - Sucursal: {data.Sucursal}");

                // Deserializar DTO raíz
                var dto = JsonSerializer.Deserialize<SalesDataDto>(data.Json) ?? new SalesDataDto();

                // 1) Ventas
                var ventasValidas = dto.Ventas?.Where(v => v.Id_Venta != null).ToList();
                nombreProceso = "Ventas";
                Logger.Important($"Procesando Ventas ({ventasValidas?.Count ?? 0})...");
                await BulkMergeAsync(connection, ventasValidas, @"
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
                    Procesado SMALLINT NOT NULL,
                    FechaHoraVenta DATETIME NOT NULL,
                    TipoVenta INT NOT NULL
                );",
                    "#TempVentas",
                    @"
                MERGE INTO Ventas AS target
                USING #TempVentas AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                WHEN NOT MATCHED THEN
                    INSERT (FechaOperacion, ClaveSimi, Id_Venta, id_usuario_venta, Empleado,
                            idRegistradora, idRegistradoraVenta, idRegistradoraCobro, TipoOperacion,
                            Procesado, FechaHoraVenta, TipoVenta)
                    VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.id_usuario_venta, source.Empleado,
                            source.idRegistradora, source.idRegistradoraVenta, source.idRegistradoraCobro, source.TipoOperacion,
                            source.Procesado, source.FechaHoraVenta, source.TipoVenta);");

                Logger.Important($"Ventas procesadas.");
                //GC.Collect();
                //GC.WaitForPendingFinalizers();

                // 2) VentasProductos
                var ventasProductos = dto.VentasProductos?.Where(v => v.Id_Venta != null).ToList();
                Logger.Important($"Procesando VentasProductos ({ventasProductos?.Count ?? 0})...");
                nombreProceso = "VentasProductos";
                await BulkMergeAsync(connection, ventasProductos, @"
                CREATE TABLE #TempVentasProductos (
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
                    Nivel3 VARCHAR(50) NOT NULL
                );",
                    "#TempVentasProductos",
                    @"
                        MERGE INTO VentasProductos AS target
                        USING #TempVentasProductos AS source
                        ON target.FechaOperacion = source.FechaOperacion
                           AND target.ClaveSimi = source.ClaveSimi
                           AND target.Id_Venta = source.Id_Venta
                           AND target.Codigo = source.Codigo

                        WHEN MATCHED THEN
                            UPDATE SET
                                target.Id_ProductoSAT     = source.Id_ProductoSAT,
                                target.TipoOperacion      = source.TipoOperacion,
                                target.Producto           = source.Producto,
                                target.NoPonderado        = source.NoPonderado,
                                target.Premio             = source.Premio,
                                target.Combo              = source.Combo,
                                target.Inventario         = source.Inventario,
                                target.Cantidad           = source.Cantidad,
                                target.Precio             = source.Precio,
                                target.IVA                = source.IVA,
                                target.Descuento          = source.Descuento,
                                target.DescuentoPorciento = source.DescuentoPorciento,
                                target.IVA_Porciento      = source.IVA_Porciento,
                                target.IVA_Importe        = source.IVA_Importe,
                                target.Presentacion       = source.Presentacion,
                                target.Nivel1             = source.Nivel1,
                                target.Nivel2             = source.Nivel2,
                                target.Nivel3             = source.Nivel3

                        WHEN NOT MATCHED THEN
                            INSERT (
                                FechaOperacion, ClaveSimi, Id_Venta, Codigo,
                                Id_ProductoSAT, TipoOperacion, Producto,
                                NoPonderado, Premio, Combo, Inventario,
                                Cantidad, Precio, IVA, Descuento,
                                DescuentoPorciento, IVA_Porciento, IVA_Importe,
                                Presentacion, Nivel1, Nivel2, Nivel3
                            )
                            VALUES (
                                source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.Codigo,
                                source.Id_ProductoSAT, source.TipoOperacion, source.Producto,
                                source.NoPonderado, source.Premio, source.Combo, source.Inventario,
                                source.Cantidad, source.Precio, source.IVA, source.Descuento,
                                source.DescuentoPorciento, source.IVA_Porciento, source.IVA_Importe,
                                source.Presentacion, source.Nivel1, source.Nivel2, source.Nivel3
                            );
                ");

                Logger.Important($"VentasProductos procesadas.");
                //GC.Collect();
                //GC.WaitForPendingFinalizers();

                // 3) VentasImpuestos
                var ventasImpuestos = dto.VentasImpuestos?.Where(v => v.Id_Venta != null).ToList();
                nombreProceso = "VentasImpuestos";

                Logger.Important($"Procesando VentasImpuestos ({ventasImpuestos?.Count ?? 0})...");
                await BulkMergeAsync(connection, ventasImpuestos, @"
                CREATE TABLE #TempVentasImpuestos (
                    FechaOperacion DATETIME NOT NULL,
                    ClaveSimi CHAR(10) NOT NULL,
                    Id_Venta INT NOT NULL,
                    Impuesto VARCHAR(10) NOT NULL,
                    TipoFactor VARCHAR(10) NOT NULL,
                    TasaImpuesto NUMERIC(12,2) NOT NULL,
                    ClaveSATImpuesto VARCHAR(10) NOT NULL,
                    BaseImpuesto NUMERIC(12,2) NOT NULL,
                    ImporteImpuesto NUMERIC(12,2) NOT NULL,
                    TipoOperacion INT NOT NULL
                );",
                    "#TempVentasImpuestos",
                    @"
                MERGE INTO VentasImpuestos AS target
                USING #TempVentasImpuestos AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                   AND target.Impuesto = source.Impuesto
                   AND target.TipoFactor = source.TipoFactor
                   AND target.TasaImpuesto = source.TasaImpuesto
                WHEN NOT MATCHED THEN
                    INSERT (FechaOperacion, ClaveSimi, Id_Venta, Impuesto, TipoFactor, TasaImpuesto, ClaveSATImpuesto, BaseImpuesto, ImporteImpuesto, TipoOperacion)
                    VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.Impuesto, source.TipoFactor, source.TasaImpuesto, source.ClaveSATImpuesto, source.BaseImpuesto, source.ImporteImpuesto, source.TipoOperacion);");

                Logger.Important($"VentasImpuestos procesadas.");
                //GC.Collect();
                //GC.WaitForPendingFinalizers();

                // 4) VentasImpuestosDetalle
                var ventasImpuestosDetalle = dto.VentasImpuestosDetalle?.Where(v => v.Id_Venta != null).ToList();
                Logger.Important($"Procesando VentasImpuestosDetalle ({ventasImpuestosDetalle?.Count ?? 0})...");
                nombreProceso = "VentasImpuestosDetalle";

                await BulkMergeAsync(connection, ventasImpuestosDetalle, @"
                CREATE TABLE #TempVentasImpuestosDetalle (
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
                    TipoOperacion INT NOT NULL
                );",
                    "#TempVentasImpuestosDetalle",
                    @"
                MERGE INTO VentasImpuestosDetalle AS target
                USING #TempVentasImpuestosDetalle AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                   AND target.Id_Producto = source.Id_Producto
                   AND target.Impuesto = source.Impuesto
                WHEN NOT MATCHED THEN
                    INSERT (ClaveSimi, FechaOperacion, Id_Venta, Id_Producto, Impuesto, ClaveImpuesto, TasaImpuesto, TipoFactor, Base, ImporteIVA, ImporteVenta, TipoOperacion)
                    VALUES (source.ClaveSimi, source.FechaOperacion, source.Id_Venta, source.Id_Producto, source.Impuesto, source.ClaveImpuesto, source.TasaImpuesto, source.TipoFactor, source.Base, source.ImporteIVA, source.ImporteVenta, source.TipoOperacion);");

                Logger.Important($"VentasImpuestosDetalle procesadas.");
                //GC.Collect();
                //GC.WaitForPendingFinalizers();

                // 5) VentasDesgloceTotales
                var ventasDesgloceTotales = dto.VentasDesgloceTotales?.Where(v => v.Id_Venta != null).ToList();
                Logger.Important($"Procesando VentasDesgloseTotales ({ventasDesgloceTotales?.Count ?? 0})...");
                nombreProceso = "VentasDesgloseTotales";
                await BulkMergeAsync(connection, ventasDesgloceTotales, @"
                CREATE TABLE #VentasDesgloseTotales (
                    ClaveSimi CHAR(10) NOT NULL,
                    FechaOperacion DATETIME NOT NULL,
                    Id_Venta INT NOT NULL,
                    PrecioSinIVA NUMERIC(12,2) NOT NULL,
                    Importe NUMERIC(12,2) NOT NULL,
                    Descuento NUMERIC(12,2) NOT NULL,
                    Impuestos NUMERIC(12,2) NOT NULL,
                    Total NUMERIC(12,2) NOT NULL,
                    TipoOperacion INT NOT NULL
                );",
                    "#VentasDesgloseTotales",
                    @"
                MERGE INTO VentasDesgloseTotales AS target
                USING #VentasDesgloseTotales AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                WHEN NOT MATCHED THEN
                    INSERT (ClaveSimi, FechaOperacion, Id_Venta, PrecioSinIVA, Importe, Descuento, Impuestos, Total, TipoOperacion)
                    VALUES (source.ClaveSimi, source.FechaOperacion, source.Id_Venta, source.PrecioSinIVA, source.Importe, source.Descuento, source.Impuestos, source.Total, source.TipoOperacion);");

                Logger.Important($"VentasDesgloseTotales procesadas.");
                //GC.Collect();
                //GC.WaitForPendingFinalizers();

                // 6) VentasImportesProductos
                var ventasImportesProductos = dto.VentasImportesProductos?.Where(v => v.Id_Venta != null).ToList();
                Logger.Important($"Procesando VentasImportesProductos ({ventasImportesProductos?.Count ?? 0})...");
                nombreProceso = "VentasImportesProductos";

                await BulkMergeAsync(connection, ventasImportesProductos, @"
                CREATE TABLE #TempVentasImportesProductos (
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
                    TipoOperacion INT NOT NULL
                );",
                    "#TempVentasImportesProductos",
                    @"
                MERGE INTO VentasImportesProductos AS target
                USING #TempVentasImportesProductos AS source
                ON target.FechaOperacion = source.FechaOperacion
                   AND target.ClaveSimi = source.ClaveSimi
                   AND target.Id_Venta = source.Id_Venta
                   AND target.Id_Producto = source.Id_Producto
                WHEN NOT MATCHED THEN
                    INSERT (FechaOperacion, ClaveSimi, Id_Venta, Id_Producto, Precio, PrecioUnitarioNeto, Cantidad,
                            SubtotalNeto, SubtotalConImpuestos, DescuentoNeto, DescuentoConImpuestos, ImporteNeto, ImporteConImpuestos,
                            ImpuestoCalculado, Total, TipoOperacion)
                    VALUES (source.FechaOperacion, source.ClaveSimi, source.Id_Venta, source.Id_Producto, source.Precio, source.PrecioUnitarioNeto, source.Cantidad,
                            source.SubtotalNeto, source.SubtotalConImpuestos, source.DescuentoNeto, source.DescuentoConImpuestos, source.ImporteNeto, source.ImporteConImpuestos,
                            source.ImpuestoCalculado, source.Total, source.TipoOperacion);");

                Logger.Important($"VentasImportesProductos procesadas.");
                //GC.Collect();
                //GC.WaitForPendingFinalizers();

                // 7) VentasVendedorCuotas
                Console.WriteLine($"Procesando VentasVendedorCuotas ({dto.VentasVendedorCuotas?.Count ?? 0})...");
                nombreProceso = "VentasVendedorCuotas";

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
                        MontoIva = v.MontoIva
                    }).ToList();

                await BulkMergeAsync(connection, ventasVendedorCuotasConSucursal, @"
                                    CREATE TABLE #TempVentasVendedorCuotas (
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
                                        MontoIva DECIMAL(12,2) NOT NULL
                                    );",
                                                    "#TempVentasVendedorCuotas",
                                                    @"
                                    MERGE INTO VentasVendedorCuotas AS target
                                    USING #TempVentasVendedorCuotas AS source
                                    ON target.ClaveSimi = source.ClaveSimi
                                       AND CONVERT(VARCHAR,target.Fecha,112) = CONVERT(VARCHAR,source.Fecha,112)
                                       AND target.IdVendedor = source.IdVendedor

                                    WHEN MATCHED
                                    THEN UPDATE SET 
                                            target.Nombre = source.Nombre,
                                            target.ImporteVenta = source.ImporteVenta,
                                            target.Transaccionesventa = source.Transaccionesventa,
                                            target.PorcVenta = source.PorcVenta,
                                            target.ImporteNaturistas = source.ImporteNaturistas,
                                            target.PorcNaturistas = source.PorcNaturistas,
                                            target.ImporteNocturno = source.ImporteNocturno,
                                            target.MontoDescuento = source.MontoDescuento,
                                            target.Menudeos = source.Menudeos,
                                            target.MontoIva = source.MontoIva

                                    WHEN NOT MATCHED THEN
                                        INSERT (
                                            ClaveSimi, Fecha, IdVendedor, Nombre,
                                            ImporteVenta, Transaccionesventa, PorcVenta,
                                            ImporteNaturistas, PorcNaturistas, ImporteNocturno,
                                            MontoDescuento, Menudeos, MontoIva
                                        )
                                        VALUES (
                                            source.ClaveSimi, source.Fecha, source.IdVendedor, source.Nombre,
                                            source.ImporteVenta, source.Transaccionesventa, source.PorcVenta,
                                            source.ImporteNaturistas, source.PorcNaturistas, source.ImporteNocturno,
                                            source.MontoDescuento, source.Menudeos, source.MontoIva
                                        );
                                    ");

                Console.WriteLine($"VentasVendedorCuotas procesadas.");
                //GC.Collect();
                //GC.WaitForPendingFinalizers();

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
                                VALUES (@ClaveSimi, @FechaHoraTransmision);
        ";

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
                                VALUES (@ClaveSimi, GETDATE());
                        ";

                    parametros = new
                    {
                        ClaveSimi = data.Sucursal
                    };
                }
                await connection.ExecuteAsync(sql, parametros);
                Logger.Important("Finalizando SQL Server");
                connection.Dispose();

               
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



                Logger.Important($"Sincronización completada correctamente.");
            }
            catch (Exception ex)
            {
                try
                {
                    Logger.Important($"PROCESO_ERROR_{data.Sucursal}: {nombreProceso} - JSON {data.Json} - {data.Sucursal}, {ex.Message}");
                    throw;
                }
                catch (Exception rollEx)
                {
                    Logger.Important($"PROCESO_ERROR_{data.Sucursal}: {nombreProceso} - JSON {data.Json} - {data.Sucursal}, {ex.Message}");
                    throw;
                }
            }
        }



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

}
