#!/usr/bin/env bash
# Crea el certificado que cifra las llaves de Data Protection (secretos MFA de la consola) para Coolify. La consola usa SU PROPIO certificado, nunca el del ERP.
# UNA sola vez por instalación (producción y QA, cada una el suyo). Si se pierde el certificado o su contraseña, los secretos
# guardados ya no se pueden leer: guarde dp-cert.pfx y la contraseña en un gestor de contraseñas.
#   DP_CERT_PASSWORD='...' ./deploy/scripts/generate-dp-cert.sh
# Deja en deploy/secrets/ (ignorado por git y por Docker):
#   dp-cert.pfx          el certificado
#   dp-cert.base64.txt   su contenido en Base64, para la variable DP_CERT_BASE64 de Coolify
# No imprime ningún secreto en pantalla.
set -euo pipefail
# Git Bash (Windows) convierte "/CN=..." en una ruta de Windows y openssl falla; en Linux no hace nada.
export MSYS_NO_PATHCONV=1
cd "$(dirname "$0")/.."

if [ -z "${DP_CERT_PASSWORD:-}" ]; then
  echo "Falta DP_CERT_PASSWORD (genere una con: openssl rand -base64 32 | tr -d '/+=;')." >&2
  exit 1
fi
if [ -f secrets/dp-cert.pfx ]; then
  echo "deploy/secrets/dp-cert.pfx ya existe: no se reemplaza (los secretos guardados dependen de él)." >&2
  exit 1
fi

mkdir -p secrets
chmod 700 secrets
# Carpeta temporal relativa (no /tmp): con MSYS_NO_PATHCONV el openssl de Windows no entendería "/tmp/...".
work="secrets/.tmp-$$"
mkdir -p "$work"
trap 'rm -rf "$work"' EXIT
openssl req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes \
  -subj "/CN=erp-dataprotection" -keyout "$work/dp.key" -out "$work/dp.crt" 2>"$work/req.log" \
  || { cat "$work/req.log" >&2; exit 1; }
DP_CERT_PASSWORD="$DP_CERT_PASSWORD" openssl pkcs12 -export \
  -inkey "$work/dp.key" -in "$work/dp.crt" -out secrets/dp-cert.pfx -passout env:DP_CERT_PASSWORD
openssl base64 -A -in secrets/dp-cert.pfx -out secrets/dp-cert.base64.txt
chmod 600 secrets/dp-cert.pfx secrets/dp-cert.base64.txt
echo "Listo (válido 10 años):"
echo "  deploy/secrets/dp-cert.pfx         -> respáldelo junto con su contraseña"
echo "  deploy/secrets/dp-cert.base64.txt  -> péguelo en Coolify como DP_CERT_BASE64 (marcado como secreto)"
