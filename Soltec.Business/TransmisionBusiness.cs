using Soltec.Entities.Transmision;
using Soltec.DB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Soltec.Business
{
    public class TransmisionBusiness
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TransmisionBusiness> _logger;

        public TransmisionDB TransmisionDAL;

        public TransmisionBusiness(IConfiguration configuration,
                                   ILogger<TransmisionBusiness> logger,
                                   ILoggerFactory loggerFactory) // LoggerFactory para DAL
        {
            _configuration = configuration;
            _logger = logger;

            // Crear logger tipado para TransmisionDB
            var transmisionDbLogger = loggerFactory.CreateLogger<TransmisionDB>();
            TransmisionDAL = new TransmisionDB(configuration, transmisionDbLogger);
        }

        public async Task MarkOnLine(MarkOnlineBody markOnlineBody)
        {
            try
            {
                await TransmisionDAL.MarkOnLine(markOnlineBody);
                _logger.LogInformation("Sucursal {@Sucursal} registrada con la version {@Version1}",
                                        markOnlineBody.Sucursal, markOnlineBody.Version1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar la sucursal {@Sucursal} con la version {@Version1}",
                                 markOnlineBody.Sucursal, markOnlineBody.Version1);
            }
        }

        public async Task<IEnumerable<ServicioProcesos>> GetProcesos()
        {
            try
            {
                var procesos = await TransmisionDAL.GetProcesos();
                _logger.LogInformation("Se obtuvieron {@Count} procesos", procesos?.Count() ?? 0);
                return procesos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error al obtener los procesos");
                return Enumerable.Empty<ServicioProcesos>();
            }
        }

        public async Task<IEnumerable<VersionesApp>> GetVersionesApp()
        {
            try
            {
                var result = await TransmisionDAL.GetVersionesApp();
                _logger.LogInformation("Se obtuvieron {@Count} versiones de app", result?.Count() ?? 0);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lista de versiones de app");
                return Enumerable.Empty<VersionesApp>();
            }
        }

        public async Task<IEnumerable<VersionesApp>> ObtieneVersiones()
        {
            try
            {
                var result = await TransmisionDAL.ObtieneVersiones();
                _logger.LogInformation("Se obtuvieron {@Count} versiones activas", result?.Count() ?? 0);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error al obtener las versiones activas");
                return Enumerable.Empty<VersionesApp>();
            }
        }

        public async Task<IEnumerable<VersionesApp>> ObtieneVersiones_SimiPET()
        {
            try
            {
                var result = await TransmisionDAL.ObtieneVersiones_SimiPET();
                _logger.LogInformation("Se obtuvieron {@Count} versiones activas SimiPET", result?.Count() ?? 0);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error al obtener las versiones activas SimiPET");
                return Enumerable.Empty<VersionesApp>();
            }
        }

        public async Task<IEnumerable<MonitorDeApps>> GetMonitorDeApps()
        {
            try
            {
                var result = await TransmisionDAL.GetMonitorDeApps();
                _logger.LogInformation("Se obtuvieron {@Count} registros de Monitor de Apps", result?.Count() ?? 0);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocurrió un error al obtener Monitor de Apps");
                return Enumerable.Empty<MonitorDeApps>();
            }
        }
    }
}
