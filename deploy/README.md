# Despliegue de la consola de plataforma (backend) en Coolify

Mismo esquema que el backend del ERP, con recursos y secretos **propios**: la consola nunca comparte base, volúmenes, JWT ni
certificado de Data Protection con el ERP.

```
docker-compose.coolify.yml   db (PostgreSQL 18) -> migrate (script idempotente de EF) -> api -> menus
deploy/api.Dockerfile        build · migrations · migrator (psql) · api
deploy/migrator-entrypoint.sh  migrate | menus
deploy/sql/menus-platform.sql  menú de la consola (idempotente)
deploy/scripts/generate-dp-cert.sh  certificado de Data Protection (deploy/secrets/, ignorado por git y Docker)
```

## 1. Secretos (una vez por entorno)

| Variable de Coolify | Qué es | Cómo se obtiene |
| --- | --- | --- |
| `POSTGRES_PASSWORD` | Contraseña de la base `platform` | `openssl rand -hex 24`. Solo se aplica al crear el volumen |
| `JWT_SECRET` | Firma de los JWT de la consola | `openssl rand -hex 48` (distinto del ERP) |
| `PLATFORM_INITIAL_ADMIN_PASSWORD` | Contraseña inicial de `superadmin` y `administrador` | 8–16 caracteres, mayúscula, minúscula, número y especial. Marcar **Is Literal** si lleva `$` |
| `DP_CERT_BASE64`, `DP_CERT_PASSWORD` | Certificado de Data Protection de la consola | `bash deploy/scripts/generate-dp-cert.sh`; nunca reutilizar el del ERP ni regenerarlo después |
| `PLATFORM_PUBLIC_URL` | URL pública del frontend de la consola | p. ej. `https://consola.<dominio>` |
| `PLATFORM_SHARED_NETWORK` | Red compartida con el frontend (alias `platform-api:8080`) | `platform-prod`; QA en el mismo servidor: `platform-qa` |
| `MAIL_*` | Correo saliente (recuperación de contraseña, avisos) | Igual que en el ERP |
| `API_SUNAT_URL` | Dirección de la API SUNAT (sin ella no se aplican los cortes: las acciones remotas se reintentan) | Interna de Coolify, p. ej. `http://erp-sunat-api:8081` |
| `API_SUNAT_PUBLIC_URL` | Dirección **https** pública de la API SUNAT (`https://api.midominio.pe`): la que la consola entrega al ERP junto con las credenciales de su empresa (fase C6b). **Distinta de `API_SUNAT_URL`**, que puede ser interna | Sin ella las entregas de credenciales esperan (con un aviso en la pantalla "Credenciales al ERP") y el ERP recibe una lista vacía |
| `LICENSE_SIGNING_KEY` | Llave privada Ed25519 (32 bytes en base64) que firma las licencias del ERP; sin ella no se entregan licencias. Generar: `openssl rand -base64 32`. La pública sale de `GET /api/v1/License/PublicKey` y se pone en cada ERP (`License__PublicKey`). **Rotarla obliga a actualizar la pública de todos los ERP** | Secreto de Coolify |
| `API_SUNAT_ADMIN_KEY` | Clave de servicio (≥ 32 bytes) que la consola envía en `X-Admin-Key` | La misma `ADMIN_SERVICE_KEY` de la API SUNAT, que además debe listar la IP de la consola en `ADMIN_SERVICE_ALLOWED_IPS` |
| `COOLIFY_URL`, `COOLIFY_TOKEN` | Coolify donde se montan las instancias de los clientes (fase C5). El token (Keys & Tokens → API tokens) necesita permisos de escritura y despliegue | Sin ellas, montar y actualizar instancias queda apagado (los despliegues esperan) |
| `COOLIFY_PROJECT_UUID`, `COOLIFY_SERVER_UUID`, `COOLIFY_ENVIRONMENT`, `COOLIFY_DESTINATION_UUID` | Proyecto, servidor, entorno (por defecto `production`) y, solo si el servidor tiene varios destinos de Docker, el destino | Se leen del panel de Coolify. Recomendado un proyecto aparte para los clientes |
| `PROVISIONING_BASE_DOMAIN` | Dominio base: la instancia de un cliente queda en `{subdominio}.{dominio}` | Un DNS comodín `*.erp.<dominio>` hacia el VPS; Traefik reparte |
| `PROVISIONING_IMAGE_REPO` | Repositorio de imágenes ya publicadas (p. ej. `ghcr.io/cuenta`): `erp-api`, `erp-migrator` y `erp-web`, con la versión como etiqueta | **Las imágenes las publica un pipeline aparte** (fase 1 del análisis SaaS, aún pendiente); sin ellas el stack no arranca |
| `PROVISIONING_LICENSE_CONTROL_URL` | Dirección **https** pública de la consola (la que cada ERP consulta para su licencia) | Solo el endpoint de licencias debe quedar accesible desde internet |
| `PROVISIONING_DEFAULT_VERSION` | Versión que el formulario propone al montar | Opcional |
| `PORTAL_PUBLIC_URL` | Dirección **https** pública del portal de clientes de la API SUNAT: de aquí sale el enlace `…/activar?token=…` de la invitación por correo | Sin ella las invitaciones esperan (con un aviso en la pantalla Portal). El portal es otra app; el token del enlace vale 72 h y se usa una sola vez |

Ninguna variable va marcada como *Available at Buildtime*. Después de cambiar una variable: **Redeploy** (Restart reusa las
anteriores).

## 2. Recurso en Coolify

1. Nuevo recurso → Docker Compose → este repositorio, Base Directory `/`, Compose `/docker-compose.coolify.yml`.
2. Dominio en el servicio `api` con el puerto del contenedor: `https://api-consola.<dominio>:8080`.
3. **Acceso restringido:** la consola es interna. Restringir el dominio por IP (middleware `ipallowlist` de Traefik en las
   etiquetas del servicio) o publicarla solo por VPN. El único endpoint que deberá quedar accesible desde internet es el de
   licencias de los ERP (fase C4), en una ruta aparte.
4. Deploy. El primer arranque crea los roles y los usuarios iniciales; `menus` aplica el menú.

## 3. Base de datos

Publicada solo en `127.0.0.1:5435` del VPS (el ERP usa 5433). Desde DBeaver: túnel SSH al VPS y conexión a
`127.0.0.1:5435`, base `platform`.

Respaldo: el mismo procedimiento del ERP (tarea programada de Coolify con `pg_dump` al volumen `platform-db-backups`,
copiado fuera del servidor).

## 4. Despliegue automático

`.github/workflows/deploy-prod.yml` y `deploy-qa.yml` llaman al webhook de Coolify al hacer push a `deploy-prod` /
`deploy-qa`. Secretos del repositorio: `COOLIFY_PROD_URL`, `COOLIFY_PROD_TOKEN`, `COOLIFY_QA_URL`, `COOLIFY_QA_TOKEN`
(los del recurso de la consola, no los del ERP).
