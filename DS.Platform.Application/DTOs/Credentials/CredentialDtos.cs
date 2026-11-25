using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.DTOs.Credentials.Request
{
    /// <summary>Pide entregar al ERP las credenciales de la API SUNAT de un emisor (regenerarlas invalida las actuales).</summary>
    public class CreateApiCredentialDeliveryRequestDto
    {
        /// <summary>Instancia del ERP que las recibirá (la que las recoge con su clave de licencia).</summary>
        public required int InstanceId { get; set; }
        /// <summary><c>ApiSunatIssuer</c> cuyas credenciales se entregan; debe ser del mismo cliente que la instancia.</summary>
        public required int IssuerId { get; set; }
        /// <summary>Confirmación explícita de que las credenciales actuales del emisor dejan de funcionar.</summary>
        public bool ConfirmInvalidatePrevious { get; set; }
    }

    /// <summary>Consulta de la instancia del ERP (<c>POST api/v1/License/ApiSunat/Pull</c>, clave en <c>X-License-Key</c>).</summary>
    public class ApiSunatPullRequestDto
    {
        /// <summary>Valor al azar de la instancia (16 a 64 caracteres), como en la consulta de licencia.</summary>
        public string? Nonce { get; set; }
    }

    /// <summary>Confirmación de la instancia del ERP (<c>POST api/v1/License/ApiSunat/Ack</c>): las entregas que ya guardó.</summary>
    public class ApiSunatAckRequestDto
    {
        public List<int>? Ids { get; set; }
    }
}

namespace DS.Platform.Application.DTOs.Credentials.Response
{
    /// <summary>Fila de la pantalla "Credenciales al ERP". Nunca trae la API key ni el secret.</summary>
    public class ApiCredentialDeliveryResponseDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = null!;
        public int InstanceId { get; set; }
        public string InstanceName { get; set; } = null!;
        public int IssuerId { get; set; }
        public string IssuerRuc { get; set; } = null!;
        public string IssuerName { get; set; } = null!;
        public ApiCredentialDeliveryStatus Status { get; set; }
        public string StatusName { get; set; } = null!;
        public int Attempts { get; set; }
        public DateTime NextAttemptOnUtc { get; set; }
        public string? LastError { get; set; }
        public DateTime? ReadyOnUtc { get; set; }
        /// <summary>Hasta cuándo espera la consola al ERP (solo Lista).</summary>
        public DateTime? ExpiresOnUtc { get; set; }
        public DateTime? LastPulledOnUtc { get; set; }
        public DateTime? DeliveredOnUtc { get; set; }
        /// <summary>Lista pero el ERP aún no la recogió (ni la vio): "sin recoger".</summary>
        public bool WaitingForErp { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    /// <summary>Una entrega para el ERP: el RUC de su empresa y las credenciales en claro (viajan una sola vez, hasta su confirmación).</summary>
    public class ApiSunatDeliveryDto
    {
        public int Id { get; set; }
        public string Ruc { get; set; } = null!;
        public string ApiKey { get; set; } = null!;
        public string ApiSecret { get; set; } = null!;

        // Un log o un mensaje de excepción que muestre el objeto no debe mostrar el secreto.
        public override string ToString() => $"ApiSunatDeliveryDto(Id={Id}, Ruc={Ruc}, ***)";
    }

    public class ApiSunatPullResponseDto
    {
        /// <summary>Dirección pública (https) de la API SUNAT; null si la consola no la tiene configurada (y entonces no hay entregas).</summary>
        public string? BaseUrl { get; set; }
        public List<ApiSunatDeliveryDto> Deliveries { get; set; } = new();

        public override string ToString() => $"ApiSunatPullResponseDto(Deliveries={Deliveries.Count})";
    }

    public class ApiSunatAckResponseDto
    {
        public int Acknowledged { get; set; }
    }
}
