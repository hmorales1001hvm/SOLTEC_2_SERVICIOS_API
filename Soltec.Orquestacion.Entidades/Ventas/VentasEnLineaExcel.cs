namespace Soltec.Entities.Ventas
{
    public class VentasEnLineaExcel
    {
        public string Sucursal { get; set; }

        public string Nombre { get; set; }

        public DateTime Fecha { get; set; }

        public decimal Venta { get; set; }

        public DateTime FechaRegistro { get; set; }
    }
}
