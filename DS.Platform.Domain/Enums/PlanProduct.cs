namespace DS.Platform.Domain.Enums
{
    /// <summary>Producto que vende un plan de la consola.</summary>
    public enum PlanProduct
    {
        /// <summary>ERP en SaaS: una instancia por cliente (límites de empresas, usuarios, sucursales e IA).</summary>
        Erp = 1,

        /// <summary>API SUNAT: facturación electrónica (límites de empresas emisoras y comprobantes por mes).</summary>
        ApiSunat = 2
    }
}
