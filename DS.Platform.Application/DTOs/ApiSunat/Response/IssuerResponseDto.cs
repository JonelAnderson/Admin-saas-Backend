namespace DS.Platform.Application.DTOs.ApiSunat.Response
{
    /// <summary>Empresa emisora de la API SUNAT vinculada a un cliente.</summary>
    public class IssuerResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerRuc { get; set; } = null!;
        public string CustomerName { get; set; } = null!;
        public long TenantId { get; set; }
        public string Ruc { get; set; } = null!;
        public string BusinessName { get; set; } = null!;
        public string? TradeName { get; set; }
        public string? Environment { get; set; }
        public string? ApiPlan { get; set; }
        public int? DocumentsThisMonth { get; set; }
        public int? MaxDocumentsMonth { get; set; }
        /// <summary>La consola quiere la emisión suspendida (el cliente no tiene suscripción vigente).</summary>
        public bool SuspendDesired { get; set; }
        /// <summary>La API SUNAT ya aplicó la suspensión.</summary>
        public bool SuspendApplied { get; set; }
        /// <summary>Hay un cambio de estado en camino hacia la API.</summary>
        public bool SyncPending { get; set; }
        /// <summary>Plan de la API que la consola quiere para la empresa (null = no se administra).</summary>
        public string? DesiredPlan { get; set; }
        /// <summary>Hay un cambio de plan en camino hacia la API.</summary>
        public bool PlanPending { get; set; }
        public DateTime? LastSyncedOnUtc { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Empresa de la API SUNAT para elegirla al vincularla a un cliente.</summary>
    public class ApiTenantResponseDto
    {
        public long Id { get; set; }
        public string Ruc { get; set; } = null!;
        public string BusinessName { get; set; } = null!;
        public string? TradeName { get; set; }
        public string? Environment { get; set; }
        public string? Plan { get; set; }
        public bool IsActive { get; set; }
        public bool BillingSuspended { get; set; }
        public int DocumentsThisMonth { get; set; }
        public int MaxDocumentsMonth { get; set; }
        /// <summary>Ya está vinculada a un cliente de la consola.</summary>
        public bool Linked { get; set; }
    }
}
