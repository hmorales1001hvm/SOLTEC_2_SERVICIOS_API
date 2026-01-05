using Soltec.Common.Logger;
using Soltec.Orquestacion.App;
using System;
using Soltec.Orquestacion.BR;
using Google.Protobuf.WellKnownTypes;
using System.Reflection;

namespace Soltec.Orquestacion.App
{
    public class Program
    {
		public static void Main(string[] args)
        {
            try
            {
                var processNumber = 0;
                var idEmpresa = 0;
                string logName = string.Empty;
				var valorInicial = 0;
				var valorFinal = 0;
				var peticion = 0;
				var arrayEmpresas = string.Empty;
				if (args.Length > 0)
				{
					processNumber = int.Parse(args[0]);
					logName = $"\\Proceso-{processNumber}\\";

					if (processNumber == 1)
					{
						idEmpresa = int.Parse(args[1]);
						valorInicial = int.Parse(args[2]);
						valorFinal = int.Parse(args[3]);
						logName = $"\\Proceso-{processNumber}_{idEmpresa}_{valorInicial}_{valorFinal}\\";
					}
					else if (processNumber == 4)
					{
						peticion = int.Parse(args[1]);
					}
					else if (processNumber == 7)
					{
						arrayEmpresas = args[1];
					}
					else if (processNumber == 11)
					{
						idEmpresa = int.Parse(args[1]);
						logName = $"\\Proceso-{processNumber}_{idEmpresa}\\";
					}
				}

				Console.Write($"No. de proceso: {processNumber}\n");
			

				switch (args[0])
                {
                    case "0":
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Iniciando replicación de BD ==========");
						OrquestacionDB();
						Logger.Important("========== Finaliza replicación de BD ==========");
						break;
                   
                    case "1":
						Console.Write($"Id Empresa: {idEmpresa}\n");
						Console.Write($"Valor inicial paginado: {valorInicial}\n");
						Console.Write($"Valor final paginado: {valorFinal} \n");

						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
                        Logger.Important("========== Iniciando orquestación ==========");
                        LoadFiles(idEmpresa, valorInicial, valorFinal);
						Logger.Important("========== Finaliza orquestación ==========");
						break;

					case "2":
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Iniciando orquestación de catálogos ==========");
						LoadFilesCatalogs();
						Logger.Important("========== Finaliza  orquestación de catálogos ==========");
						break;

					case "3": // Ejecuta concentrado del catálogo de productos y precios.
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Iniciando actualización y concentrado de catálogos ==========");
						LoadUpdateCatalogSQLServerAsync();
						Logger.Important("========== Finaliza actualización y concentrado de catálogos ==========");
						break;
					case "4": // API - Recibe Ticket .
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Iniciando proceso para API - Recibe Ticket ==========");
						RecibeTicket(Settings1.Default.PathFileJSON, peticion, Settings1.Default.ApiRecibeTicket);
						Logger.Important("========== Finaliza proceso para API - Recibe Ticket ==========");
						break;
					case "5":
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Iniciando Backup de Tickets ==========");
						BackupTicketsAsync();
						Logger.Important("========== Finaliza Backup de Tickets ==========");
						break;
					case "6":
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Iniciando almacenamiento de facturas en CONTABO - BUCKET ==========");
						BackupBucketContaboAsync();
						Logger.Important("========== Finaliza almacenamiento de facturas en CONTABO - BUCKET ==========");
						break;

					case "7":
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Iniciando proceso carga de ventas depositos ==========");
						ProcesaDatosVentaDepositos(arrayEmpresas);
						Logger.Important("========== Finaliza proceso carga de ventas depositos ==========");
						break;
					case "8":
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Inicia lectura de conceptos de XMLs ==========");
						ProcesaFacturasConceptosXML();
						Logger.Important("========== Finaliza lectura de conceptos de XMLs ==========");
						break;
					case "9":
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Inicia carga de Kushki ==========");
						ProcesaKushki();
						Logger.Important("========== Finaliza carga de Kushki ==========");
						break;
					case "10":
						FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						Logger.Important("========== Inicia monitoreo de archivos ==========");
						MonitoreoArchivos();
						Logger.Important("========== Finaliza monitoreo de archivos ==========");
						break;
                    case "11":
                        FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
						var version = Assembly.GetExecutingAssembly().GetName().Version;
                        Logger.Important($"========== Inicia carga de SQS - AWS - Versión [{version}] ==========");
                        ProcesaSQS(idEmpresa);
                        Logger.Important("========== Termina carga de SQS - AWS ==========");
                        break;
                    //case "12":
                    //    FileUtil.localLogPath = $"{Settings1.Default.LogPath}{logName}";
                    //    var version2 = Assembly.GetExecutingAssembly().GetName().Version;
                    //    Logger.Important($"========== Inicia Cron Carga de Históricos - Versión [{version2}] ==========");
                    //    ProcesaHistoricos();
                    //    Logger.Important("========== Termina Cron Carga de Históricos ==========");
                    //    break;

                }
                    
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

		async static void OrquestacionDB()
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.OrquestacionDB();
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}

		async static void LoadFiles(int idEmpresa, int valorInicial, int valorFinal)
        {
            try
            {
                var result = await Soltec.Orquestacion.BR.Orchestration.ProcessFiles(Settings1.Default.PathSourceFile + "Operativas", idEmpresa, valorInicial, valorFinal);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

		async static void LoadFilesCatalogs()
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.ProcessFilesCatalogs(Settings1.Default.PathSourceFile + "Catalogos");
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}


		async static void LoadUpdateCatalogSQLServerAsync()
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.LoadUpdateCatalogSQLServerAsync();
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}

		async static void RecibeTicket(string pathJSON, int peticion, string apiRecibeTicket)
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.RecibeTicket(pathJSON, peticion, apiRecibeTicket);
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}

		
		async static void BackupTicketsAsync()
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.BackupTicketsAsync();
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}

		async static void BackupBucketContaboAsync()
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.BackupBucketContaboAsync(Settings1.Default.AccessKey, 
																								 Settings1.Default.SecretKey, 
																								 Settings1.Default.BucketName, 
																								 Settings1.Default.ServiceBucketURL);
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}

		async static void ProcesaDatosVentaDepositos(string arrayEmpresas)
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.ProcesaDatosVentaDepositos(arrayEmpresas);
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}


		async static void ProcesaFacturasConceptosXML()
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.ProcesaFacturasConceptosXML(Settings1.Default.RutaFacturasXML);
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}


		async static void ProcesaKushki()
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.ProcesaKushki(Settings1.Default.KushkiHost, Settings1.Default.KushkiUserName, Settings1.Default.KushkiPathFilePEM, Settings1.Default.KushkiRemoteFilePath,Settings1.Default.KushkiPathDownloadFile);
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}


		async static void MonitoreoArchivos()
		{
			try
			{
				var result = await Soltec.Orquestacion.BR.Orchestration.MonitoreoArchivos(Settings1.Default.apiURL);
			}
			catch (Exception ex)
			{
				Logger.Error(ex);
			}
		}

        async static void ProcesaSQS(int idEmpresa)
        {
            try
            {
                var result = await Soltec.Orquestacion.BR.Orchestration.ProcesaSQS(Settings1.Default.AccessKeySQS,
                                                                                   Settings1.Default.SecretKeySQS,
                                                                                   Settings1.Default.RegionSQS,
																				   idEmpresa);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        //async static void ProcesaHistoricos()
        //{
        //    try
        //    {
        //        var result = await Soltec.Orquestacion.BR.Orchestration.ProcesaHistoricos();
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error(ex);
        //    }
        //}
        

    }
}
