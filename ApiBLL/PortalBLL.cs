using ApiCommon.Entities.Ventas;
using ApiDAL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApiBLL
{
    public class PortalBLL
    {
        private readonly IConfiguration Configuration;

        private readonly ILogger<PortalBLL> Logger;

        public PortalDAL portal;

        public PortalBLL(IConfiguration configuration, ILogger<PortalBLL> logger)
        {
            Configuration = configuration;
            Logger = logger;

            portal = new PortalDAL(Configuration);
        }

        public async Task<List<VentasEnLinea>> LoadSQLScripts()
        {
            try
            {
                var scripts = await portal.LoadSQLScripts();

                return scripts;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Oucrrio un error al obtener los scripts de configuración.");
                return null;
            }
        }
    }
}
