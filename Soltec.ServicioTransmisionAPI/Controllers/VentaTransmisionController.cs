using ApiCommon.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Soltec.Business;
using Soltec.DB;
using Soltec.Entidades.Ventas;
using Soltec.Entities.Ventas;
using System.IO.Compression;

namespace Soltec.ServicioTransmisionAPI.Controllers
{
    [Route("api/venta")]
    [ApiController]
    public class VentaTransmisionController : BaseApiController
    {
        private readonly IConfiguration Configuration;
        public VentasBusiness VentasBusiness;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<VentaTransmisionController> _logger;


        public VentaTransmisionController(IConfiguration configuration,
                                            ILogger<VentasDB> logger,
                                            ILogger<SetDeTransmisionesDB> logger2,
                                            ILogger<ConexionCacheRepository> logger3,
                                            ILogger<VentaTransmisionController> controllerLogger,
                                            IWebHostEnvironment env)
        {
            Configuration = configuration;
            _logger = controllerLogger;
            _env = env;

            VentasBusiness = new VentasBusiness(Configuration, logger, logger2,logger3);
        }


        #region Requieren Token

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
        public async Task<IActionResult> UploadFileZIP([FromHeader(Name = "Sucursal")] string? sucursalHeader = null)
        {
            try
            {
                //_logger.LogInformation("UploadFileZIP iniciado | HeaderSucursal={HeaderSucursal}", sucursalHeader);

                if (!Request.Form.Files.Any())
                    return Ok(new ApiResponse());

                string pathToSave = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

                Directory.CreateDirectory(pathToSave);
                Directory.CreateDirectory(Path.Combine(pathToSave, "Operativas"));
                Directory.CreateDirectory(Path.Combine(pathToSave, "Catalogos"));

                foreach (IFormFile file in Request.Form.Files)
                {
                    string destino = file.FileName.Contains("Operativas")
                        ? Path.Combine(pathToSave, "Operativas", file.FileName)
                        : Path.Combine(pathToSave, "Catalogos", file.FileName);

                    using var stream = new FileStream(destino, FileMode.Create);
                    await file.CopyToAsync(stream);
                }

                //_logger.LogInformation(
                //    "UploadFileZIP finalizado correctamente | HeaderSucursal={HeaderSucursal}",
                //    sucursalHeader
                //);

                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en UploadFileZIP | HeaderSucursal={HeaderSucursal}", sucursalHeader
                );
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
                //_logger.LogInformation($"Ruta del histórico: {pathToSave}, sucursal: {sucursal}");

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

                await VentasBusiness.ActualizarEstatusHistorico(sucursal);

                return Ok(new ApiResponse { Success = true, Message = "Archivo(s) ZIP guardado(s) correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en SincronizaScriptZipAsync: {sucursal}");
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


        [HttpGet("DescargarOnDemandZip")]
        public async Task<IActionResult> DescargarOnDemandZip()
        {
            try
            {
                string pathHistoricos = Path.Combine(_env.ContentRootPath, "Historicos");

                if (!Directory.Exists(pathHistoricos))
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Message = "No existe el directorio de históricos."
                    });
                }

                // Buscar archivos que contengan _DatosOnDemand
                var archivos = Directory.GetFiles(pathHistoricos, "*_DatosOnDemand*", SearchOption.TopDirectoryOnly);
                if (archivos.Length == 0)
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Message = "No se encontraron archivos OnDemand."
                    });
                }

                // Crear ZIP en memoria
                var memoryStream = new MemoryStream();

                using (var zip = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var archivo in archivos)
                    {
                        var zipEntry = zip.CreateEntry(Path.GetFileName(archivo), CompressionLevel.Fastest);

                        using var entryStream = zipEntry.Open();
                        using var fileStream = new FileStream(archivo, FileMode.Open, FileAccess.Read);

                        fileStream.CopyTo(entryStream);
                    }
                }

                memoryStream.Position = 0;

                return File(
                    memoryStream,
                    "application/zip",
                    $"DatosOnDemand_{DateTime.Now:yyyyMMddHHmmss}.zip"
                );
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [HttpDelete("EliminarOnDemandZip")]
        public IActionResult EliminarOnDemandZip([FromQuery] string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Parámetro 'fileName' requerido."
                    });
                }

                // Seguridad básica: evitar rutas relativas
                if (fileName.Contains("..") || fileName.Contains(Path.DirectorySeparatorChar))
                {
                    return BadRequest(new ApiResponse
                    {
                        Success = false,
                        Message = "Nombre de archivo inválido."
                    });
                }

                string historicosPath = Path.Combine(_env.ContentRootPath, "Historicos");
                string filePath = Path.Combine(historicosPath, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Message = "El archivo no existe."
                    });
                }

                System.IO.File.Delete(filePath);

                //_logger.LogInformation("ZIP eliminado correctamente: {file}", fileName);

                return Ok(new ApiResponse
                {
                    Success = true,
                    Message = "Archivo eliminado correctamente."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando ZIP {file}", fileName);
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

        [HttpGet("LecturaLog")]
        public async Task<IActionResult> LecturaLog()
        {
            try
            {
                string basePath = "/soltec2files/LogsAPI";
                string fileName = "Log.txt";

                string path = Path.Combine(basePath, fileName);

                if (!System.IO.File.Exists(path))
                    return NotFound("Log no encontrado en: " + path);

                var lineas = System.IO.File
                                     .ReadLines(path)
                                     .Reverse()
                                     .Take(5000)
                                     .Reverse();

                return Content(string.Join("\n", lineas), "text/plain");
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("LecturaSucursales")]
        public async Task<IActionResult> LecturaSucursales([FromQuery] int? idEmpresa = null)
        {
            try
            {
                string path = "/soltec2files/RutaCache/CacheConexiones.json";

                if (!System.IO.File.Exists(path))
                    return NotFound("CacheConexiones no encontrado en: " + path);

                string jsonString = await System.IO.File.ReadAllTextAsync(path);

                // Deserializamos a Diccionario<string, ConexionSucursal>
                var dicSucursales = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ConexionSucursal>>(jsonString);

                // Si se pasó IdEmpresa, filtramos
                if (idEmpresa.HasValue)
                {
                    dicSucursales = dicSucursales
                        .Where(kv => kv.Value.IdEmpresa == idEmpresa.Value)
                        .ToDictionary(kv => kv.Key, kv => kv.Value);
                }

                return Ok(dicSucursales);
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [HttpPost("EliminarCacheSucursales")]
        public IActionResult EliminarCacheSucursales()
        {
            try
            {
                string path = "/soltec2files/RutaCache/CacheConexiones.json";

                if (!System.IO.File.Exists(path))
                    return NotFound(new
                    {
                        success = false,
                        message = "CacheConexiones no encontrado en: " + path
                    });

                System.IO.File.Delete(path);

                return Ok(new
                {
                    success = true,
                    message = "CacheConexiones.json eliminado correctamente."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error al eliminar el archivo.",
                    error = ex.Message
                });
            }
        }



        [RequestSizeLimit(524288000)]
        [HttpPost("SincronizaScriptUltimo")]
        public async Task<IActionResult> SincronizaScriptUltimo([FromBody] ProcesosOnLine data, [FromHeader(Name = "Sucursal")] string? sucursalHeader = null)
        {
            try
            {
                //_logger.LogInformation("SincronizaScriptUltimo iniciado | HeaderSucursal={HeaderSucursal}", sucursalHeader);

                await VentasBusiness.SincronizaScriptUltimo(data);

                //_logger.LogInformation("SincronizaScriptUltimo finalizado correctamente | HeaderSucursal={HeaderSucursal}", sucursalHeader);

                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SincronizaScriptUltimo | HeaderSucursal={HeaderSucursal}", sucursalHeader);

                return SoltecErrorMessage(ex);
            }
        }

        [RequestSizeLimit(524288000)]
        [HttpPost("ActualizaSucursalTransmision")]
        public async Task<IActionResult> ActualizaSucursalTransmision([FromBody] ProcesosOnLine data, [FromHeader(Name = "Sucursal")] string? sucursalHeader = null)
        {
            try
            {
                //_logger.LogInformation("ActualizaSucursalTransmision iniciado | HeaderSucursal={HeaderSucursal}", sucursalHeader);

                await VentasBusiness.ActualizaSucursalTransmision(data);

                //_logger.LogInformation("ActualizaSucursalTransmision finalizado correctamente | HeaderSucursal={HeaderSucursal}", sucursalHeader);

                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ActualizaSucursalTransmision | HeaderSucursal={HeaderSucursal}", sucursalHeader);

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
                await VentasBusiness.SincronizaScript_SimiPET(data);
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
                var reporteExcel = await VentasBusiness.GetReporteExcel(rfcEmpresa);

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
                var getParametros = await VentasBusiness.GetParametros(claveSimi);

                return Ok(new ApiResponse<ParametrosGenerales>(getParametros));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }
        #endregion


        #region QUITAR ESTOS SERVICIOS CUANDO YA SE TENGA TODO HOMOLOGADO.
        [HttpGet("ObtieneScriptsConCargaInicial/{numeroSucursal}")]
        public async Task<IActionResult> ObtieneScriptsConCargaInicial(string numeroSucursal)
        {
            try
            {
                //_logger.LogInformation("ObtieneScriptsConCargaInicial iniciado | Sucursal={Sucursal}", numeroSucursal);
                var getSqlScripts = await VentasBusiness.ObtieneScriptsConCargaInicial(numeroSucursal);
                //_logger.LogInformation("ObtieneScriptsConCargaInicial finalizado correctamente | Sucursal={Sucursal}", numeroSucursal);

                return Ok(new ApiResponse<SPOS_SQLScripts>(getSqlScripts));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtieneScriptsConCargaInicial | Sucursal={Sucursal}", numeroSucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("ObtieneScripts_SIMIPET/{numeroSucursal}")]
        public async Task<IActionResult> ObtieneScripts_SIMIPET(string numeroSucursal)
        {
            try
            {
                var getSqlScripts = await VentasBusiness.ObtieneScripts_SIMIPET(numeroSucursal);

                return Ok(new ApiResponse<SPOS_SQLScripts>(getSqlScripts));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en SincronizaScriptUltimo: {numeroSucursal}");
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
                var reporteExcel = await VentasBusiness.GetReporteExcel(rfcEmpresa);

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
                var getParametros = await VentasBusiness.GetParametros(claveSimi);

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
