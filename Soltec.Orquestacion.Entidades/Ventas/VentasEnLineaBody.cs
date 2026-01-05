using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiCommon.Entities.Ventas
{
    public class VentasEnLineaBody
    {
        public int IdVentaEnLinea { get; set; }

        public string Sucursal { get; set; }

        public DateTime Fecha { get; set; }

        public decimal Venta { get; set; }
    }
}
