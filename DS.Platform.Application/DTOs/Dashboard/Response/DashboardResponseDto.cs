namespace DS.Platform.Application.DTOs.Dashboard.Response
{
    /// <summary>Tablero inicial de la consola (diseño §7): todo lo que pide atención en un vistazo.</summary>
    public class DashboardResponseDto
    {
        /// <summary>Hoy en Lima (yyyy-MM-dd).</summary>
        public string Today { get; set; } = null!;
        public int Customers { get; set; }
        public List<DashboardCountDto> SubscriptionsByStatus { get; set; } = [];
        /// <summary>Cobrado en el mes de Lima (pagos registrados, sin anulados), por moneda.</summary>
        public List<DashboardRevenueDto> MonthRevenue { get; set; } = [];
        /// <summary>Vencen o se suspenden en los próximos 7 días, las más cercanas primero.</summary>
        public List<DashboardDueDto> DueThisWeek { get; set; } = [];
        public int InstancesTotal { get; set; }
        /// <summary>Instancias sin contacto desde hace más de un día (o que nunca se conectaron).</summary>
        public List<DashboardInstanceDto> InstancesWithoutContact { get; set; } = [];
        /// <summary>La versión más alta que informa alguna instancia; las que tienen otra están desactualizadas.</summary>
        public string? LatestVersion { get; set; }
        public List<DashboardInstanceDto> InstancesOutdated { get; set; } = [];
        public int RemoteActionsNeedingAttention { get; set; }
        public int NotificationsNeedingAttention { get; set; }
        /// <summary>Resumen de la API SUNAT; null si no se pudo consultar (la consola no falla por eso).</summary>
        public DashboardApiDto? Api { get; set; }
    }

    public class DashboardCountDto
    {
        public int Status { get; set; }
        public string Name { get; set; } = null!;
        public int Count { get; set; }
    }

    public class DashboardRevenueDto
    {
        public string Currency { get; set; } = null!;
        public decimal Amount { get; set; }
        public int Payments { get; set; }
    }

    public class DashboardDueDto
    {
        public int SubscriptionId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public string PlanName { get; set; } = null!;
        /// <summary>Qué pasa ese día: "Vence" o "Se suspende".</summary>
        public string Event { get; set; } = null!;
        /// <summary>yyyy-MM-dd (Lima).</summary>
        public string Date { get; set; } = null!;
        public int DaysLeft { get; set; }
    }

    public class DashboardInstanceDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Version { get; set; }
        public DateTime? LastContactOnUtc { get; set; }
        public string ContactName { get; set; } = null!;
    }

    public class DashboardApiDto
    {
        public bool Available { get; set; }
        public string? Error { get; set; }
        public long TenantsActive { get; set; }
        public long DocumentsThisMonth { get; set; }
        public long RejectedThisMonth { get; set; }
        /// <summary>Rechazados u observados por SUNAT hoy; null si esa consulta falló.</summary>
        public long? RejectedToday { get; set; }
        public long PendingNow { get; set; }
        public long FailedJobs { get; set; }
        public long CertificatesExpiring { get; set; }
        public long CertificatesBroken { get; set; }
    }
}
