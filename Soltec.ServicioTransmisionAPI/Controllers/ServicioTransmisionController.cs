using ApiCommon.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Soltec.Business;
using Soltec.Entities.Transmision;

namespace Soltec.ServicioTransmisionAPI.Controllers
{
    [Route("api/transmision")]
    [ApiController]
    public class ServicioTransmisionController : BaseApiController
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ServicioTransmisionController> _logger;
        private readonly TransmisionBusiness _transmisionBusiness;

        public ServicioTransmisionController(
            IConfiguration configuration,
            ILogger<ServicioTransmisionController> logger,
            ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _logger = logger;

            var transmisionBusinessLogger = loggerFactory.CreateLogger<TransmisionBusiness>();
            _transmisionBusiness = new TransmisionBusiness(configuration, transmisionBusinessLogger, loggerFactory);
        }

        private void LogSucursal(string? sucursal, string mensaje)
        {
            if (!string.IsNullOrEmpty(sucursal))
            {
                _logger.LogInformation("{Mensaje} - Sucursal: {Sucursal}", mensaje, sucursal);
            }
        }

        #region Requieren TOKEN
        [Authorize]
        [HttpGet("isOnlineV2")]
        public async Task<IActionResult> IsOnlineV2([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "IsOnlineV2 solicitado");
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en IsOnlineV2 - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [Authorize]
        [HttpPost("markOnLineV2")]
        public async Task<IActionResult> MarkOnLineV2(
            [FromBody] MarkOnlineBody markOnlineBody,
            [FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "MarkOnLineV2 solicitado");
                await _transmisionBusiness.MarkOnLine(markOnlineBody);

                _logger.LogInformation("Sucursal {Sucursal} registrada correctamente", markOnlineBody.Sucursal);
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en MarkOnLineV2 para sucursal {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [Authorize]
        [HttpGet("getProcesosV2")]
        public async Task<IActionResult> GetProcesosV2([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "GetProcesosV2 solicitado");
                var getProcesos = (await _transmisionBusiness.GetProcesos()).ToList();

                _logger.LogInformation("Se obtuvieron {Count} procesos", getProcesos.Count);
                return Ok(new ApiResponse<ServicioProcesos>(getProcesos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener procesos - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [Authorize]
        [HttpGet("getVersionesAppV2")]
        public async Task<IActionResult> GetVersionesAppV2([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "GetVersionesAppV2 solicitado");
                var getVersiones = (await _transmisionBusiness.GetVersionesApp()).ToList();

                _logger.LogInformation("Se obtuvieron {Count} versiones de app", getVersiones.Count);
                return Ok(new ApiResponse<VersionesApp>(getVersiones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener versiones de app - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("ObtieneVersiones")]
        public async Task<IActionResult> ObtieneVersiones([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "ObtieneVersiones solicitado");
                var getVersiones = (await _transmisionBusiness.ObtieneVersiones()).ToList();

                _logger.LogInformation("Se obtuvieron {Count} versiones activas", getVersiones.Count);
                return Ok(new ApiResponse<VersionesApp>(getVersiones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtieneVersiones - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("ObtieneVersiones_SimiPET")]
        public async Task<IActionResult> ObtieneVersiones_SimiPET([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "ObtieneVersiones_SimiPET solicitado");
                var getVersiones = (await _transmisionBusiness.ObtieneVersiones_SimiPET()).ToList();

                _logger.LogInformation("Se obtuvieron {Count} versiones SimiPET", getVersiones.Count);
                return Ok(new ApiResponse<VersionesApp>(getVersiones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtieneVersiones_SimiPET - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }
        #endregion

        #region Métodos Legacy (opcional mantener)
        [HttpGet("isOnline")]
        public async Task<IActionResult> IsOnline([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "IsOnline ejecutado");
                _logger.LogInformation("Servicio IsOnline ejecutado correctamente");
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en IsOnline - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("isOnline_SimiPET")]
        public async Task<IActionResult> IsOnline_SimiPET([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "IsOnline_SimiPET ejecutado");
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en IsOnline_SimiPET - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [HttpPost("markOnLine")]
        public async Task<IActionResult> MarkOnLine(
            [FromBody] MarkOnlineBody markOnlineBody,
            [FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "MarkOnLine solicitado");
                await _transmisionBusiness.MarkOnLine(markOnlineBody);

                _logger.LogInformation("Sucursal {Sucursal} registrada correctamente", markOnlineBody.Sucursal);
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en MarkOnLine - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("getProcesos")]
        public async Task<IActionResult> GetProcesos([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "GetProcesos solicitado");
                var getProcesos = (await _transmisionBusiness.GetProcesos()).ToList();

                _logger.LogInformation("Se obtuvieron {Count} procesos", getProcesos.Count);
                return Ok(new ApiResponse<ServicioProcesos>(getProcesos));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetProcesos - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("getVersionesApp")]
        public async Task<IActionResult> GetVersionesApp([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "GetVersionesApp solicitado");
                var getVersiones = (await _transmisionBusiness.GetVersionesApp()).ToList();

                _logger.LogInformation("Se obtuvieron {Count} versiones de app", getVersiones.Count);
                return Ok(new ApiResponse<VersionesApp>(getVersiones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetVersionesApp - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("getMonitorDeApps")]
        public async Task<IActionResult> GetMonitorDeApps([FromHeader(Name = "Sucursal")] string? sucursal = null)
        {
            try
            {
                LogSucursal(sucursal, "GetMonitorDeApps solicitado");
                var getVersiones = (await _transmisionBusiness.GetMonitorDeApps()).ToList();

                _logger.LogInformation("Se obtuvieron {Count} apps en monitor", getVersiones.Count);
                return Ok(new ApiResponse<MonitorDeApps>(getVersiones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetMonitorDeApps - Sucursal: {Sucursal}", sucursal);
                return SoltecErrorMessage(ex);
            }
        }
        #endregion
    }
}






//using ApiCommon.Api;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;
//using Soltec.Business;
//using Soltec.DB;
//using Soltec.Entities.Transmision;

//namespace Soltec.ServicioTransmisionAPI.Controllers
//{
//    [Route("api/transmision")]
//    [ApiController]
//    public class ServicioTransmisionController : BaseApiController
//    {
//        private readonly IConfiguration Configuration;

//        private readonly ILogger<ServicioTransmisionController> _logger;

//        private TransmisionBusiness TransmisionBusiness;

//        public ServicioTransmisionController(IConfiguration configuration,
//                                      ILogger<ServicioTransmisionController> logger,
//                                      ILoggerFactory loggerFactory)
//        {
//            _logger = logger;

//            var transmisionBusinessLogger = loggerFactory.CreateLogger<TransmisionBusiness>();
//            TransmisionBusiness = new TransmisionBusiness(configuration, transmisionBusinessLogger, loggerFactory);
//        }



//        #region Requieren de TOKEN.
//        [Authorize]
//        [HttpGet("isOnlineV2")]
//        public async Task<IActionResult> IsOnlineV2()
//        {
//            try
//            {
//                return Ok(new ApiResponse());
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }

//        [Authorize]
//        [HttpPost("markOnLineV2")]
//        public async Task<IActionResult> MarkOnLineV2([FromBody] MarkOnlineBody markOnlineBody)
//        {
//            try
//            {
//                await TransmisionBusiness.MarkOnLine(markOnlineBody);

//                return Ok(new ApiResponse());
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }

//        [Authorize]
//        [HttpGet("getProcesosV2")]
//        public async Task<IActionResult> GetProcesosV2()
//        {
//            try
//            {
//                var getProcesos = (await TransmisionBusiness.GetProcesos()).ToList();

//                return Ok(new ApiResponse<ServicioProcesos>(getProcesos));
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }


//        [Authorize]
//        [HttpGet("getVersionesAppV2")]
//        public async Task<IActionResult> GetVersionesAppV2()
//        {
//            try
//            {
//                var getVersiones = (await TransmisionBusiness.GetVersionesApp()).ToList();

//                return Ok(new ApiResponse<VersionesApp>(getVersiones));
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }


//        [HttpGet("ObtieneVersiones")]
//        public async Task<IActionResult> ObtieneVersiones()
//        {
//            try
//            {
//                var getVersiones = (await TransmisionBusiness.ObtieneVersiones()).ToList();

//                return Ok(new ApiResponse<VersionesApp>(getVersiones));
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }

//        [HttpGet("ObtieneVersiones_SimiPET")]
//        public async Task<IActionResult> ObtieneVersiones_SimiPET()
//        {
//            try
//            {
//                var getVersiones = (await TransmisionBusiness.ObtieneVersiones_SimiPET()).ToList();

//                return Ok(new ApiResponse<VersionesApp>(getVersiones));
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }
//        #endregion



//        #region QUITAR ESTOS SERVICIOS CUANDO YA SE TENGA TODO HOMOLOGADO.
//        [HttpGet("isOnline")]
//        public async Task<IActionResult> IsOnline()
//        {
//            try
//            {
//                _logger.LogInformation("Servicio IsOnline ejecutado correctamente");

//                return Ok(new ApiResponse());
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error en IsOnline");
//                return SoltecErrorMessage(ex);
//            }
//        }


//        [HttpGet("isOnline_SimiPET")]
//        public async Task<IActionResult> IsOnline_SimiPET()
//        {
//            try
//            {
//                return Ok(new ApiResponse());
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }

//        [HttpPost("markOnLine")]
//        public async Task<IActionResult> MarkOnLine([FromBody] MarkOnlineBody markOnlineBody)
//        {
//            try
//            {
//                await TransmisionBusiness.MarkOnLine(markOnlineBody);

//                return Ok(new ApiResponse());
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }

//        [HttpGet("getProcesos")]
//        public async Task<IActionResult> GetProcesos()
//        {
//            try
//            {
//                var getProcesos = (await TransmisionBusiness.GetProcesos()).ToList();

//                return Ok(new ApiResponse<ServicioProcesos>(getProcesos));
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }


//        [HttpGet("getVersionesApp")]
//        public async Task<IActionResult> GetVersionesApp()
//        {
//            try
//            {
//                var getVersiones = (await TransmisionBusiness.GetVersionesApp()).ToList();

//                return Ok(new ApiResponse<VersionesApp>(getVersiones));
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }

//        [HttpGet("getMonitorDeApps")]
//        public async Task<IActionResult> GetMonitorDeApps()
//        {
//            try
//            {
//                var getVersiones = (await TransmisionBusiness.GetMonitorDeApps()).ToList();

//                return Ok(new ApiResponse<MonitorDeApps>(getVersiones));
//            }
//            catch (Exception ex)
//            {
//                return SoltecErrorMessage(ex);
//            }
//        }
//        #endregion


//    }
//}
