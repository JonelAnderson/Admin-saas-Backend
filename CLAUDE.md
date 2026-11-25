# Consola de plataforma (backend) — reglas para el agente

Backend de la consola **interna** que gestiona el SaaS del ERP y la API SUNAT. Diseño: `Docs/plataforma-consola-arquitectura.md`
(fases C1–C6). Proyectos `DS.Platform.*`; nació como copia del ERP el 2026-11-23.

## Reglas heredadas del ERP

Se aplican todas las reglas de la sección "Backend" del `CLAUDE.md` raíz:
- CQRS con MediatR **12.5.0** (no actualizarlo): un caso de uso por archivo; handler `internal` con constructor primario y un
  solo `IRequestHandler`.
- Handlers solo con `IUnitOfWork<TId>`, `ICurrentUserService`, `IDateTimeService`, `IStringLocalizer` e interfaces de
  Application. Nunca `ApplicationDbContext` ni clases de Infrastructure (lo vigila `HandlerDependencyRulesTests`).
- Repositorio por agregado expuesto en `IUnitOfWork` / `UnitOfWork`; un solo `Commit` por caso de uso.
- Mapeo explícito sin AutoMapper (`ExplicitMappingRulesTests`).
- Paginación `ApplyPaging` / `PageOf` (tope 50, nunca `Skip` en Application), búsqueda con `WhereSearch`, combos grandes con
  `Lookup` (máx. 30).
- Controladores delgados con `[Authorize(Policy = Permissions.X.Y)]`; jobs de Hangfire que solo envían un command.
- Secretos fuera de `appsettings`; hablar en español; no usar git; leer un archivo completo antes de borrarlo o
  sobrescribirlo.

## Lo que cambia respecto del ERP

- **No es multiempresa.** `ICurrentUserService` no tiene `CompanyId`, `BranchOfficeId` ni `WarehouseId`; no hay filtro global
  por empresa ni `ICompanyScoped`. No volver a agregarlos.
- **Roles** (`SystemRoleCatalog`):
  - SuperAdmin: todo; no se edita.
  - Administrador: todo al crearse; luego se configura desde Roles.
  - Cobranzas: clientes, suscripciones y pagos (registrar y anular).
  - Soporte: solo consulta.
  - Lectura: solo permisos de consulta.
  - Básico.

  Un administrador llega a todos los usuarios menos a los SuperAdmin (`UserAdminScope`). Un permiso nuevo va en
  `Permissions.cs` y en los roles que correspondan.
- **Fechas de negocio de Lima:** `LimaDate.Today(dateTime.NowUtc)` (`Application/Helpers`). Vencimientos, pruebas y
  suspensiones son `DateOnly`; nunca `DateTime.Now` en un handler.
- **Concurrencia:** lo que dos administradores pueden tocar a la vez implementa `IConcurrencyChecked` y se agrega a
  `ConcurrencyTokenRulesTests`. Hoy: Plan, Customer, Subscription, Payment, ApiSunatIssuer, RemoteAction, Notification, InstanceDeployment, PortalAccess, ApiCredentialDelivery. `ErpInstance` no lo lleva a propósito: lo que informa la instancia es telemetría y gana el último.
- **Menús:** `deploy/sql/menus-platform.sql` (idempotente). Una pantalla nueva es una línea `PERFORM pg_temp.erp_menu(...)` más
  su Url en los roles que la ven.
- **Secretos propios:** base, JWT, certificado de Data Protection y `UserSecretsId` (`ds-platform-webapi-*`). Nunca
  reutilizar los del ERP.

## Módulos

| Módulo | Dónde | Reglas |
| --- | --- | --- |
| **Planes** (referencia) | `Features/Plans`, `PlanController`, `Billing.Plans` | Producto ERP o API SUNAT. Código único por producto (mayúsculas). Los límites del otro producto van vacíos (validador) y null = sin límite. Un plan con suscripciones abiertas no se borra (se desactiva) ni cambia de producto |
| **Clientes** | `Features/Customers`, `CustomerController`, `Core.Customers` | Quien contrata el ERP y / o la API SUNAT. RUC de 11 dígitos con dígito verificador (`RucRules`), único entre los no borrados. Contacto principal en la misma fila. `GET Customer/Lookup` para combos. No se borra con suscripciones abiertas |
| **Suscripciones** | `Features/Subscriptions`, `SubscriptionController`, `Billing.Subscriptions` | Qué cliente tiene qué plan; detalle abajo |
| **Pagos** | `Features/Payments`, `PaymentController`, `Billing.Payments` | Un pago = una suscripción; detalle abajo |
| **Emisores API SUNAT** | `Features/ApiSunat`, `ApiSunatIssuerController`, `ApiSunatIssuers.*` | Empresas de la API SUNAT vinculadas a un cliente; detalle abajo |
| **Instancias ERP** | `Features/Instances`, `Features/Licensing`, `InstanceController`, `LicenseController`, `Instances.*` | Cada ERP de un cliente y su licencia; detalle abajo |
| **Acciones remotas** | `Features/RemoteActions`, `RemoteActionController`, `RemoteActions.*` | Bandeja de lo que la consola pide a la API SUNAT; detalle abajo |
| **Despliegues** | `Features/Deployments`, `DeploymentController`, `Deployments.*` | Montar y actualizar el stack de un cliente en Coolify; detalle abajo |
| **Portal de clientes** | `Features/Portal`, `PortalAccessController`, `PortalAccess.*` | Quién de cada cliente entra al portal de la API SUNAT y a qué empresas; detalle abajo |
| **Credenciales al ERP** | `Features/Credentials`, `ApiCredentialDeliveryController`, `LicenseController` (Pull / Ack), `ApiCredentialDeliveries.*` | Entrega de las credenciales de la API SUNAT de un emisor al ERP de su cliente; detalle abajo |
| **Avisos** | `Features/Notifications`, `NotificationController`, `Notifications.*` | Bandeja de avisos por correo a los clientes; detalle abajo |
| **Tablero** | `Features/Dashboard`, `DashboardController`, `Dashboards.View` | Lo que pide atención en Inicio; detalle abajo |

Reglas de las suscripciones:
- **Una por producto:** como mucho una suscripción no cancelada por cliente y producto (índice único filtrado y control en
  el handler).
- **Alta:**
  - precio, moneda y prueba salen del plan;
  - el precio pactado se puede cambiar (descuento);
  - el día de vencimiento es de 1 a 28.
- **Prueba gratuita:** solo la primera vez por cliente y producto (`EverSubscribedIncludingDeletedAsync`), aunque la anterior
  se haya cancelado o borrado.
- **Estado:** lo calcula **solo** `SubscriptionStatusCalculator.Apply` (puro), en cada command y en el job diario
  `SubscriptionsDaily` (06:00 Lima → `EvaluateSubscriptionsCommand`). Nunca asignar `Status` a mano, salvo Cancelada.
  - Cubierto hasta = el mayor entre el fin de la prueba y "pagado hasta".
  - Prueba / Activa → En gracia (hasta cubierto + días de gracia) → Suspendida.
  - La suspensión manual gana; Cancelada es final.
- **Acciones con motivo obligatorio** (queda en `StatusReason` y en la auditoría):
  - prórroga (`Extend`: "pagado hasta" posterior al actual y no anterior a hoy);
  - suspensión y reactivación manual;
  - cancelación.

  Reactivar una suscripción vencida la deja suspendida por falta de pago, y el mensaje lo dice.
- **Corte efectivo:** el estado vive en la consola. Llegará al ERP por la licencia (fase C4) y a la API SUNAT por las
  acciones remotas (fase C3). Esas llamadas nunca van dentro del caso de uso: van a la bandeja `ops.remote_actions` en el
  mismo `Commit` (diseño §5.1).

Reglas de los pagos:
- **Inmutables:** no hay Update ni Delete. Se anulan (`Void`) con motivo, y solo el **último** pago registrado de la
  suscripción. Además, solo si nada movió "pagado hasta" después (una prórroga). Al anular, "pagado hasta" vuelve a
  `PreviousPaidUntil` y se recalcula el estado.
- **Periodo:** lo calcula el servidor con `PaymentPeriodCalculator`, nunca viene del formulario.
  - Empieza el día siguiente a "cubierto hasta", sin huecos: los días de gracia usados se pagan.
  - Si estuvo suspendida más allá de la gracia, empieza hoy (no se cobran los meses de corte).
  - Termina tras N ciclos (1 mes o 12 meses cada uno), el día anterior.
- **Registro:** extiende `PaidUntil` y recalcula el estado en el **mismo** `Commit`, así que reactiva una suscripción en gracia
  o suspendida por falta de pago. Una suspensión manual se mantiene.
- **Montos:** `ExpectedAmount` = precio pactado × ciclos, solo como referencia (el monto puede diferir por un descuento). La
  moneda debe ser la de la suscripción. La fecha del pago no puede ser futura.
- **Sin duplicados:** el mismo medio + número de operación no se registra dos veces (índice único filtrado y control en el
  handler). Una operación anulada libera el número.
- **Concurrencia:** dos registros a la vez chocan por `xmin` de la suscripción (409).

Reglas de la API SUNAT (fase C3):
- **Emisor** (`apisunat.issuers`, `ApiSunatIssuer`): una empresa de la API SUNAT vinculada a un cliente (un `TenantId` y un RUC se
  vinculan una sola vez). Vincular (`Link`) lee RUC, razón social, ambiente, plan y uso de la API (`IApiSunatAdminClient`), no del
  formulario; `Sync` los refresca. `SuspendDesired` (lo que la consola quiere) y `SuspendApplied` (lo que la API confirmó): si
  difieren hay una acción en camino. No se desvincula mientras esté suspendida por la consola.
- **Cuándo se suspende** (`ApiAccessPolicy`, pura): el cliente tiene suscripciones y **ninguna** vigente (Prueba, Activa, En gracia) de
  cualquier producto. La API SUNAT va incluida en el plan del ERP; contratada sola, tiene su propia suscripción. Sin suscripciones no
  se toca. La suspensión es **por falta de pago** (`tipo = pago`): la empresa sigue activa, consulta y descarga, y solo la emisión
  nueva responde 402. La suspensión manual del panel de la API (403 en todo) manda: la consola no la pisa ni la reactiva
  (`ApiAccessPolicy.AppliedFor`).
- **Corte efectivo:** los 9 casos de uso que cambian el estado de una suscripción (alta, edición, prórroga, suspensión, reactivación,
  cancelación, pagos registrar / anular y `EvaluateSubscriptionsCommand`) llaman a `ApiAccessReconciler.ReconcileAsync` **antes de su
  único `Commit`**. Esa llamada solo escribe en la base: cancela lo pendiente del emisor y encola una `RemoteAction` (Suspender /
  Reactivar). Un caso de uso nuevo que cambie el estado de una suscripción debe llamarlo también. Nunca llaman a la API.
- **Despachador** (`DispatchRemoteActionsCommand`, job Hangfire `RemoteActionsDispatch` cada minuto): toma un lote y lo marca Enviando
  en un guardado propio (si otro despachador lo tomó, el guardado choca por `xmin` y no envía nada: nunca dos veces; una Enviando
  con el bloqueo vencido, 5 min, se retoma), llama a la API y guarda el resultado de cada una por separado. Reintentos con espera
  creciente (`RemoteActionRetryPolicy`: 1 min → 6 h, 10 intentos); un error no reintentable (empresa inexistente, petición
  rechazada) la deja Abandonada; `RetryRemoteActionCommand` la devuelve a Pendiente. El actor viaja en `X-Admin-Actor` (quien la
  originó, o "sistema" si fue el job diario).
- **Cliente HTTP** (`ApiSunatAdminClient`, Infrastructure.Shared; `IApiSunatAdminClient` en Application): `X-Admin-Key` + `X-Admin-Actor`,
  sin redirecciones, tiempo máximo `ApiSunat:TimeoutSeconds`, nunca lanza (devuelve `ApiSunatCallResult` con `Retryable`) y nunca
  registra la clave. Configuración: `ApiSunat:BaseUrl` en appsettings / `API_SUNAT_URL`; la clave `ApiSunat__AdminKey` solo en
  user-secrets / entorno (la misma `ADMIN_SERVICE_KEY` de la API SUNAT, que además lista la IP de la consola en
  `ADMIN_SERVICE_ALLOWED_IPS`). Sin ellas la integración queda apagada y las acciones se reintentan. En tests: `FakeApiSunatAdminClient`
  (`DS.Platform.Tests/Support`) y `DS.Platform.Tests/ApiSunat/ApiSunatTests.cs`.
- **Plan de la API** (`ApiPlanPolicy`, pura, en `ApiAccessReconciler.cs`): `Plan.ApiPlanSlug` (free / pro / business) es obligatorio en un plan de la API y opcional en uno del ERP (API incluida; sin él, el plan del ERP no dice nada de la API y sus límites de API deben ir vacíos). Entre las suscripciones vigentes con API manda el plan de mayor nivel y, a igual nivel, el cupo mayor (sin cupo = el del plan). Sin suscripción vigente con API **no se toca** el plan (la suspensión bloquea la emisión pero no degrada). El reconciliador (mismo `Commit`, nunca llama a la API) deja `PlanDesiredSlug` / `PlanDesiredMaxDocuments` en el emisor y encola una `RemoteAction` `ApplyPlan` (`PlanSlug`, `PlanMaxDocuments`) que el despachador envía con `PUT /api/admin/tenants/{id}/plan`; lo pedido antes de un tipo no cancela lo del otro. Editar un plan reajusta a todos sus clientes (`UpdatePlanCommand`). El cupo de comprobantes es **por empresa emisora** (lo mide la API) y `MaxIssuers` limita cuántas empresas vincula el cliente (`ApiPlanPolicy.IssuerLimit`, en `LinkIssuerCommand`). La API SUNAT trae apagados por defecto el registro público (`REGISTRO_PUBLICO_ENABLED`) y el autoservicio de suscripción (`SUSCRIPCION_AUTOSERVICIO_ENABLED`).
- Permisos: `ApiSunatIssuers.Menu / View / Link / Sync / Unlink` (Cobranzas todos; Soporte consulta y sincroniza) y
  `RemoteActions.Menu / View / Retry` (Cobranzas y Soporte). Menús: grupo "API SUNAT" (4) en `menus-platform.sql`.

Reglas de los despliegues en Coolify (fase C5, `Docs/plataforma-consola-arquitectura.md`):
- **Una instancia = un servicio Docker Compose en Coolify** (`InstanceStack.ComposeYaml`, puro: db, migrate, api, menus y web; imágenes `{repo}/erp-api|erp-migrator|erp-web:{ERP_VERSION}`, sin `build:`, sin puertos publicados, sin el worker de IA). Cada cliente tiene su propia base **dentro** de su stack (no se comparte Postgres: la consola no necesita credenciales de administración de bases). El compose es igual para todos y no cambia con la versión: todo sale de las variables del servicio, así **actualizar = cambiar `ERP_VERSION` y reiniciar**. Los valores secretos van entre comillas en el compose.
- **Nunca dentro de un caso de uso:** `ProvisionInstanceCommand` y `UpgradeInstanceCommand` solo guardan un `InstanceDeployment` (bandeja `ops.InstanceDeployments`, mismos estados y `RemoteActionRetryPolicy` que las acciones remotas; como mucho uno sin terminar por instancia, índice único filtrado) y el job `DeploymentsDispatch` (cada minuto) envía `DispatchDeploymentsCommand`, que toma el lote marcándolo En curso (choque por `xmin` = no se ejecuta dos veces). Solo `ICoolifyClient` habla con Coolify (`CoolifyClient`, Infrastructure.Shared: Bearer, sin redirecciones, nunca lanza, nunca registra token ni valores).
- **Montar (`Create`) avanza por pasos que se guardan al terminar cada uno** (`DeploymentStep`: servicio creado → variables cargadas → arranque pedido): un reintento continúa donde quedó, nunca crea un segundo servicio ni toca las variables de un stack ya arrancado. `PATCH envs` y, si no existe (404), `POST envs`; el servicio se crea con `instant_deploy=false` y se arranca después de cargar todo. Si la consola muere entre el 201 de Coolify y su guardado puede quedar un servicio huérfano `erp-{subdominio}-{id}` (visible en Coolify).
- **Secretos del stack** (contraseña de Postgres, JWT, contraseña inicial de los usuarios del ERP, certificado de Data Protection propio del cliente, clave de licencia): los genera `IProvisioningSecretsGenerator` al montar, se guardan **cifrados con `ISecretProtector`** en `SecretsProtected` solo hasta que el stack arranca y se borran al terminar o cancelar (después solo viven en Coolify). La contraseña inicial se devuelve **una sola vez** al operador (usuario `administrador`); la clave de licencia no se muestra: va directo al stack y en la base queda su hash. Montar renueva la clave de la instancia, por eso solo se permite si **nunca se conectó** (una montada a mano no se duplica) y el cliente tiene suscripción de ERP vigente (si no, nacería en solo lectura).
- **Dirección y versión:** `{subdominio}.{Provisioning:BaseDomain}` (`ProvisioningRules`: minúsculas, números y guion, 2–30, sin nombres reservados; única entre instancias); la versión es la etiqueta de las imágenes (**nunca `latest`**: debe ser rastreable). `ErpInstance.CoolifyServiceUuid` / `DeployedVersion` los pone el despachador. Actualizar exige un montaje terminado, otra versión y ningún despliegue abierto.
- **La consola nunca borra stacks ni bases de clientes:** no existe un despliegue de baja. Cancelar un montaje que ya creó el servicio libera la instancia y el mensaje dice qué servicio eliminar en Coolify; una instancia con stack (o con un despliegue abierto) no se puede borrar de la consola.
- **Configuración:** `Coolify` (`BaseUrl`, `ProjectUuid`, `ServerUuid`, `EnvironmentName`, `DestinationUuid`; el token `Coolify__Token` solo en user-secrets / entorno) y `Provisioning` (`BaseDomain`, `ImageRepository`, `LicenseControlUrl` https, `DefaultVersion`). Sin ellas la integración está apagada y los despliegues esperan. En el compose de Coolify: `COOLIFY_*` y `PROVISIONING_*`.
- Permisos `Deployments.Menu / View / Provision / Upgrade / Retry` (Soporte todos; Cobranzas solo consulta). Menú "Despliegues" en ERP, `/private/erp/deployments`. Tests: `DS.Platform.Tests/Deployments` (`FakeCoolifyClient`, `FakeSecretsGenerator`).

Reglas del acceso al portal de clientes (fase C6, contrato en `Docs/api-sunat-portal-clientes-diseno.md`; el portal es otra app):
- **La consola es dueña de quién entra y a qué empresas; la API SUNAT guarda el usuario real** (contraseña, MFA, sesiones). `PortalAccess` (`ops.PortalAccesses`) es una persona de un cliente (correo único en minúsculas, **un correo pertenece a un solo cliente**) con sus empresas (`PortalAccessIssuer`, solo emisores vinculados a ese cliente). Lo que la consola quiere: `IsEnabled`; lo que la API confirmó: `ApiUserId`, `InvitedOnUtc`, `InvitationExpiresOnUtc`, `InvitationCount`.
- **Nunca dentro de un caso de uso:** `InviteToPortalCommand` (invitar y reinvitar son lo mismo: mismo correo = misma fila, reemplaza empresas, vuelve a habilitar y puede pedir `ResetMfa`), `SetPortalAccessEnabledCommand` y `RetryPortalAccessCommand` solo guardan el trabajo pendiente en la propia fila (`WorkType` Invitar / Cambiar estado; mismos estados y `RemoteActionRetryPolicy` que las acciones remotas; un acceso en curso no se toca: `BusyMessage`). El job `PortalAccessDispatch` (cada minuto) envía `DispatchPortalAccessesCommand`: toma el lote marcándolo En curso (choque por `xmin` = no se ejecuta dos veces), llama a la API y guarda cada resultado por separado. Cliente: `IApiSunatAdminClient.CreatePortalInvitationAsync` (`POST api/admin/portal/invitations` → `{userId, token, expiresAt}`) y `SetPortalUserActiveAsync` (`PUT api/admin/portal/users/{id}/estado`, idempotente); el actor viaja en `X-Admin-Actor` (quien pidió el último cambio).
- **El token de activación es secreto y viaja una sola vez:** `ApiPortalInvitation` es una clase con `ToString` sin el token (un log del resultado no lo muestra). El despachador lo pasa de la respuesta de la API al cuerpo del aviso `PortalInvitation` (`NoticeTemplates.PortalInvitationEmail`, enlace `{Portal:PublicUrl}/activar?token=…`, `PortalRules.ActivationUrl`) en el **mismo guardado** que el acceso, con `DedupKey` `portal-invite:{id}:{n}` (cada invitación su aviso) y el correo de la **persona invitada** (`NoticeSpec.ToEmail`, no el del cliente). El token no se guarda en ningún otro lado ni se devuelve en ninguna respuesta. En cuanto el aviso se envía, se omite o se abandona, `SendNotificationsCommand` reemplaza el cuerpo por `PortalInvitationScrubbedBody` (sin enlace); la auditoría enmascara con `***` el cuerpo de las notificaciones de ese tipo (`AuditableContext.IsSensitiveColumn`); un aviso de invitación abandonado no se reintenta desde Avisos (hay que reinvitar). El enlace vale lo que diga la API (72 h) y los avisos caducan a los 3 días.
- **Reglas:** el cliente debe existir y estar activo y tener al menos una de sus empresas vinculadas; sin `Portal:PublicUrl` https (`IPortalSettings`, appsettings / `PORTAL_PUBLIC_URL`; vacío por defecto) la invitación espera con `PortalRules.NotConfiguredMessage`. Deshabilitar solo existe con usuario en la API; a quien nunca se invitó (sin `ApiUserId`) deshabilitar le **cancela** la invitación y habilitar se rechaza (se invita de nuevo). Habilitar / deshabilitar no manda correo. Un acceso con empresas borradas queda Abandonado con su mensaje.
- Permisos `PortalAccess.Menu / View / Invite / Manage` (Soporte y Cobranzas todos). Menú "Portal" en API SUNAT, `/private/api-sunat/portal`. Migración `Phase9_PortalAccess`. Tests: `DS.Platform.Tests/Portal` (`FakeApiSunatAdminClient.CreatePortalInvitationAsync` / `SetPortalUserActiveAsync`, `FakeMail`).

Reglas de la entrega de credenciales de la API SUNAT al ERP (fase C6b, contrato en `Docs/erp-api-sunat-sincronizacion-diseno.md`):
- **La consola regenera y el ERP recoge; nada secreto sale por la pantalla.** `ApiCredentialDelivery` (`ops.ApiCredentialDeliveries`, `IConcurrencyChecked`) = una entrega de un emisor a una instancia del mismo cliente. Estados (`ApiCredentialDeliveryStatus`, propio, no el de las acciones remotas): Pendiente → En curso → **Lista** (credenciales cifradas, vence a los 7 días) → **Entregada** (el ERP confirmó); Fallida (se reintenta), Abandonada, Cancelada, Vencida. Como mucho **una abierta por emisor** (Pendiente, En curso, Lista o Fallida; índice único filtrado y control en el handler).
- **Nunca dentro de un caso de uso:** `CreateApiCredentialDeliveryCommand` solo guarda la fila Pendiente. Exige `ConfirmInvalidatePrevious` (regenerar **invalida las credenciales actuales** del emisor), cliente / instancia / emisor activos, instancia y emisor del **mismo cliente**, RUC de 11 dígitos. El job `ApiCredentialsDispatch` (cada minuto, `HangfireApiCredentialsJobService`) envía `DispatchApiCredentialDeliveriesCommand`: toma el lote marcándolo En curso con bloqueo de 5 min (choque por `xmin` = no se ejecuta dos veces), llama a `IApiSunatAdminClient.RegenerateTenantCredentialsAsync` (`POST api/admin/tenants/{id}/credenciales/regenerar`, `{motivo}`), guarda la key y el secret con `ISecretProtector` y pasa a Lista; reintentos con `RemoteActionRetryPolicy`. **Sin `ApiSunat:PublicUrl` (https, `API_SUNAT_PUBLIC_URL`; distinta de `API_SUNAT_URL`) no regenera nada** (así las credenciales actuales siguen sirviendo). El mismo despachador pasa a Vencida las Listas sin confirmar a los 7 días.
- **El ERP las recoge** (anónimo con `X-License-Key`, mismo límite por IP "license"; `AnonymousEndpoints`): `POST api/v1/License/ApiSunat/Pull` (`{nonce}`) devuelve `{baseUrl, deliveries[{id, ruc, apiKey, apiSecret}]}` solo de **su instancia** (máx. 20; sin PublicUrl, lista vacía) con `Cache-Control: no-store` y anota `LastPulledOnUtc`; `POST api/v1/License/ApiSunat/Ack` (`{ids}`, máx. 50) pasa a Entregada y **borra el secreto** (idempotente; ids de otra instancia, canceladas o vencidas no cuentan). Clave inválida = el mismo mensaje de la licencia. El secreto se borra también al cancelar y vencer (`CredentialSecrets.Wipe`).
- **Secretos:** `ApiTenantCredentials`, `ApiSunatDeliveryDto` y `ApiSunatPullResponseDto` tienen `ToString` sin valores; `ApiCredentialDeliveryResponseDto` no tiene campo secreto; la auditoría enmascara con `***` `ApiKeyProtected` / `ApiSecretProtected` (`AuditableContext.IsSensitiveColumn`).
- Permisos `ApiCredentialDeliveries.Menu / View / Create / Retry / Cancel` (Soporte todos; Cobranzas Menu y View). Menú "Credenciales al ERP" en ERP, `/private/erp/api-credentials`. Migración `Phase10_ApiCredentialDelivery`. Tests: `DS.Platform.Tests/Credentials` (`FakeApiSunatAdminClient.RegenerateTenantCredentialsAsync`).
- **Pendiente en la API SUNAT:** su rol de servicio (`AdminRole.SERVICIO`) no tiene el permiso `TENANTS_CREDENTIALS`, así que `credenciales/regenerar` respondería 403 (que el cliente trata como reintentable) hasta que se lo den.

Reglas de los avisos por correo y el tablero (fase C2):
- **Bandeja** (`ops.Notifications`, `Notification`): el caso de uso solo **encola** (`NotificationOutbox.EnqueueAsync`, sin `Commit`, en el mismo guardado) con asunto y cuerpo HTML ya armados (`NoticeTemplates`, pura, texto de usuario codificado); nunca envía correo. `DedupKey` es única (tipo + suscripción / empresa + periodo): el mismo aviso del mismo periodo no se repite aunque el job corra dos veces. Un cliente sin correo de contacto deja el aviso **Omitido** (visible en la bandeja). `QueuedOnUtc` (reloj del caso de uso) marca la caducidad.
- **Cuándo avisa** (`NoticeRules`, pura): faltan 1–5 días para "cubierto hasta" → por vencer; ese día → vence hoy; En gracia → gracia; Suspendida por falta de pago → suspensión (la manual y la cancelada **no** avisan). `EvaluateSubscriptionsCommand` (job diario) los encola; `RegisterPaymentCommand` encola la **constancia de pago** (en el cuerpo del correo, referencia `C{suscripción}-{fin del periodo}`: el Id del pago aún no existe al encolar) y `QueueCertificateNoticesCommand` (job `CertificateNoticesDaily`, 08:00 Lima) los de certificados digitales de las empresas vinculadas (`GET /api/admin/certificates`, tramos 30 / 7 / 1 día y vencido; renovar con otra fecha reinicia los avisos).
- **Envío** (`SendNotificationsCommand`, job `NotificationsDispatch` cada 5 min): igual que las acciones remotas (toma un lote marcándolo Enviando, choque por `xmin` = no se envía dos veces); `IMailService.IsConfigured` falso (sin `MailSettings`: en appsettings quedan vacíos, los valores van en user-secrets / `MAIL_*`) = no toca nada y los avisos esperan; 5 intentos con espera 5 min → 30 min → 2 h → 6 h (`NotificationRetryPolicy`); agotados quedan Abandonados; a los 3 días sin enviarse caducan (Omitidos: un aviso de cobro viejo ya no sirve). `RetryNotificationCommand` reinicia uno Fallido / Abandonado.
- **Tablero** (`GetDashboardQuery`, `GET v1/Dashboard`, `Dashboards.View`; solo lee): suscripciones por estado, cobrado del mes de Lima por moneda, vencimientos de los próximos 7 días, instancias sin contacto (> 1 día) y con versión menor a la más alta, acciones remotas y avisos que piden atención, y el resumen de la API SUNAT (`GET /api/admin/dashboard` + rechazos del día). Si la API no responde, esa parte llega con `available: false` y el motivo: el tablero no falla por una caída ajena.
- Permisos `Notifications.Menu / View / Retry` (Cobranzas todos; Soporte consulta). Menú: "Avisos" en Comercial, `/private/billing/notifications`. Tests: `DS.Platform.Tests/Notifications` (`FakeMail`), `DS.Platform.Tests/ApiSunat/ApiPlanTests.cs`.

Reglas de las licencias del ERP (fase C4):
- **Instancia** (`erp.instances`, `ErpInstance`): un ERP de un cliente (nombre único por cliente). Su clave `lic_...` se genera al crearla o
  rotarla, viaja **una sola vez** en la respuesta y en la base solo queda su SHA-256 (`LicenseKeys`). Rotar invalida la anterior al
  instante. El cliente no se borra mientras tenga instancias.
- **Estado de licencia** (`LicenseStateBuilder`, puro): sale de la suscripción **del ERP** del cliente (una de la API SUNAT no cuenta).
  Prueba / Activa → Active, En gracia → Grace, Suspendida → Suspended (solo lectura), solo canceladas → Cancelled, sin suscripción del
  ERP → Suspended. Lleva los límites del plan (empresas, usuarios, sucursales, IA; null = sin límite) y un mensaje para Grace,
  Suspended y los 5 días previos al vencimiento.
- **Consulta** (`POST api/v1/License/Status`, anónima, cabecera `X-License-Key`, `CheckLicenseCommand`): la instancia consulta cada 10 min
  con un `nonce` (16–64 caracteres) y su telemetría (versión, migración, empresas, usuarios, sucursales, documentos del mes). La respuesta
  es `{ payload, signature, algorithm }`: el payload es el JSON que se firma tal cual con Ed25519 (`ILicenseSigner`,
  `Ed25519LicenseSigner`, `License:SigningKey`) y repite el nonce y `issuedAt`. Clave desconocida, vacía, inactiva o borrada → siempre el
  mismo mensaje; sin llave de firma no se entrega nada. `LicenseIssues` guarda una fila solo cuando cambia lo entregado.
  `GET api/v1/License/PublicKey` entrega la llave pública (no secreta). Límite de 30 consultas por minuto y por IP
  (`RateLimitingExtensions`); el único endpoint anónimo nuevo, listado en `AuthorizationDefaults.AnonymousEndpoints`.
- Permisos `Instances.Menu / View / Create / Edit / RotateKey / Delete` (Cobranzas todos menos Delete; Soporte y Lectura consulta).
  Menú: grupo "ERP" (3), `/private/erp/instances`. Tests: `DS.Platform.Tests/Licensing`.

## Pendiente (fases)

- **C2 y C3:** cerradas (2026-11-24). Quedan sin probar contra sistemas reales: el correo saliente (SMTP real), el `PUT plan` y
  `GET certificates` / `dashboard` / `rejections` contra una API SUNAT desplegada (solo pruebas unitarias con `FakeApiSunatAdminClient`), y los ITs
  de la API SUNAT (sin Docker aquí). Decisión por defecto a confirmar: el cupo de comprobantes del plan de la API es por empresa emisora.
  Los planes de la API creados antes de `Phase6_ApiPlanApply` no tienen `ApiPlanSlug`: edítelos para elegir free / pro / business.
- **C4 (resto):** probar la licencia entre una consola y un ERP reales (hasta ahora solo pruebas unitarias con firma Ed25519 real), y la integración entre el ERP y la consola en la e2e.
- **C5 (hecha, sin probar contra un Coolify real):** el cliente HTTP sigue la documentación de la API de Coolify (`POST /services`, `PATCH|POST /services/{uuid}/envs`, `POST /services/{uuid}/start` y `/restart?latest=true`) pero nunca corrió contra uno. A verificar en el primer montaje real: que el dominio con puerto (`https://host:80` en `urls`) enrute a `web`, que Coolify tome las variables que se cargan por API para interpolar `${...}` del compose, y que `start` arranque un servicio recién creado sin despliegue previo. **Prerrequisito que falta:** publicar las imágenes `erp-api`, `erp-migrator` y `erp-web` en un registro con versión (fase 1 del análisis SaaS; el ERP hoy compila en cada despliegue). Fuera de alcance por decisión: Postgres compartido entre clientes, worker de IA por cliente, baja automática de stacks y respaldo por cliente (siguen en el runbook).

## Verificar después de cada cambio

```bash
dotnet build DS.Platform.sln -c Release
dotnet test DS.Platform.Tests -c Release
```

Si cambia el modelo, crear la migración `PhaseN_Descripcion` (comando en `README.md`). Debe poder correr dentro del script
idempotente: nada de `CREATE INDEX CONCURRENTLY`.
