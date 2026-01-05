using System;

namespace AppCommon.Venta
{
    public class VentasEnLineaBody
    {
        public int IdVentaEnLinea { get; set; }

        public string Sucursal { get; set; }

        public DateTime Fecha { get; set; }

        public decimal Venta { get; set; }
    }
}
