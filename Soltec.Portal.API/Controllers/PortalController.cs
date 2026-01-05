using ApiBLL;
using ApiCommon.Api;
using ApiCommon.Entities.Ventas;
using Microsoft.AspNetCore.Mvc;

namespace Soltec.Portal.API.Controllers
{
    [Route("api/portal")]
    [ApiController]
    public class PortalController : BaseApiController
    {
        private readonly IConfiguration Configuration;

        private readonly ILogger<PortalBLL> Logger;

        public PortalBLL portal;

        public PortalController(IConfiguration configuration, ILogger<PortalBLL> logger)
        {
            Configuration = configuration;
            Logger = logger;

            portal = new PortalBLL(configuration, logger);
        }

        //[Authorize]
        [HttpGet("loadSQLScripts")]
        public async Task<IActionResult> LoadSQLScripts()
        {
            try
            {
                var scripts = await portal.LoadSQLScripts();

                return Ok(new ApiResponse<VentasEnLinea>(scripts));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }
        }


    }
}
