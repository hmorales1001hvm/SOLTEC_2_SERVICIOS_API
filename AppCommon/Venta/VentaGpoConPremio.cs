using System;

namespace AppCommon.Venta
{
    public class VentaGpoConPremio
    {
        public DateTime Fecha { get; set; }

        public int IdVendedor { get; set; }

        public string Nombre { get; set; }

        public decimal ImporteVenta { get; set; }

        public int Transaccionesventa { get; set; }

        public decimal PorcVenta { get; set; }

        public decimal ImporteNaturistas { get; set; }

        public decimal PorcNaturistas { get; set; }

        public decimal ImporteNocturno { get; set; }

        public decimal MontoDescuento { get; set; }

        public decimal Menudeos { get; set; }

        public decimal MontoIva { get; set; }
    }
}
