# Etapa de construcción (Build)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar los archivos .csproj y restaurar dependencias
COPY ["DS.Platform.WebApi/DS.Platform.WebApi.csproj", "DS.Platform.WebApi/"]
COPY ["DS.Platform.Application/DS.Platform.Application.csproj", "DS.Platform.Application/"]
COPY ["DS.Platform.Domain/DS.Platform.Domain.csproj", "DS.Platform.Domain/"]
COPY ["DS.Platform.Infrastructure.Persistence/DS.Platform.Infrastructure.Persistence.csproj", "DS.Platform.Infrastructure.Persistence/"]
COPY ["DS.Platform.Infrastructure.Shared/DS.Platform.Infrastructure.Shared.csproj", "DS.Platform.Infrastructure.Shared/"]
COPY ["DS.Platform.Shared/DS.Platform.Shared.csproj", "DS.Platform.Shared/"]
RUN dotnet restore "DS.Platform.WebApi/DS.Platform.WebApi.csproj"

# Copiar todo el código fuente y construir
COPY . .
WORKDIR "/src/DS.Platform.WebApi"
RUN dotnet build "DS.Platform.WebApi.csproj" -c Release -o /app/build

# Etapa de publicación (Publish)
FROM build AS publish
RUN dotnet publish "DS.Platform.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa final (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Exponer el puerto por defecto de ASP.NET 10
EXPOSE 8080

# CREACIÓN EXPLÍCITA DEL DIRECTORIO PARA EL VOLUMEN (CRÍTICO)
# Esto garantiza que el montaje de Coolify coincida exactamente con un directorio existente
RUN mkdir -p /app/wwwroot

# Crear usuario con UID 1000 (estándar de Ubuntu/Coolify) para evitar problemas de permisos de volumen
RUN adduser --uid 1000 --disabled-password --gecos "" coolifyuser

# Asignar permisos al usuario creado (UID 1000)
RUN chown -R coolifyuser:coolifyuser /app/wwwroot

# Cambiar al usuario no root por seguridad y compatibilidad de permisos
USER coolifyuser

# Copiar los binarios compilados
COPY --from=publish /app/publish .

# Definir el punto de entrada
ENTRYPOINT ["dotnet", "DS.Platform.WebApi.dll"]
