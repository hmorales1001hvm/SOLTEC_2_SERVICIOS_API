using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soltec.DB;
using Soltec.Entities.Ventas;

namespace Soltec.Business
{
    public class VentasBusiness
    {
        private readonly IConfiguration Configuration;
        private readonly ILogger<VentasDB> Logger;
        private readonly ILogger<SetDeTransmisionesDB> Logger2;
        private readonly ILogger<ConexionCacheRepository> Logger3;


        public VentasDB VentasDB;
        public SetDeTransmisionesDB SetDeTransmisionesDal;
        

        public VentasBusiness(IConfiguration configuration, ILogger<VentasDB> logger, ILogger<SetDeTransmisionesDB> logger2, ILogger<ConexionCacheRepository> logger3)
        {
            Configuration = configuration;
            Logger = logger;
            Logger2 = logger2;
            Logger3 = logger3;
            VentasDB = new VentasDB(Configuration, Logger);
            SetDeTransmisionesDal = new SetDeTransmisionesDB(Configuration, Logger2, Logger3);
        }


        //public async Task<List<SPOS_SQLScripts>> ObtieneScripts(string numeroSucursal)
        //{
        //    var result = new List<SPOS_SQLScripts>();
        //    try
        //    {
        //        result = await VentasDB.ObtieneScripts(numeroSucursal);

        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogError(ex, $"Oucrrio un error al obtener los scripts.");
        //        throw;
        //    }
        //}

        public async Task<List<SPOS_SQLScripts>> ObtieneScriptsConCargaInicial(string numeroSucursal)
        {
            var result = new List<SPOS_SQLScripts>();
            try
            {
                result = await VentasDB.ObtieneScriptsConCargaInicial(numeroSucursal);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Oucrrio un error al obtener los scripts.");
                throw;
            }
        }

        

        public async Task<List<SPOS_SQLScripts>> ObtieneScripts_SIMIPET(string numeroSucursal)
        {
            var result = new List<SPOS_SQLScripts>();
            try
            {
                result = await VentasDB.ObtieneScripts_SIMIPET(numeroSucursal);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Oucrrio un error al obtener los scripts.");
                throw;
            }
        }

   //     public async Task<List<SPOS_SQLScripts>> GetSPOS_SQLScripts(string numeroSucursal, bool isOnLine)
   //     {
   //         var result = new List<SPOS_SQLScripts>();
   //         try
   //         {
			//	result = await VentasDB.GetSPOS_SQLScripts(numeroSucursal, isOnLine);

   //             return result;
   //         }
   //         catch (Exception ex)
   //         {
   //             Logger.LogError(ex, $"Oucrrio un error al obtener los scripts.");
			//	throw;
			//}
   //     }

        //public async Task<List<SPOS_SQLScripts>> GetSQLScripts( bool isOnLine, string numeroSucursal)
        //{
        //    var result = new List<SPOS_SQLScripts>();
        //    try
        //    {
        //        result = await VentasDB.GetSQLScripts(isOnLine, numeroSucursal);

        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.LogError(ex, $"Oucrrio un error al obtener los scripts.");
        //        throw;
        //    }
        //}
        
        public async Task SincronizaScriptUltimo(ProcesosOnLine data)
        {
            try
            {
                await SetDeTransmisionesDal.SincronizaScriptUltimo(data);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error al guardar el proceso en linea {data.NombreProceso}.");
                throw;

            }
        }

        public async Task ActualizaSucursalTransmision(ProcesosOnLine data)
        {
            try
            {
                Logger.LogInformation($"Actualizando sucursal: {data.Sucursal}");
                await SetDeTransmisionesDal.ActualizaSucursalTransmision(data);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Sucursal en línea.");
                throw;
            }
        }

        

        public async Task ActualizarEstatusHistorico(string sucursal)
        {
            try
            {
                Logger.LogInformation($"Actualizando la sucursal con el estatus RECIBIDO");
                await SetDeTransmisionesDal.ActualizarEstatusHistorico(sucursal);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error al actualizar la sucursal.");
                throw;

            }
        }

        


        public async Task SincronizaScript_SimiPET(ProcesosOnLine data)
        {
            try
            {
                Logger.LogInformation($"Procesando Script - SincronizaScript_SimiPET - {data.DatabaseName} : {data.NombreProceso}");
                await SetDeTransmisionesDal.SincronizaScript_SimiPET(data);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error al guardar el proceso en linea {data.NombreProceso}.");
                throw;

            }
        }

        
        public async Task<byte[]> GetReporteExcel(string rfcEmpresa)
        {
            try
            {
                Logger.LogWarning($"Generando reporte excel para rfc empresa: {rfcEmpresa}");
                var apiDirectory = Configuration.GetSection("AppConfig").GetSection("ApiDirectory").Value;

                byte[] excelPackage = null;

                var template = new FileInfo(Path.Combine($@"{apiDirectory}\Templates\ReporteVentasEnLinea.xlsx"));

                if (!template.Exists)
                    throw new Exception("La plantilla no existe");

                Logger.LogInformation($"Obteniendo datos para la generacion de reporte rfc empresa: {rfcEmpresa}");
                var getReporteExcelData = await VentasDB.GetReporteExcelDataDAL(rfcEmpresa);


                //The next block of code it will comment in a future to test
                //using (var package = new ExcelPackage(template))
                //{
                //    var dataSheet = package.Workbook.Worksheets["datos"];
                //    dataSheet.Cells["A2:D2"].LoadFromCollection(getReporteExcelData);
                //    excelPackage = package.GetAsByteArray();
                //}

                return excelPackage;
            }
            catch (Exception ex)
            {
                Logger.LogError($"GetReporteExcel - {ex.Message}");
                throw;
            }
        }

        public async Task<ParametrosGenerales> GetParametros(string claveSimi)
        {
            var result = new ParametrosGenerales();
            try
            {
                Logger.LogInformation($"Obteniendo parametros para la clave simi: {claveSimi}");
				result = await VentasDB.GetParametrosDAL(claveSimi);

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Ocurrio un error al obtener los parametros para la sucursal {claveSimi}");
                throw;
			}
        }


    }
}
