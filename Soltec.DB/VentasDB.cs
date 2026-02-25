using Soltec.Entities.Ventas;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Soltec.Common.Logger;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace Soltec.DB
{
    public class VentasDB
    {
        private readonly IConfiguration Configuration;
        private readonly ILogger<VentasDB> _Logger;
        private bool IMP = false;
        private bool INF = false;
        private bool WRN = false;

        public VentasDB(IConfiguration configuration, ILogger<VentasDB> logger)
        {
            Configuration = configuration;
            _Logger = logger;

            IMP = Configuration.GetValue("settingsAPIs:MostrarLogIMP", false);
            INF = Configuration.GetValue("settingsAPIs:MostrarLogINF", false);
            WRN = Configuration.GetValue("settingsAPIs:MostrarLogWRN", false);
        }

        public async Task<List<SPOS_SQLScripts>> ObtieneScriptsConCargaInicial(string numeroSucursal)
        {
            var scripts = new List<SPOS_SQLScripts>();
            if (IMP)
                Logger.Important($"ObtieneScriptsConCargaInicial - Obteniendo script para la sucursal {numeroSucursal}");

            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaRealOrquestador").Value;
            var queryScripts = @"SELECT    SS.IdSqlScript,
                                           SS.IsOnLine, 
                                           SS.SQLScript ,
                                           SS.Nombre,
                                           SS.Tipo,
                                           SS.Condicion,
                                           IFNULL(SS.ValorIncrementoDecremento,0) ValorIncrementoDecremento ,
                                           SS.EsAPI,
                                           SS.EsCatalogo,
                                           EsSp,
                                           IFNULL(C.Param1,IFNULL(SS.Param1,'')) Param1,
                                           IFNULL(C.Param2,IFNULL(SS.Param2,'')) Param2,
                                           IFNULL(C.Param3,IFNULL(SS.Param3,'')) Param3,
                                           IFNULL(C.Param4,IFNULL(SS.Param4,'')) Param4,
                                           IFNULL(C.Param5,IFNULL(SS.Param5,'')) Param5,
                                           IFNULL(C.Param6,IFNULL(SS.Param6,'')) Param6,
                                           IFNULL(C.Param7,IFNULL(SS.Param7,'')) Param7,
                                           IFNULL(C.Param8,IFNULL(SS.Param8,'')) Param8,
                                           IFNULL(C.Param9,IFNULL(SS.Param9,'')) Param9,
                                           IFNULL(C.Param10,IFNULL(SS.Param10,'')) Param10,
                                           MultiplesTablas,
                                           TiempoTransmision,
                                           Carga_SQLServer_SQLite,
                                           ResetearTablaSQLite,
                                           SC.IdSucursal,
                                           X.HostName, 
                                           X.UserName, 
                                           X.Password, 
                                           X.DatabaseName,
                                           SS.MultiFra,
                                           CASE WHEN X.Activo = 0 THEN '' ELSE IFNULL(X.UrlSQS,'') END UrlSQS,
                                           SC.ConTransmisionInicial,
                                           IFNULL(SC.TicketsFaltantes,'') TicketsFaltantes,
                                           'NORMAL' TipoCarga,
                                           X.UrlAPIs 
                                FROM spos_sqlscripts                    SS 
                                INNER JOIN spos_SqlScriptsPorEmpresa    SQ ON SS.IdSQLScript = SQ.IdSqlScript
                                INNER JOIN sucursal                     SC ON SQ.IdEmpresa = SC.idEmpresa 
                                INNER JOIN catempresa                   EMP ON SC.IdEmpresa = EMP.idEmpresa
                                LEFT JOIN soltec2_orquestador_servidormysql_detalle X ON SQ.IdEmpresa = X.IdEmpresa 
                                LEFT JOIN spos_sqlscriptsdetalle C ON SS.IdSQLScript = C.IdSQLScript AND SC.claveSimi = C.NumeroSucursal AND C.Activo = 1 AND C.EsHistorico = 0 AND C.Desde <= CURDATE() AND C.Hasta >= CURDATE()
                                WHERE SS.Activo = 1 AND Tipo IN('DTS') AND SC.claveSimi='" + numeroSucursal + @"'

                                UNION

                                SELECT     SS.IdSqlScript,
                                           SS.IsOnLine, 
                                           SS.SQLScript ,
                                           SS.Nombre,
                                           SS.Tipo,
                                           SS.Condicion,
                                           IFNULL(SS.ValorIncrementoDecremento,0) ValorIncrementoDecremento ,
                                           SS.EsAPI,
                                           SS.EsCatalogo,
                                           EsSp,
                                           sh.Desde Param1,
                                           sh.Hasta Param2,
                                           IFNULL(SS.Param3,'') Param3,
                                           IFNULL(SS.Param4,'') Param4,
                                           IFNULL(SS.Param5,'') Param5,
                                           IFNULL(SS.Param6,'') Param6,
                                           IFNULL(SS.Param7,'') Param7,
                                           IFNULL(SS.Param8,'') Param8,
                                           IFNULL(SS.Param9,'') Param9,
                                           IFNULL(SS.Param10,'') Param10,
                                           MultiplesTablas,
                                           TiempoTransmision,
                                           Carga_SQLServer_SQLite,
                                           ResetearTablaSQLite,
                                           SC.IdSucursal,
                                           X.HostName, 
                                           X.UserName, 
                                           X.Password, 
                                           X.DatabaseName,
                                           SS.MultiFra,
                                           CASE WHEN X.Activo = 0 THEN '' ELSE IFNULL(X.UrlSQS,'') END UrlSQS,
                                           SC.ConTransmisionInicial,
                                           IFNULL(SC.TicketsFaltantes,'') TicketsFaltantes,
                                           'HISTORICO' TipoCarga,
                                           X.UrlAPIs 
                                FROM spos_sqlscripts                    SS 
                                INNER JOIN spos_SqlScriptsPorEmpresa    SQ ON SS.IdSQLScript = SQ.IdSqlScript
                                INNER JOIN sucursal                     SC ON SQ.IdEmpresa = SC.idEmpresa 
                                INNER JOIN catempresa                   EMP ON SC.IdEmpresa = EMP.idEmpresa
                                INNER JOIN soltec2_orquestador_servidormysql_detalle X ON SQ.IdEmpresa = X.IdEmpresa 
                                INNER JOIN soltec2_Historicos sh ON SS.IdSQLScript = sh.IdSQLScript AND SC.claveSimi = sh.ClaveSimi AND sh.Activo = 1 AND sh.Estatus='PENDIENTE'
                                WHERE SS.Activo = 1 AND Tipo IN('DTS') AND SC.claveSimi='" + numeroSucursal + @"'

                                UNION 

                                SELECT     SS.IdSqlScript,
                                           SS.IsOnLine, 
                                           SS.SQLScript ,
                                           SS.Nombre,
                                           SS.Tipo,
                                           SS.Condicion,
                                           IFNULL(SS.ValorIncrementoDecremento,0) ValorIncrementoDecremento ,
                                           SS.EsAPI,
                                           SS.EsCatalogo,
                                           EsSp,
                                           sh.Desde Param1,
                                           sh.Hasta Param2,
                                           IFNULL(SS.Param3,'') Param3,
                                           IFNULL(SS.Param4,'') Param4,
                                           IFNULL(SS.Param5,'') Param5,
                                           IFNULL(SS.Param6,'') Param6,
                                           IFNULL(SS.Param7,'') Param7,
                                           IFNULL(SS.Param8,'') Param8,
                                           IFNULL(SS.Param9,'') Param9,
                                           IFNULL(SS.Param10,'') Param10,
                                           MultiplesTablas,
                                           TiempoTransmision,
                                           Carga_SQLServer_SQLite,
                                           ResetearTablaSQLite,
                                           SC.IdSucursal,
                                           X.HostName, 
                                           X.UserName, 
                                           X.Password, 
                                           X.DatabaseName,
                                           SS.MultiFra,
                                           CASE WHEN X.Activo = 0 THEN '' ELSE IFNULL(X.UrlSQS,'') END UrlSQS,
                                           SC.ConTransmisionInicial,
                                           IFNULL(SC.TicketsFaltantes,'') TicketsFaltantes,
                                           'ONDEMAND' TipoCarga,
                                           X.UrlAPIs
                                FROM spos_sqlscripts                    SS 
                                INNER JOIN spos_SqlScriptsPorEmpresa    SQ ON SS.IdSQLScript = SQ.IdSqlScript
                                INNER JOIN sucursal                     SC ON SQ.IdEmpresa = SC.idEmpresa 
                                INNER JOIN catempresa                   EMP ON SC.IdEmpresa = EMP.idEmpresa
                                INNER JOIN soltec2_orquestador_servidormysql_detalle X ON SQ.IdEmpresa = X.IdEmpresa 
                                INNER JOIN soltec2_TransmisionOnDemand sh ON SS.IdSQLScript = sh.IdSQLScript AND sh.Activo = 1
                                WHERE SS.Activo = 1 AND Tipo IN('OND') AND SC.claveSimi='" + numeroSucursal + "';";

            var lstSqlScripts = new List<SPOS_SQLScripts>();

            {
                try
                {
                    using var connection = new MySqlConnection(dbConnection);
                    await connection.OpenAsync();

                    lstSqlScripts = (await connection.QueryAsync<SPOS_SQLScripts>(queryScripts, commandType: CommandType.Text, commandTimeout: 2000)).ToList();

                    if (IMP)
                        Logger.Important($"Se encontraron {lstSqlScripts.Count} scripts, para la sucursal: {numeroSucursal}");

                    foreach (var q in lstSqlScripts)
                    {

                        var query = $"SELECT {q.Param1} Param1, " +
                                    $"{q.Param2} Param2," +
                                    $"{q.Param3} Param3, " +
                                    $"{q.Param4} Param4," +
                                    $"{q.Param5} Param5," +
                                    $"{q.Param6} Param6," +
                                    $"{q.Param7} Param7," +
                                    $"{q.Param8} Param8," +
                                    $"{q.Param9} Param9," +
                                    $"{q.Param10} Param10 " +
                            $" FROM spos_sqlscripts WHERE IdSqlScript={q.IdSqlScript}";
                        try
                        {
                            var lst = lstSqlScripts.Where(x => x.IdSqlScript == q.IdSqlScript).FirstOrDefault();
                            string _fechaInicial = string.Empty;
                            string _fechaFinal = string.Empty;

                            if (q.TipoCarga == "NORMAL")
                            {
                                var param = (await connection.QueryFirstOrDefaultAsync<ParametrosScripts>(query, commandType: CommandType.Text, commandTimeout: 600));
                                if (param != null)
                                {
                                    _fechaInicial = param.Param1;
                                    _fechaFinal = param.Param2;

                                    if (param.Param1 != null)
                                        //lst.SQLScript = q.SQLScript.Replace("Param1", "{" + param.Param1 + "}");
                                        if (param.Param2 != null)
                                            //lst.SQLScript = q.SQLScript.Replace("Param2", "{" + param.Param2+ "}");
                                            if (param.Param3 != null)
                                                lst.SQLScript = q.SQLScript.Replace("Param3", param.Param3);
                                    if (param.Param4 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param4", param.Param4);
                                    if (param.Param5 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param5", param.Param5);
                                    if (param.Param6 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param6", param.Param6);
                                    if (param.Param7 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param7", param.Param7);
                                    if (param.Param8 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param8", param.Param8);
                                    if (param.Param9 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param9", param.Param9);
                                    if (param.Param10 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param10", param.Param10);
                                }
                            }
                            else if (q.TipoCarga == "HISTORICO")
                            {
                                _fechaInicial = q.Param1;
                                _fechaFinal = q.Param2;
                            }
                            else if (q.TipoCarga == "ONDEMAND")
                            {
                                _fechaInicial = q.Param1;
                                _fechaFinal = q.Param2;
                            }

                            scripts.Add(new SPOS_SQLScripts()
                            {
                                Activo = lst.Activo,
                                Condicion = lst.Condicion,
                                Descripcion = lst.Descripcion,
                                EsAPI = lst.EsAPI,
                                EsCatalogo = lst.EsCatalogo,
                                EsSP = lst.EsSP,
                                IdSqlScript = lst.IdSqlScript,
                                Nombre = lst.Nombre,
                                Param1 = lst.Param1,
                                Param2 = lst.Param2,
                                Param3 = lst.Param3,
                                Param4 = lst.Param4,
                                Param5 = lst.Param5,
                                Param6 = lst.Param6,
                                Param7 = lst.Param7,
                                Param8 = lst.Param8,
                                Param9 = lst.Param9,
                                Param10 = lst.Param10,
                                SQLScript = lst.SQLScript,
                                Tipo = lst.Tipo,
                                ValorIncrementoDecremento = lst.ValorIncrementoDecremento,
                                MultiplesTablas = lst.MultiplesTablas,
                                TiempoTransmision = lst.TiempoTransmision,
                                Carga_SQLServer_SQLite = lst.Carga_SQLServer_SQLite,
                                ScriptTable = lst.ScriptTable,
                                ResetearTablaSQLite = lst.ResetearTablaSQLite,
                                IdSucursal = lst.IdSucursal,
                                MultiFra = lst.MultiFra,
                                HostName = lst.HostName,
                                DatabaseName = lst.DatabaseName,
                                Password = lst.Password,
                                UserName = lst.UserName,
                                UrlSQS = lst.UrlSQS,
                                fechaInicial = _fechaInicial,
                                fechaFinal = _fechaFinal,
                                ConTransmisionInicial = lst.ConTransmisionInicial,
                                TicketsFaltantes = lst.TicketsFaltantes,
                                TipoCarga = q.TipoCarga,
                                UrlAPIs = lst.UrlAPIs
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Mapeo de campos {query}");
                            throw new Exception(ex.Message);
                        }
                    }

                    await connection.CloseAsync();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al procesar su información para el método: ObtieneScriptsConCargaInicial. Error {ex.Message}");
                    throw;
                }
            }
            return scripts;
        }

        public async Task<List<SPOS_SQLScripts>> ObtieneScripts_SIMIPET(string numeroSucursal)
        {
            var scripts = new List<SPOS_SQLScripts>();

            if (IMP)
                Logger.Important($"ObtieneScripts_SIMIPET - Obteniendo Scripts para la sucursal {numeroSucursal}");

            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbSimiPET").Value;
            var queryScripts = @"SELECT     SS.IdSqlScript,
			                                SS.IsOnLine, 
                                            SS.SQLScript ,
                                            SS.Nombre,
                                            SS.Tipo,
                                            SS.Condicion,
                                            IFNULL(SS.ValorIncrementoDecremento,0) ValorIncrementoDecremento ,
                                            SS.EsAPI,
                                            SS.EsCatalogo,
                                            EsSp,
                                            SS.Param1 Param1,
                                            SS.Param2 Param2,
                                            IFNULL(C.Param3,IFNULL(SS.Param3,'')) Param3,
                                            IFNULL(C.Param4,IFNULL(SS.Param4,'')) Param4,
                                            IFNULL(C.Param5,IFNULL(SS.Param5,'')) Param5,
                                            IFNULL(C.Param6,IFNULL(SS.Param6,'')) Param6,
                                            IFNULL(C.Param7,IFNULL(SS.Param7,'')) Param7,
                                            IFNULL(C.Param8,IFNULL(SS.Param8,'')) Param8,
                                            IFNULL(C.Param9,IFNULL(SS.Param9,'')) Param9,
                                            IFNULL(C.Param10,IFNULL(SS.Param10,'')) Param10,
                                            MultiplesTablas,
                                            TiempoTransmision,
                                            Carga_SQLServer_SQLite,
                                            ResetearTablaSQLite,
                                            SC.IdSucursal,
                                            X.HostName, 
                                            X.UserName, 
                                            X.Password, 
                                            X.DatabaseName,
                                            SS.MultiFra,
                                            CASE WHEN X.Activo = 0 THEN '' ELSE IFNULL(X.UrlSQS,'') END UrlSQS,
                                            SC.ConTransmisionInicial,
                                            IFNULL(SC.TicketsFaltantes,'') TicketsFaltantes,
                                            'NORMAL' TipoCarga,
                                            X.UrlAPIs
                                FROM spos_sqlscripts                    SS 
                                INNER JOIN spos_SqlScriptsPorEmpresa    SQ ON SS.IdSQLScript = SQ.IdSqlScript
                                INNER JOIN sucursal                     SC ON SQ.IdEmpresa = SC.idEmpresa 
                                INNER JOIN catempresa                   EMP ON SC.IdEmpresa = EMP.idEmpresa
                                LEFT JOIN soltec2_orquestador_servidormysql_detalle X ON SQ.IdEmpresa = X.IdEmpresa 
                                LEFT JOIN spos_sqlscriptsdetalle C ON SS.IdSQLScript = C.IdSQLScript AND SC.claveSimi = C.NumeroSucursal AND C.Activo = 1 AND C.EsHistorico = 0
                                 WHERE SS.Activo = 1 AND Tipo IN('DTS','SVL','SQS|JSON','SQS|PLANO') AND SC.claveSimi='" + numeroSucursal + @"'

                                UNION

                                SELECT     SS.IdSqlScript,
                                           SS.IsOnLine, 
                                           SS.SQLScript ,
                                           SS.Nombre,
                                           SS.Tipo,
                                           SS.Condicion,
                                           IFNULL(SS.ValorIncrementoDecremento,0) ValorIncrementoDecremento ,
                                           SS.EsAPI,
                                           SS.EsCatalogo,
                                           EsSp,
                                           sh.Desde Param1,
                                           sh.Hasta Param2,
                                           IFNULL(SS.Param3,'') Param3,
                                           IFNULL(SS.Param4,'') Param4,
                                           IFNULL(SS.Param5,'') Param5,
                                           IFNULL(SS.Param6,'') Param6,
                                           IFNULL(SS.Param7,'') Param7,
                                           IFNULL(SS.Param8,'') Param8,
                                           IFNULL(SS.Param9,'') Param9,
                                           IFNULL(SS.Param10,'') Param10,
                                           MultiplesTablas,
                                           TiempoTransmision,
                                           Carga_SQLServer_SQLite,
                                           ResetearTablaSQLite,
                                           SC.IdSucursal,
                                           X.HostName, 
                                           X.UserName, 
                                           X.Password, 
                                           X.DatabaseName,
                                           SS.MultiFra,
                                           CASE WHEN X.Activo = 0 THEN '' ELSE IFNULL(X.UrlSQS,'') END UrlSQS,
                                           SC.ConTransmisionInicial,
                                           IFNULL(SC.TicketsFaltantes,'') TicketsFaltantes,
                                           'HISTORICO' TipoCarga,
                                           X.UrlAPIs 
                                FROM spos_sqlscripts                    SS 
                                INNER JOIN spos_SqlScriptsPorEmpresa    SQ ON SS.IdSQLScript = SQ.IdSqlScript
                                INNER JOIN sucursal                     SC ON SQ.IdEmpresa = SC.idEmpresa 
                                INNER JOIN catempresa                   EMP ON SC.IdEmpresa = EMP.idEmpresa
                                INNER JOIN soltec2_orquestador_servidormysql_detalle X ON SQ.IdEmpresa = X.IdEmpresa 
                                INNER JOIN soltec2_Historicos sh ON SS.IdSQLScript = sh.IdSQLScript AND SC.claveSimi = sh.ClaveSimi AND sh.Activo = 1 AND sh.Estatus='PENDIENTE'
                                WHERE SS.Activo = 1 AND Tipo IN('DTS') AND SC.claveSimi='" + numeroSucursal + "';";
            var lstSqlScripts = new List<SPOS_SQLScripts>();

            {
                try
                {
                    using var connection = new MySqlConnection(dbConnection);
                    await connection.OpenAsync();

                    lstSqlScripts = (await connection.QueryAsync<SPOS_SQLScripts>(queryScripts, commandType: CommandType.Text, commandTimeout: 2000)).ToList();

                    if (IMP)
                        Logger.Important($"Se encontraron {lstSqlScripts.Count} scripts, para la sucursal: {numeroSucursal}");

                    foreach (var q in lstSqlScripts)
                    {
                        var query = $"SELECT {q.Param1} Param1, " +
                                    $"{q.Param2} Param2," +
                                    $"{q.Param3} Param3, " +
                                    $"{q.Param4} Param4," +
                                    $"{q.Param5} Param5," +
                                    $"{q.Param6} Param6," +
                                    $"{q.Param7} Param7," +
                                    $"{q.Param8} Param8," +
                                    $"{q.Param9} Param9," +
                                    $"{q.Param10} Param10 " +
                            $" FROM spos_sqlscripts WHERE IdSqlScript={q.IdSqlScript}";
                        try
                        {
                            var lst = lstSqlScripts.Where(x => x.IdSqlScript == q.IdSqlScript).FirstOrDefault();
                            string _fechaInicial = string.Empty;
                            string _fechaFinal = string.Empty;

                            if (q.TipoCarga == "NORMAL")
                            {
                                var param = (await connection.QueryFirstOrDefaultAsync<ParametrosScripts>(query, commandType: CommandType.Text, commandTimeout: 600));
                                if (param != null)
                                {
                                    _fechaInicial = param.Param1;
                                    _fechaFinal = param.Param2;

                                    if (param.Param1 != null)
                                        if (param.Param2 != null)
                                            if (param.Param3 != null)
                                                lst.SQLScript = q.SQLScript.Replace("Param3", param.Param3);
                                    if (param.Param4 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param4", param.Param4);
                                    if (param.Param5 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param5", param.Param5);
                                    if (param.Param6 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param6", param.Param6);
                                    if (param.Param7 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param7", param.Param7);
                                    if (param.Param8 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param8", param.Param8);
                                    if (param.Param9 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param9", param.Param9);
                                    if (param.Param10 != null)
                                        lst.SQLScript = q.SQLScript.Replace("Param10", param.Param10);
                                }
                            }
                            else if (q.TipoCarga == "HISTORICO")
                            {
                                _fechaInicial = q.Param1;
                                _fechaFinal = q.Param2;
                            }
                            scripts.Add(new SPOS_SQLScripts()
                            {
                                Activo = lst.Activo,
                                Condicion = lst.Condicion,
                                Descripcion = lst.Descripcion,
                                EsAPI = lst.EsAPI,
                                EsCatalogo = lst.EsCatalogo,
                                EsSP = lst.EsSP,
                                IdSqlScript = lst.IdSqlScript,
                                Nombre = lst.Nombre,
                                Param1 = lst.Param1,
                                Param2 = lst.Param2,
                                Param3 = lst.Param3,
                                Param4 = lst.Param4,
                                Param5 = lst.Param5,
                                Param6 = lst.Param6,
                                Param7 = lst.Param7,
                                Param8 = lst.Param8,
                                Param9 = lst.Param9,
                                Param10 = lst.Param10,
                                SQLScript = lst.SQLScript,
                                Tipo = lst.Tipo,
                                ValorIncrementoDecremento = lst.ValorIncrementoDecremento,
                                MultiplesTablas = lst.MultiplesTablas,
                                TiempoTransmision = lst.TiempoTransmision,
                                Carga_SQLServer_SQLite = lst.Carga_SQLServer_SQLite,
                                ScriptTable = lst.ScriptTable,
                                ResetearTablaSQLite = lst.ResetearTablaSQLite,
                                IdSucursal = lst.IdSucursal,
                                MultiFra = lst.MultiFra,
                                HostName = lst.HostName,
                                DatabaseName = lst.DatabaseName,
                                Password = lst.Password,
                                UserName = lst.UserName,
                                UrlSQS = lst.UrlSQS,
                                fechaInicial = _fechaInicial,
                                fechaFinal = _fechaFinal,
                                ConTransmisionInicial = lst.ConTransmisionInicial,
                                TicketsFaltantes = lst.TicketsFaltantes,
                                TipoCarga = q.TipoCarga,
                                UrlAPIs = lst.UrlAPIs
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Mapeo de campos {query}");
                            throw new Exception(ex.Message);
                        }
                    }

                    await connection.CloseAsync();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al procesar su información para el método: ObtieneScripts_SIMIPET. Error {ex.Message}");
                    throw;
                }
            }
            return scripts;
        }

        public static DateTime GetJsonDateTime(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement value))
            {
                string raw = value.GetString();

                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (DateTime.TryParse(raw, new CultureInfo("es-MX"), DateTimeStyles.None, out DateTime fecha))
                    {
                        return fecha;
                    }
                }
            }
            return DateTime.Now;
        }

        /// <summary>
        /// Este no se donde se consumen.
        /// </summary>
        /// <param name="rfcEmpresa"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<VentasEnLineaExcel>> GetReporteExcelDataDAL(string rfcEmpresa)
        {
            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaReal").Value;

            var ventasEnLineaDatos = new List<VentasEnLineaExcel>();

            try
            {
                //using (var connection = new MySqlConnection(dbConnection))
                //{
                //    await connection.OpenAsync();

                //    var parameters = new DynamicParameters();
                //    parameters.Add("@rfcEmpresa", rfcEmpresa, DbType.String);

                //    ventasEnLineaDatos = connection.Query<VentasEnLineaExcel>("sp_EnLinea_GetVentasEnLineaExcel", parameters, commandType: CommandType.StoredProcedure, commandTimeout: 600).ToList();
                //    await connection.CloseAsync();
                //}

                //return ventasEnLineaDatos;
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error - GetReporteExcelDataDAL\n{ex.Message}");
                throw new Exception(ex.Message);
            }
        }

        public async Task<ParametrosGenerales> GetParametrosDAL(string claveSimi)
        {
            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaReal").Value;
            var getParametros = new ParametrosGenerales();
            try
            {
                //using (var connection = new MySqlConnection(dbConnection))
                //{
                //    await connection.OpenAsync();

                //    var parameters = new DynamicParameters();
                //    parameters.Add("@claveSimi", claveSimi, DbType.String);

                //    getParametros = connection.QueryFirst<ParametrosGenerales>("sp_EnLinea_GetParametros", parameters, commandType: CommandType.StoredProcedure, commandTimeout: 600);
                //    await connection.CloseAsync();
                //}

                //return getParametros;
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en: GetParametrosDAL.\n{ex.Message}");
                throw new Exception(ex.Message);
            }
        }

    }
}
