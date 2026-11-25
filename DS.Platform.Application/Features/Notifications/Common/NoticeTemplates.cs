using System.Globalization;
using System.Text;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Application.Features.Notifications.Common
{
    public sealed record NoticeContext(
        string CustomerName, string ProductName, string PlanName, DateOnly CoveredUntil, DateOnly? SuspendOn, string Currency, decimal Price);

    /// <summary>Datos de la constancia de un pago registrado (van en el cuerpo del correo).</summary>
    public sealed record PaymentReceipt(
        string Reference, string CustomerName, string CustomerRuc, string ProductName, string PlanName, DateOnly PaidOn, decimal Amount,
        string Currency, string MethodName, string? OperationNumber, int Periods, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly CoveredUntil);

    /// <summary>Invitación al portal: el enlace lleva el token de activación (secreto); <c>ValidHours</c> es lo que le queda de vida.</summary>
    public sealed record PortalInvitationNotice(string Name, string CustomerName, string ActivationUrl, int ValidHours, IReadOnlyList<string> Issuers);

    public sealed record CertificateNotice(string CustomerName, string IssuerRuc, string IssuerName, DateOnly? ExpiresOn, long DaysLeft);

    /// <summary>
    /// Asunto y cuerpo HTML de cada aviso. Pura: todo texto de usuario va codificado para HTML. Los montos y fechas salen en el formato
    /// del Perú (dd/MM/yyyy, dos decimales).
    /// </summary>
    public static class NoticeTemplates
    {
        public const string Team = "Equipo de cobranzas";

        public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        public static string Money(string currency, decimal amount)
            => $"{(currency == "USD" ? "US$" : "S/")} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

        /// <summary>Codifica solo lo que HTML interpreta (&lt; &gt; &amp; comillas): las tildes salen tal cual, legibles.</summary>
        private static string E(string? text)
            => (text ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

        private static string Wrap(string customer, string paragraphs)
            => $"<p>Estimado cliente {E(customer)}:</p>{paragraphs}<p>Saludos cordiales,<br/>{Team}</p>";

        public static (string Subject, string Body) Build(NotificationType type, NoticeContext c)
        {
            var what = $"{c.ProductName} ({c.PlanName})";
            var covered = Date(c.CoveredUntil);
            var price = Money(c.Currency, c.Price);
            switch (type)
            {
                case NotificationType.DueSoon:
                    return ($"Su servicio {c.ProductName} vence el {covered}",
                        Wrap(c.CustomerName, $"<p>Le recordamos que su servicio <b>{E(what)}</b> está cubierto hasta el <b>{covered}</b>.</p>"
                            + $"<p>Para evitar interrupciones, registre su pago ({price}) antes de esa fecha.</p>"));
                case NotificationType.DueToday:
                    return ($"Su servicio {c.ProductName} vence hoy",
                        Wrap(c.CustomerName, $"<p>Su servicio <b>{E(what)}</b> está cubierto hasta hoy, <b>{covered}</b>.</p>"
                            + $"<p>Registre su pago ({price}) para continuar sin interrupciones.</p>"));
                case NotificationType.GraceEntered:
                    var until = c.SuspendOn is DateOnly s ? $" Si no registramos su pago, el servicio se suspenderá el <b>{Date(s)}</b>." : string.Empty;
                    return ($"Su servicio {c.ProductName} venció: periodo de gracia",
                        Wrap(c.CustomerName, $"<p>Su servicio <b>{E(what)}</b> venció el <b>{covered}</b> y está en periodo de gracia: sigue operando.{until}</p>"
                            + $"<p>Registre su pago ({price}) para regularizarlo.</p>"));
                case NotificationType.Suspended:
                    return ($"Su servicio {c.ProductName} fue suspendido",
                        Wrap(c.CustomerName, $"<p>Suspendimos su servicio <b>{E(what)}</b> por falta de pago (estuvo cubierto hasta el <b>{covered}</b>).</p>"
                            + "<p>Mientras tanto, el ERP queda en solo lectura y la API SUNAT no emite comprobantes nuevos; sus datos y comprobantes siguen disponibles para consulta y descarga.</p>"
                            + $"<p>Al registrar su pago ({price}) se reactiva solo.</p>"));
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), "Use PaymentReceiptEmail o CertificateEmail para ese tipo.");
            }
        }

        /// <summary>Constancia de pago: asunto y cuerpo con el detalle del pago.</summary>
        public static (string Subject, string Body) PaymentReceiptEmail(PaymentReceipt r)
        {
            var rows = new StringBuilder();
            void Row(string label, string value) => rows.Append($"<tr><td style=\"padding:4px 12px 4px 0;color:#555\">{E(label)}</td><td><b>{E(value)}</b></td></tr>");
            Row("Referencia", r.Reference);
            Row("Cliente", $"{r.CustomerName} (RUC {r.CustomerRuc})");
            Row("Servicio", $"{r.ProductName} ({r.PlanName})");
            Row("Fecha de pago", Date(r.PaidOn));
            Row("Monto", Money(r.Currency, r.Amount));
            Row("Medio", r.MethodName);
            if (!string.IsNullOrWhiteSpace(r.OperationNumber))
            {
                Row("N.º de operación", r.OperationNumber);
            }
            Row("Periodo que cubre", $"{Date(r.PeriodStart)} al {Date(r.PeriodEnd)} ({r.Periods} {(r.Periods == 1 ? "periodo" : "periodos")})");
            Row("Servicio cubierto hasta", Date(r.CoveredUntil));
            return ($"Constancia de pago {r.Reference}",
                Wrap(r.CustomerName, "<p>Registramos su pago. Conserve esta constancia:</p>"
                    + $"<table style=\"border-collapse:collapse\">{rows}</table>"
                    + "<p>Gracias por su pago. Esta constancia no reemplaza al comprobante electrónico.</p>"));
        }

        /// <summary>Cuerpo que reemplaza al de la invitación cuando ya salió (o ya no puede salir): sin el enlace, por seguridad.</summary>
        public const string PortalInvitationScrubbedBody =
            "<p>Invitación al portal de clientes de la API SUNAT. Por seguridad, el enlace de activación no se conserva una vez enviado.</p>";

        /// <summary>Invitación al portal de clientes: la persona fija su contraseña desde el enlace (válido pocas horas).</summary>
        public static (string Subject, string Body) PortalInvitationEmail(PortalInvitationNotice n)
        {
            var issuers = string.Join("", n.Issuers.Select(i => $"<li>{E(i)}</li>"));
            var link = E(n.ActivationUrl);
            return ("Invitación al portal de clientes de la API SUNAT",
                $"<p>Hola {E(n.Name)}:</p>"
                + $"<p>{E(n.CustomerName)} le dio acceso al portal de clientes de la API SUNAT, donde podrá consultar sus comprobantes y sus credenciales de las empresas:</p>"
                + $"<ul>{issuers}</ul>"
                + $"<p>Para activar su acceso y elegir su contraseña, abra este enlace (vale {n.ValidHours} horas y sirve una sola vez):</p>"
                + $"<p><a href=\"{link}\">{link}</a></p>"
                + "<p>Si no esperaba esta invitación, ignore este correo.</p>"
                + $"<p>Saludos cordiales,<br/>{Team}</p>");
        }

        public static (string Subject, string Body) CertificateEmail(CertificateNotice n)
        {
            var who = $"{n.IssuerName} (RUC {n.IssuerRuc})";
            if (n.DaysLeft < 0)
            {
                return ($"El certificado digital de {n.IssuerRuc} venció",
                    Wrap(n.CustomerName, $"<p>El certificado digital de <b>{E(who)}</b> venció"
                        + (n.ExpiresOn is DateOnly d ? $" el <b>{Date(d)}</b>" : string.Empty)
                        + ". Mientras no lo renueve, SUNAT rechazará sus comprobantes.</p>"
                        + "<p>Renueve el certificado y cárguelo de nuevo en el panel de la API SUNAT.</p>"));
            }
            return ($"El certificado digital de {n.IssuerRuc} vence en {n.DaysLeft} días",
                Wrap(n.CustomerName, $"<p>El certificado digital de <b>{E(who)}</b> vence"
                    + (n.ExpiresOn is DateOnly d2 ? $" el <b>{Date(d2)}</b>" : string.Empty)
                    + $" (faltan {n.DaysLeft} días).</p><p>Renueve el certificado antes de esa fecha para no interrumpir la emisión.</p>"));
        }
    }
}
