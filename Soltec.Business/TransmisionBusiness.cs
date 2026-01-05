using Soltec.Entities.Transmision;
using Soltec.DB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Soltec.Business
{
    public class TransmisionBusiness
    {
        private readonly IConfiguration Configuration;

        private readonly ILogger<TransmisionDB> Logger;

        public TransmisionDB TransmisionDAL;

        public TransmisionBusiness(IConfiguration configuration, ILogger<TransmisionDB> logger)
        {
            Configuration = configuration;
            Logger = logger;

            TransmisionDAL = new TransmisionDB(configuration,logger);
        }

        public async Task MarkOnLine(MarkOnlineBody markOnlineBody)
        {
            try
            {
                await TransmisionDAL.MarkOnLine(markOnlineBody);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Sucursal: {markOnlineBody.Sucursal} - Error al registrar la sucursal con la version: {markOnlineBody.Version1}");
            }
        }

        public async Task<IEnumerable<ServicioProcesos>> GetProcesos()
        {
            try
            {
                var getProcesos = await TransmisionDAL.GetProcesos();

                return getProcesos;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Ocurrio un error al obtener los procesos.");
                return null;
            }
        }

        public async Task<IEnumerable<VersionesApp>> GetVersionesApp()
        {
            try
            {
                var result = await TransmisionDAL.GetVersionesApp();

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Ocurrio un error al obtener las lista de versiones.");
				return null;
			}
            
        }

        public async Task<IEnumerable<VersionesApp>> ObtieneVersiones()
        {
            try
            {
                var result = await TransmisionDAL.ObtieneVersiones();

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Ocurrio un error al obtener las lista de versiones.");
                return null;
            }

        }

        public async Task<IEnumerable<VersionesApp>> ObtieneVersiones_SimiPET()
        {
            try
            {
                var result = await TransmisionDAL.ObtieneVersiones_SimiPET();

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Ocurrio un error al obtener las lista de versiones.");
                return null;
            }

        }

        public async Task<IEnumerable<MonitorDeApps>> GetMonitorDeApps()
        {
            try
            {
                var result = await TransmisionDAL.GetMonitorDeApps();

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Ocurrio un error al obtener las lista de Monitor de Apps.");
                return null;
            }

        }

    }
}
