# syntax=docker/dockerfile:1
# API de la consola de plataforma (.NET 10) y su "migrator" (psql + script idempotente de migraciones + menús).
# Contexto de build: la raíz de este repositorio (Admin-saas-Backend). Lo usa docker-compose.coolify.yml.
ARG DOTNET_VERSION=10.0

# ------------------------------------------------------------------ build
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY . ./
RUN dotnet restore DS.Platform.WebApi/DS.Platform.WebApi.csproj
RUN dotnet publish DS.Platform.WebApi/DS.Platform.WebApi.csproj -c Release -o /out/api --no-restore /p:UseAppHost=false

# ------------------------------------------------------------------ script de migraciones (idempotente)
# EF solo arma el host (no arranca la API ni toca una base): la cadena y el secreto son de mentira y el secreto se genera
# al azar en cada build, así que ningún valor real queda en la imagen.
FROM build AS migrations
# dotnet-ef con la versión del manifiesto (dotnet-tools.json): una sola fuente de la versión.
RUN dotnet tool restore
RUN mkdir -p /out \
 && ASPNETCORE_ENVIRONMENT=Development \
    ConnectionStrings__DefaultConnection="Host=127.0.0.1;Database=design_only;Username=x;Password=x" \
    AppConfiguration__Secret="$(head -c 48 /dev/urandom | base64)" \
    dotnet ef migrations script --idempotent --configuration Release \
      --project DS.Platform.Infrastructure.Persistence --startup-project DS.Platform.WebApi --context ApplicationDbContext \
      -o /out/migrate.sql

# ------------------------------------------------------------------ migrator: aplica migraciones y menús con psql
FROM postgres:18-alpine AS migrator
COPY --from=migrations /out/migrate.sql /migrate/migrate.sql
COPY deploy/sql/menus-platform.sql /migrate/menus-platform.sql
COPY deploy/migrator-entrypoint.sh /usr/local/bin/platform-migrate
RUN sed -i 's/\r$//' /usr/local/bin/platform-migrate && chmod +x /usr/local/bin/platform-migrate
ENTRYPOINT ["platform-migrate"]
CMD ["migrate"]

# ------------------------------------------------------------------ api
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS api
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    TZ=America/Lima
COPY --from=build /out/api ./
# wwwroot = archivos subidos (fotos de usuarios); Logs; llaves de Data Protection.
# Son volúmenes.
RUN mkdir -p /app/wwwroot /app/Logs /app/dp-keys && chown -R app:app /app/wwwroot /app/Logs /app/dp-keys
USER app
EXPOSE 8080
# Healthcheck sin curl (no se instala nada): bash abre la conexión con /dev/tcp y busca "Healthy" en /health.
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=5 \
  CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && grep -q Healthy <&3"]
ENTRYPOINT ["dotnet", "DS.Platform.WebApi.dll"]
