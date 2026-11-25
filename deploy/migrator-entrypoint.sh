#!/bin/sh
# Migrator de la consola de plataforma. Usa las variables estándar de psql: PGHOST, PGPORT, PGDATABASE, PGUSER, PGPASSWORD.
#   migrate -> aplica el script idempotente de migraciones (se corre en cada despliegue)
#   menus   -> aplica menus-platform.sql (idempotente; necesita que la API haya arrancado una vez: roles)
set -eu

wait_db() {
  i=0
  until pg_isready -q; do
    i=$((i + 1))
    if [ "$i" -ge 60 ]; then
      echo "La base no responde (PGHOST=${PGHOST:-})." >&2
      exit 1
    fi
    sleep 2
  done
}

case "${1:-migrate}" in
  migrate)
    wait_db
    echo "Aplicando migraciones..."
    psql -v ON_ERROR_STOP=1 -q -f /migrate/migrate.sql
    echo "Migraciones al día."
    ;;
  menus)
    wait_db
    echo "Aplicando menús..."
    psql -v ON_ERROR_STOP=1 -q -f /migrate/menus-platform.sql
    echo "Menús al día."
    ;;
  *)
    exec "$@"
    ;;
esac
