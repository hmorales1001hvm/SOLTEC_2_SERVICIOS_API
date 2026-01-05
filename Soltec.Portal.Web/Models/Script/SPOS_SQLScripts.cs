using System.Runtime.Intrinsics.X86;
using System;

namespace Soltec.Portal.Web.Models.Script
{
	public class SPOS_SQLScripts
	{
        public int IdSqlScript { get; set; }
        public string SQLScript { get; set; }
		public string Nombre { get; set; }
        public string Tipo { get; set; }
        public string Condicion { get; set; }
        public bool EsAPI { get; set; }
		public bool Activo { get; set; }
		public string Estado { get; set; } = "NO";
        public string Descripcion { get; set; }
        public bool MultiplesTablas { get; set; }
        public int TiempoTransmision { get; set; }

    }
}
