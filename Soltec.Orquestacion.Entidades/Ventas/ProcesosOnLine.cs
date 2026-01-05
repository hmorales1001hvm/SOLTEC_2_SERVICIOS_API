using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soltec.Entities.Ventas
{
	public class ProcesosOnLine
	{
			public string? Sucursal { get; set; }
			public string? Json { get; set; }
			public string? NombreProceso { get; set; }
			public int IdSucursal { get; set; }
            public string? Ver1 { get; set; }
            public string? Ver2 { get; set; }
            public string? Ver3 { get; set; }
            public string? Ver4 { get; set; }
            public string? Ver5 { get; set; }
            public string Ver6 { get; set; }
            public bool ConDatos { get; set; }
            public int MultiFra { get; set; } = 0;
            public string? HostName { get; set; } = string.Empty;
            public string? DatabaseName { get; set; } = string.Empty;
            public string? UserName { get; set; } = string.Empty;
            public string? Password { get; set; } = string.Empty;
            public bool ConTransmisionInicial { get; set; }
            public string? TicketsFaltantes { get; set; }
            public string TipoCarga { get; set; } = "";
    }
}
