using System;

namespace AppCommon.Entities
{
    public class GetVentaSucursal
    {
        public string Sucursal { get; set; }

        public DateTime fechaOperacion { get; set; }

        public decimal Total { get; set; }
    }
}
