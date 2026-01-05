namespace Soltec.Entities.Ventas
{
    public class VentasEnLinea
    {
        public string Sucursal { get; set; }

        public string Nombre { get; set; }

        public DateTime Fecha { get; set; }

        public decimal Venta { get; set; }

        public DateTime FechaRegistro { get; set; }
        public string Empresa { get; set; }

    }
}
