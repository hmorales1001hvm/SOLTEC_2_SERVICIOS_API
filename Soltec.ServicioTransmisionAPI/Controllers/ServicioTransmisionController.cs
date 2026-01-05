using ApiBLL;
using ApiCommon.Api;
using ApiCommon.Entities.Transmision;
using ApiDAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Soltec.ServicioTransmisionAPI.Controllers
{
    [Route("api/transmision")]
    [ApiController]
    public class ServicioTransmisionController : BaseApiController
    {
        private readonly IConfiguration Configuration;

        private readonly ILogger<TransmisionDAL> Logger;

        private TransmisionBLL TransmisionBLL;
        
        public ServicioTransmisionController(IConfiguration configuration, ILogger<TransmisionDAL> logger)
        {
            Configuration = configuration;
            Logger = logger;

            TransmisionBLL = new TransmisionBLL(Configuration, logger);
        }


        #region Requieren de TOKEN.
        [Authorize]
        [HttpGet("isOnlineV2")]
        public async Task<IActionResult> IsOnlineV2()
        {
            try
            {
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [Authorize]
        [HttpPost("markOnLineV2")]
        public async Task<IActionResult> MarkOnLineV2([FromBody] MarkOnlineBody markOnlineBody)
        {
            try
            {
                await TransmisionBLL.MarkOnLine(markOnlineBody);

                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [Authorize]
        [HttpGet("getProcesosV2")]
        public async Task<IActionResult> GetProcesosV2()
        {
            try
            {
                var getProcesos = (await TransmisionBLL.GetProcesos()).ToList();

                return Ok(new ApiResponse<ServicioProcesos>(getProcesos));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [Authorize]
        [HttpGet("getConfiguracionV2/{sucursal}")]
        public async Task<IActionResult> GetConfiguracionV2(string sucursal)
        {
            try
            {
                var getConfiguraiton = await TransmisionBLL.GetConfiguracion(sucursal);

                return Ok(new ApiResponse<ServicioConfig>(getConfiguraiton));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [Authorize]
        [HttpGet("getVersionesAppV2")]
        public async Task<IActionResult> GetVersionesAppV2()
        {
            try
            {
                var getVersiones = (await TransmisionBLL.GetVersionesApp()).ToList();

                return Ok(new ApiResponse<VersionesApp>(getVersiones));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


        [HttpGet("ObtieneVersiones")]
        public async Task<IActionResult> ObtieneVersiones()
        {
            try
            {
                var getVersiones = (await TransmisionBLL.ObtieneVersiones()).ToList();

                return Ok(new ApiResponse<VersionesApp>(getVersiones));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("ObtieneVersiones_SimiPET")]
        public async Task<IActionResult> ObtieneVersiones_SimiPET()
        {
            try
            {
                var getVersiones = (await TransmisionBLL.ObtieneVersiones_SimiPET()).ToList();

                return Ok(new ApiResponse<VersionesApp>(getVersiones));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }
        #endregion



        #region QUITAR ESTOS SERVICIOS CUANDO YA SE TENGA TODO HOMOLOGADO.
        [HttpGet("isOnline")]
        public async Task<IActionResult> IsOnline()
        {
            try
            {
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("isOnline_SimiPET")]
        public async Task<IActionResult> IsOnline_SimiPET()
        {
            try
            {
                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [HttpPost("markOnLine")]
        public async Task<IActionResult> MarkOnLine([FromBody] MarkOnlineBody markOnlineBody)
        {
            try
            {
                await TransmisionBLL.MarkOnLine(markOnlineBody);

                return Ok(new ApiResponse());
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("getProcesos")]
        public async Task<IActionResult> GetProcesos()
        {
            try
            {
                var getProcesos = (await TransmisionBLL.GetProcesos()).ToList();

                return Ok(new ApiResponse<ServicioProcesos>(getProcesos));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("getConfiguracion/{sucursal}")]
        public async Task<IActionResult> GetConfiguracion(string sucursal)
        {
            try
            {
                var getConfiguraiton = await TransmisionBLL.GetConfiguracion(sucursal);

                return Ok(new ApiResponse<ServicioConfig>(getConfiguraiton));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("getVersionesApp")]
        public async Task<IActionResult> GetVersionesApp()
        {
            try
            {
                var getVersiones = (await TransmisionBLL.GetVersionesApp()).ToList();

                return Ok(new ApiResponse<VersionesApp>(getVersiones));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }

        [HttpGet("getMonitorDeApps")]
        public async Task<IActionResult> GetMonitorDeApps()
        {
            try
            {
                var getVersiones = (await TransmisionBLL.GetMonitorDeApps()).ToList();

                return Ok(new ApiResponse<MonitorDeApps>(getVersiones));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }
        #endregion


    }
}
