# DS.Platform — Backend de la consola de plataforma

API de la consola **interna** que gestiona el SaaS del ERP (clientes, planes, suscripciones, cobros, licencias de cada
instancia) y la API SUNAT (empresas emisoras, uso, rechazos). Diseño completo:
[`Docs/plataforma-consola-arquitectura.md`](../../Docs/plataforma-consola-arquitectura.md).

- **Stack:** .NET 10 · ASP.NET Core · EF Core 10 + Npgsql · PostgreSQL 18 · MediatR 12.5.0 · FluentValidation · Hangfire ·
  Serilog · ASP.NET Identity + JWT + MFA obligatorio.
- **Arquitectura:** la misma del ERP (clean architecture, CQRS con MediatR, repositorios + Unit of Work, mapeo explícito sin
  AutoMapper). Reglas en [`CLAUDE.md`](CLAUDE.md).
- **Nació como copia del ERP** (2026-11-23): se quitaron todos los módulos de negocio y la multiempresa; quedaron Identity
  (login, MFA, sesiones, roles, permisos), usuarios, menús, auditoría, correo, archivos y Hangfire.
- **Módulos:** Planes (referencia), Clientes, Suscripciones (qué cliente tiene qué plan, con prueba, gracia, suspensión y
  cancelación; job diario `SubscriptionsDaily`) y Pagos (inmutables: extienden la suscripción y se anulan con motivo). Detalle y reglas en `CLAUDE.md`.

## Proyectos

```
DS.Platform.Domain                     Entidades (Identity, Menus, Audit, Customers, Billing: Plan y Subscription), enums, contratos
DS.Platform.Application                Features (Commands/Queries MediatR), DTOs, mapeos, validadores, interfaces
DS.Platform.Infrastructure.Persistence ApplicationDbContext, configuraciones EF, migraciones, repositorios, Identity, seeder
DS.Platform.Infrastructure.Shared      Correo, fecha
DS.Platform.Shared                     Permisos, roles (SystemRoleCatalog), Result, localización
DS.Platform.WebApi                     Controladores, middlewares, arranque
DS.Platform.Tests                      xUnit: handlers con TestDatabase + UnitOfWork, reglas de arquitectura, Identity/MFA
```

## Desarrollo

Secretos en user-secrets (nunca en `appsettings*.json`):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5432;Database=platform;Username=platform;Password=..." --project DS.Platform.WebApi
dotnet user-secrets set "AppConfiguration:Secret" "<64+ caracteres aleatorios>" --project DS.Platform.WebApi
dotnet user-secrets set "Seed:InitialAdminPassword" "<contraseña inicial>" --project DS.Platform.WebApi   # opcional en Development
```

La base `platform` puede vivir en el PostgreSQL compartido de desarrollo (`postgres-dev`), con su propio usuario:

```sql
CREATE ROLE platform LOGIN PASSWORD '...';
CREATE DATABASE platform OWNER platform;
```

Después:

```bash
dotnet build -c Release
dotnet test DS.Platform.Tests -c Release
~/.dotnet/tools/dotnet-ef.exe database update --project DS.Platform.Infrastructure.Persistence \
  --startup-project DS.Platform.WebApi --context ApplicationDbContext --configuration Release
dotnet run --project DS.Platform.WebApi          # crea roles y usuarios iniciales
psql -d platform -f deploy/sql/menus-platform.sql
```

Usuarios iniciales: `superadmin` (SuperAdmin), `administrador` (Administrador) y, solo en Development, `basicadmin`. Todos
entran con MFA.

Migración nueva:

```bash
~/.dotnet/tools/dotnet-ef.exe migrations add PhaseN_Descripcion --project DS.Platform.Infrastructure.Persistence \
  --startup-project DS.Platform.WebApi --context ApplicationDbContext --configuration Release
```

## Despliegue

Coolify con `docker-compose.coolify.yml` (db → migrate → api → menus). Runbook: [`deploy/README.md`](deploy/README.md).
