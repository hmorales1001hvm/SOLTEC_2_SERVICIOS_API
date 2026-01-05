using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Soltec.ApiCommon.Entities.Ventas
{
    public class VentaCompleta
    {
            public string ClaveSimi { get; set; }
            public string FechaOperacion { get; set; }
            public string NombreSucursal { get; set; }
            public int? Id_Venta { get; set; }
            public string id_usuario_venta { get; set; }
            public string Empleado { get; set; }
            public string Codigo { get; set; }
            public string Producto { get; set; }
            public string? Presentacion { get; set; }
            public string Nivel1 { get; set; }
            public string Nivel2 { get; set; }
            public string Nivel3 { get; set; }
            public int Premio { get; set; }
            public int? Combo { get; set; }
            public int? Inventario { get; set; }
            public string Id_ProductoSAT { get; set; }
            public int? NoPonderado { get; set; }
            public string CategoriaComercial { get; set; }
            public decimal Cantidad { get; set; }
            public decimal Precio { get; set; }
            public decimal IVA { get; set; }
            public decimal Descuento { get; set; }
            public decimal DescuentoPorciento { get; set; }
            public decimal IVA_Porciento { get; set; }
            public decimal IVA_Importe { get; set; }
            public int? idRegistradora { get; set; }
            public int? idRegistradoraVenta { get; set; }
            public int? idRegistradoraCobro { get; set; }
            public int? TipoOperacion { get; set; }
            public int? Enviado { get; set; }
    }
}
