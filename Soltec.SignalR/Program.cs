using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using System.IO;
using System.Threading;
using Soltec.SignalR.Properties;

namespace Soltec.SignalR
{
    internal class Program
    {
    
        private IDisposable SignalR { get; set; }
        static void Main(string[] args)
        {
            Program program = new Program();
            //Process servicio = new Process();
            try
            {
                program.InitializeClient();
                Console.ReadKey();

             

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
            }
          
        }

        private void StartServer()
        {
            try
            {
                SignalR = WebApp.Start(Settings.Default.LocalServerUrl);

                Console.WriteLine("Se inicio el servicio en: {0}", Settings.Default.LocalServerUrl);

            }
            catch (Exception e)
            {
                Console.WriteLine("El servicio web no pudo ser iniciado en {0}, posiblemente ya se encuentra otro en ejecución ER: {1}", Settings.Default.LocalServerUrl, e.Message);

                return;
            }
        }

        /// <summary>
        /// Inicializa el servicio de SignalR
        /// </summary>
        public void InitializeClient()
        {
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("SOLTEC");
            Console.WriteLine("=========");
            Console.WriteLine($"Cliente SignalR version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()}");
            Console.WriteLine();
            Console.WriteLine("--------------------------------------------------------------------------------------");
            Console.WriteLine();

            Task.Run(() => StartServer());

        }
    }
}
