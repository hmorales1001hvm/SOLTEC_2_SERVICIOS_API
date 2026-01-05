using ApiBLL;
using ApiCommon.Api;
using ApiCommon.Entities.Ventas;
using ApiDAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Soltec.ApiCommon.Entities.Ventas;

namespace Soltec.ServicioTransmisionAPI.Controllers
{
    [Route("api/venta")]
	[ApiController]
	public class VentaTransmisionController : BaseApiController
	{
		private readonly IConfiguration Configuration;

		private readonly ILogger<VentasDAL> Logger;
        private readonly ILogger<SetDeTransmisionesDAL> Logger2;
        public VentasBLL VentasBLL;
        private readonly IWebHostEnvironment _env;

        public VentaTransmisionController(IConfiguration configuration, ILogger<VentasDAL> logger, ILogger<SetDeTransmisionesDAL> logger2, IWebHostEnvironment env)
		{
			Configuration = configuration;
			Logger = logger;
			Logger2 = logger2;
            _env = env;
            VentasBLL = new VentasBLL(Configuration,logger, logger2);
		}

		#region Requieren Token
		[Authorize]
		[HttpGet("getSPOS_SQLScripts/{numeroSucursal}/{isOnLine}")]
		public async Task<IActionResult> GetSPOS_SQLScripts(string numeroSucursal, bool isOnLine)
		{
			try
			{
				var getSqlScripts = await VentasBLL.GetSPOS_SQLScripts(numeroSucursal, isOnLine);

				return Ok(new ApiResponse<SPOS_SQLScripts>(getSqlScripts));
			}
			catch (Exception ex)
			{
                return SoltecErrorMessage(ex);
            }
		}

        [HttpGet("healt")]
        public async Task<IActionResult> Healt()
        {
            try
            {
                return Ok(new { status = "ok", message = "App running", container = Environment.MachineName });

            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [HttpPost("Test")]
        public IActionResult PruebaSimple()
        {
            return Ok(new
            {
                success = true,
                mensaje = "El método POST funciona correctamente.",
                fecha = DateTime.Now
            });
        }

        [Authorize]
		[HttpPost("uploadFileZIP")]
		[DisableRequestSizeLimit]
		[RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue, ValueLengthLimit = int.MaxValue)]
		public async Task<IActionResult> UploadFileZIP()
		{
			try
			{
				if (!Request.Form.Files.Any())
					return Ok(new ApiResponse());

				string pathToSave = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
				if (!Directory.Exists(pathToSave))
					Directory.CreateDirectory(pathToSave);
				if (!Directory.Exists(pathToSave + "\\Operativas"))
					Directory.CreateDirectory(pathToSave + "\\Operativas");
				if (!Directory.Exists(pathToSave + "\\Catalogos"))
					Directory.CreateDirectory(pathToSave + "\\Catalogos");

				foreach (IFormFile file in Request.Form.Files)
				{
					string fullPath = string.Empty;
					if (file.FileName.Contains("Operativas"))
						fullPath = Path.Combine(pathToSave + "\\Operativas", file.FileName);
					else
						fullPath = Path.Combine(pathToSave + "\\Catalogos", file.FileName);

					using FileStream stream = new(fullPath, FileMode.Create);
					file.CopyTo(stream);
				}
				return Ok(new ApiResponse());
			}

			catch (Exception ex)
			{
				return SoltecErrorMessage(ex);
			}
		}


        //[Authorize]
        [HttpPost("SincronizaScriptZipAsync")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue, ValueLengthLimit = int.MaxValue)]
        public async Task<IActionResult> SincronizaScriptZipAsync([FromForm] string sucursal)
        {
            try
            {
                if (string.IsNullOrEmpty(sucursal))
                    return BadRequest(new ApiResponse { Success = false, Message = "Parámetro 'sucursal' requerido." });

                if (!Request.Form.Files.Any())
                    return BadRequest(new ApiResponse { Success = false, Message = "No se recibieron archivos." });

                // Carpeta Historicos en el directorio actual de la app
                string pathToSave = Path.Combine(_env.ContentRootPath, "Historicos");
                Logger.LogInformation($"RUTA DEL HISTORICO: {pathToSave}");

                if (!Directory.Exists(pathToSave))
                    Directory.CreateDirectory(pathToSave);

                foreach (IFormFile file in Request.Form.Files)
                {
                    string fullPath = Path.Combine(pathToSave, file.FileName);

                    using (FileStream stream = new(fullPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                }

                await VentasBLL.ActualizarEstatusHistorico(sucursal);

                return Ok(new ApiResponse { Success = true, Message = "Archivo(s) ZIP guardado(s) correctamente." });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                return SoltecErrorMessage(ex);
            }
        }



        [HttpGet("DescargarScriptZip")]
        public async Task<IActionResult> DescargarScriptZip([FromQuery] string sucursal)
        {
            try
            {
                if (string.IsNullOrEmpty(sucursal))
                    return BadRequest(new ApiResponse { Success = false, Message = "Parámetro 'sucursal' requerido." });

                // Ruta correcta usando el directorio actual de la aplicación
                string pathToSave = Path.Combine(_env.ContentRootPath, "Historicos");

                if (!Directory.Exists(pathToSave))
                    return NotFound(new ApiResponse { Success = false, Message = "No existe el directorio de históricos." });

                // Nombre esperado del archivo
                string fileName = $"{sucursal}_DatosHistoricos.zip";
                string filePath = Path.Combine(pathToSave, fileName);

                if (!System.IO.File.Exists(filePath))
                    return NotFound(new ApiResponse { Success = false, Message = $"No existe el archivo ZIP para la sucursal {sucursal}." });

                // Leer archivo y devolverlo
                var memory = new MemoryStream();
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    await stream.CopyToAsync(memory);
                }

                memory.Position = 0;

                return File(memory,
                            "application/zip",
                            fileName);
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }




        [Authorize]
		[HttpGet("DownloadFileZIP/{file}")]
		public async Task<IActionResult> DownloadFileZIP(string file)
		{
			try
			{
				string rutaZip = Path.Combine(Directory.GetCurrentDirectory(), "AppZIP", file);

				if (!System.IO.File.Exists(rutaZip))
				{
					return NotFound("El archivo no existe.");
				}

				var contenido = System.IO.File.ReadAllBytes(rutaZip);
				var tipoContenido = "application/zip";
				var nombreArchivo = file;

				return File(contenido, tipoContenido, nombreArchivo);

			}

			catch (Exception ex)
			{
				return SoltecErrorMessage(ex);
			}
		}


        [HttpGet("DescargaArchivoZIP/{file}")]
        public async Task<IActionResult> DescargaArchivoZIP(string file)
        {
            try
            {
                string rutaZip = Path.Combine(Directory.GetCurrentDirectory(), "AppZIP", file);

                if (!System.IO.File.Exists(rutaZip))
                {
                    return NotFound("El archivo no existe.");
                }

                var contenido = System.IO.File.ReadAllBytes(rutaZip);
                var tipoContenido = "application/zip";
                var nombreArchivo = file;

                return File(contenido, tipoContenido, nombreArchivo);

            }

            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [HttpGet("DescargaArchivoZIP_SimiPET/{file}")]
        public async Task<IActionResult> DescargaArchivoZIP_SimiPET(string file)
        {
            try
            {
                string rutaZip = Path.Combine(Directory.GetCurrentDirectory(), "AppZIP_SimiPET", file);

                if (!System.IO.File.Exists(rutaZip))
                {
                    return NotFound("El archivo no existe.");
                }

                var contenido = System.IO.File.ReadAllBytes(rutaZip);
                var tipoContenido = "application/zip";
                var nombreArchivo = file;

                return File(contenido, tipoContenido, nombreArchivo);

            }

            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }



        //[Authorize]
        [HttpPost("onLineSales")]
		public async Task<IActionResult> OnLineSales([FromBody] ProcesosOnLine data)
		{
			try
			{
				//await VentasBLL.OnLineSales(data);
				return Ok(new ApiResponse());
			}
			catch (Exception ex)
			{
				return SoltecErrorMessage(ex);
			}

		}

        //[RequestSizeLimit(209715200)]
        ////[Authorize]
        //[HttpPost("onLineSalesMultiple")]
        //public async Task<IActionResult> onLineSalesMultiple([FromBody] ProcesosOnLine data)
        //{
        //    try
        //    {
        //        return Ok(new ApiResponse());
        //    }
        //    catch (Exception ex)
        //    {
        //        return SoltecErrorMessage(ex);
        //    }

        //}


        [RequestSizeLimit(524288000)]
        //[Authorize]
        [HttpPost("SincronizaScriptUltimo")]
        public async Task<IActionResult> SincronizaScriptUltimo([FromBody] ProcesosOnLine data)
        {
            try
            {
                await VentasBLL.SincronizaScriptUltimo(data);
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [RequestSizeLimit(524288000)]
        //[Authorize]
        [HttpPost("ActualizaSucursalTransmision")]
        public async Task<IActionResult> ActualizaSucursalTransmision([FromBody] ProcesosOnLine data)
        {
            try
            {
                await VentasBLL.ActualizaSucursalTransmision(data);
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [RequestSizeLimit(524288000)]
        //[Authorize]
        [HttpPost("SincronizaScript_SimiPET")]
        public async Task<IActionResult> SincronizaScript_SimiPET([FromBody] ProcesosOnLine data)
        {
            try
            {
                await VentasBLL.SincronizaScript_SimiPET(data);
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }

        }

        

        [RequestSizeLimit(52428800)]
        //[Authorize]
        [HttpPost("onLineSalesSqlServerMultiple")]
        public async Task<IActionResult> onLineSalesSqlServerMultiple([FromBody] ProcesosOnLine data)
        {
            try
            {
                await VentasBLL.OnLineSalesSqlServerMultiple(data);
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

		// No implementado en ningún proyecto de la solución
		[Authorize]
		[HttpPost("getReporteExcelV2/{rfcEmpresa}")]
		public async Task<IActionResult> GetReporteExcelV2(string rfcEmpresa)
		{
			try
			{
				var reporteExcel = await VentasBLL.GetReporteExcel(rfcEmpresa);

				return File(reporteExcel, "application/octect-stream", $"ReporteVentasEnLiena.xlsx");

			}
			catch (Exception ex)
			{
				return SoltecErrorMessage(ex);
			}
		}

		// No implementado en ningún proyecto de la solución
		[Authorize]
		[HttpGet("getParametrosV2/{claveSimi}")]
		public async Task<IActionResult> GetParametrosV2(string claveSimi)
		{
			try
			{
				var getParametros = await VentasBLL.GetParametros(claveSimi);

				return Ok(new ApiResponse<ParametrosGenerales>(getParametros));
			}
			catch (Exception ex)
			{
				return SoltecErrorMessage(ex);
			}
		}
        #endregion


        #region QUITAR ESTOS SERVICIOS CUANDO YA SE TENGA TODO HOMOLOGADO.

        [HttpGet("getSQLScripts/{isOnLine}/{numeroSucursal}")]
        public async Task<IActionResult> GetSQLScripts(bool isOnLine, string numeroSucursal)
        {
            try
            {
                var getSqlScripts = await VentasBLL.GetSQLScripts(isOnLine, numeroSucursal);

                return Ok(new ApiResponse<SPOS_SQLScripts>(getSqlScripts));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [HttpGet("getSQLScriptsSQLite/{numeroSucursal}")]
        public async Task<IActionResult> GetSQLScriptsSQLite(string numeroSucursal)
        {
            try
            {
                var getSqlScripts = await VentasBLL.GetSQLScriptsSQLite(numeroSucursal);

                return Ok(new ApiResponse<SPOS_SQLScripts>(getSqlScripts));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [HttpGet("ObtieneScripts/{numeroSucursal}")]
        public async Task<IActionResult> ObtieneScripts(string numeroSucursal)
        {
            try
            {
                var getSqlScripts = await VentasBLL.ObtieneScripts(numeroSucursal);

                return Ok(new ApiResponse<SPOS_SQLScripts>(getSqlScripts));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [HttpGet("ObtieneScriptsConCargaInicial/{numeroSucursal}")]
        public async Task<IActionResult> ObtieneScriptsConCargaInicial(string numeroSucursal)
        {
            try
            {
                var getSqlScripts = await VentasBLL.ObtieneScriptsConCargaInicial(numeroSucursal);

                return Ok(new ApiResponse<SPOS_SQLScripts>(getSqlScripts));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        


        [HttpGet("ObtieneScripts_SIMIPET/{numeroSucursal}")]
        public async Task<IActionResult> ObtieneScripts_SIMIPET(string numeroSucursal)
        {
            try
            {
                var getSqlScripts = await VentasBLL.ObtieneScripts_SIMIPET(numeroSucursal);

                return Ok(new ApiResponse<SPOS_SQLScripts>(getSqlScripts));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        

        #endregion


        #region No implementado en ninguno de los proyectos en la solución.
        [HttpPost("getReporteExcel/{rfcEmpresa}")]
		public async Task<IActionResult> GetReporteExcel(string rfcEmpresa)
		{
			try
			{
				var reporteExcel = await VentasBLL.GetReporteExcel(rfcEmpresa);

				return File(reporteExcel, "application/octect-stream", $"ReporteVentasEnLiena.xlsx");

			}
			catch (Exception ex)
			{
				return SoltecErrorMessage(ex);
			}
		}

		[HttpGet("getParametros/{claveSimi}")]
		public async Task<IActionResult> GetParametros(string claveSimi)
		{
			try
			{
				var getParametros = await VentasBLL.GetParametros(claveSimi);

				return Ok(new ApiResponse<ParametrosGenerales>(getParametros));
			}
			catch (Exception ex)
			{
				return SoltecErrorMessage(ex);
			}
		}
		#endregion

	}
}
