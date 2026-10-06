#!/usr/bin/env bash
# Instantáneas de la base para poder probar las operaciones que ESCRIBEN.
#
# El problema: comparar un "cancelar venta" no es como comparar una consulta.
# La primera llamada cambia la base, así que la segunda ya no arranca del mismo
# sitio y la comparación no vale nada. Hay que poder volver atrás.
#
# La solución: una instantánea de SQL Server (Developer Edition las trae). Es
# instantánea de verdad -copy on write, no copia los 3.2 GB- y volver atrás
# tarda segundos. El ciclo de una prueba de escritura es:
#
#     crear  ->  escribir en Legacy  ->  anotar  ->  volver
#            ->  escribir en el nuevo ->  anotar  ->  volver  ->  comparar
#
# Así las dos operaciones salen EXACTAMENTE del mismo estado, que es lo único
# que hace honesta la comparación.
#
# Uso:
#   ./instantanea.sh crear
#   ./instantanea.sh volver
#   ./instantanea.sh soltar
#   ./instantanea.sh estado
#
# OJO: volver atrás necesita la base en exclusiva, así que tira las conexiones
# de los dos backends. Ellos reconectan solos en la siguiente petición.

set -u

SERVIDOR="${MAC_SQL_SERVIDOR:-LAP-JUAND}"
BASE="${MAC_SQL_BASE:-Produccion_svr}"
USUARIO="${MAC_SQL_USUARIO:-sa}"
CLAVE="${MAC_SQL_CLAVE:-123}"
INSTANTANEA="${BASE}_banco"

sql() {
  MSYS_NO_PATHCONV=1 sqlcmd -S "$SERVIDOR" -U "$USUARIO" -P "$CLAVE" -C -W -I -b -Q "$1" 2>&1
}

# La instantánea tiene que vivir junto a los datos, con un nombre propio.
ruta_instantanea() {
  sql "SET NOCOUNT ON;
       SELECT LEFT(physical_name, LEN(physical_name) - CHARINDEX('\\', REVERSE(physical_name)))
       FROM sys.master_files
       WHERE database_id = DB_ID('$BASE') AND type = 0;" | sed -n '3p' | tr -d '\r'
}

crear() {
  if sql "SET NOCOUNT ON; SELECT 1 FROM sys.databases WHERE name='$INSTANTANEA';" | grep -q "^1$"; then
    echo "Ya existe '$INSTANTANEA'. Usa 'volver' o 'soltar'."
    return 1
  fi

  local carpeta
  carpeta="$(ruta_instantanea)"
  if [ -z "$carpeta" ]; then
    echo "No se pudo averiguar dónde viven los datos de $BASE."
    return 1
  fi

  # Una instantánea necesita UN archivo por cada archivo de datos del original,
  # con el mismo nombre lógico. Se arma la lista desde sys.master_files en vez
  # de escribirla a mano: si algún día la base crece un filegroup, esto sigue
  # funcionando.
  local lista
  lista="$(sql "SET NOCOUNT ON;
    SELECT STRING_AGG(CONCAT('(NAME=[', name, '], FILENAME=''', '$carpeta\\', name, '_banco.ss'')'), ', ')
    FROM sys.master_files
    WHERE database_id = DB_ID('$BASE') AND type = 0;" | sed -n '3p' | tr -d '\r')"

  echo "Creando instantánea de $BASE..."
  sql "CREATE DATABASE [$INSTANTANEA] ON $lista AS SNAPSHOT OF [$BASE];" || return 1
  echo "Lista: $INSTANTANEA"
}

volver() {
  echo "Volviendo $BASE al estado de la instantánea..."
  # Las conexiones de los backends impiden el restore; se tiran y reconectan
  # solas. Es el precio de poder probar escrituras de verdad.
  sql "ALTER DATABASE [$BASE] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;" || return 1
  sql "RESTORE DATABASE [$BASE] FROM DATABASE_SNAPSHOT = '$INSTANTANEA';" || {
    sql "ALTER DATABASE [$BASE] SET MULTI_USER;"
    return 1
  }
  sql "ALTER DATABASE [$BASE] SET MULTI_USER;" || return 1
  echo "Restaurada."
}

soltar() {
  sql "IF DB_ID('$INSTANTANEA') IS NOT NULL DROP DATABASE [$INSTANTANEA];" >/dev/null
  echo "Instantánea eliminada."
}

estado() {
  sql "SET NOCOUNT ON;
       SELECT name, create_date FROM sys.databases WHERE name IN ('$BASE','$INSTANTANEA');"
}

case "${1:-}" in
  crear)  crear  ;;
  volver) volver ;;
  soltar) soltar ;;
  estado) estado ;;
  *) sed -n '2,30p' "$0" ; exit 2 ;;
esac
