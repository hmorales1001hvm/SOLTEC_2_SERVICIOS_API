using Soltec.Business;
using ApiCommon.Api;
using Soltec.Entities.Transmision;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Soltec.DB;

namespace Soltec.ServicioTransmisionAPI.Controllers
{
    [Route("api/transmision")]
    [ApiController]
    public class ServicioTransmisionController : BaseApiController
    {
        private readonly IConfiguration Configuration;

        private readonly ILogger<TransmisionDB> Logger;

        private TransmisionBusiness TransmisionBusiness;
        
        public ServicioTransmisionController(IConfiguration configuration, ILogger<TransmisionDB> logger)
        {
            Configuration = configuration;
            Logger = logger;

            TransmisionBusiness = new TransmisionBusiness(Configuration, logger);
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
                await TransmisionBusiness.MarkOnLine(markOnlineBody);

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
                var getProcesos = (await TransmisionBusiness.GetProcesos()).ToList();

                return Ok(new ApiResponse<ServicioProcesos>(getProcesos));
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
                var getVersiones = (await TransmisionBusiness.GetVersionesApp()).ToList();

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
                var getVersiones = (await TransmisionBusiness.ObtieneVersiones()).ToList();

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
                var getVersiones = (await TransmisionBusiness.ObtieneVersiones_SimiPET()).ToList();

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
                await TransmisionBusiness.MarkOnLine(markOnlineBody);

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
                var getProcesos = (await TransmisionBusiness.GetProcesos()).ToList();

                return Ok(new ApiResponse<ServicioProcesos>(getProcesos));
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
                var getVersiones = (await TransmisionBusiness.GetVersionesApp()).ToList();

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
                var getVersiones = (await TransmisionBusiness.GetMonitorDeApps()).ToList();

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
