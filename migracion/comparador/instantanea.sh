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
#   ./instantanea.sh reponer   # vuelve a poner nuestros sp_w_ (lo hace 'volver' solo)
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

# Candado compartido. Varios agentes trabajan a la vez contra la MISMA base, y
# restaurar mientras otro está a mitad de una tanda le invalida la comparación
# sin que se entere: sus dos lados dejan de salir del mismo estado. Ya pasó tres
# veces en una sola jornada, y además deja el pool de conexiones envenenado.
#
# Confiar en que cada quien consulte el candado no alcanzó. Ahora `volver`
# FALLA si hay un candado de otro.
CANDADOS="${MAC_CANDADOS:-/tmp}"
MI_CANDADO="${MAC_CANDADO:-}"

candado_ajeno() {
  for c in "$CANDADOS"/instantanea-en-uso-*; do
    [ -e "$c" ] || continue
    [ "$c" = "$CANDADOS/instantanea-en-uso-$MI_CANDADO" ] && continue
    echo "$c"
    return 0
  done
  return 1
}

volver() {
  local ajeno
  if ajeno="$(candado_ajeno)"; then
    echo "NO se restaura: otro agente tiene la base tomada ($ajeno)."
    echo "Espera a que lo suelte, o usa transacciones con rollback."
    echo "Si estás seguro de que quedó huérfano, bórralo a mano."
    return 1
  fi

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
  reponer_procedimientos_nuestros
}

# ---------------------------------------------------------------------------
# RESTAURAR SE LLEVA LOS PROCEDIMIENTOS QUE HEMOS DESPLEGADO
#
# Una instantánea es un punto en el tiempo de la base ENTERA, esquema incluido.
# Así que `volver` no solo deshace los datos de una prueba: también borra los
# `sp_w_` que se instalaron DESPUÉS de crearla.
#
# No es teórico, pasó dos veces en una tarde con el mismo procedimiento. El
# síntoma no se parece a la causa: el backend empieza a contestar
#
#     500  Could not find stored procedure 'sp_w_ConsultaConstantesSoloLectura'
#
# y el agente que lo ve se va a buscar el error a su código, cuando lo que pasó
# es que otro restauró la base. Peor: ese procedimiento es el que evita que
# leer las constantes CIERRE EL DÍA del negocio, así que mientras falta, la
# consulta de ventas de Zaragoza está caída.
#
# Por eso se reponen aquí y no "cuando alguien se acuerde": son nuestros,
# `CREATE OR ALTER` y lectura pura, así que volver a aplicarlos es gratis y
# nunca es incorrecto.
# ---------------------------------------------------------------------------
reponer_procedimientos_nuestros() {
  local raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../Querrys" 2>/dev/null && pwd)"
  if [ -z "$raiz" ] || [ ! -d "$raiz" ]; then
    echo "AVISO: no encontré la carpeta Querrys; revisa a mano los sp_w_ de $BASE."
    return 0
  fi

  # LISTA EXPLÍCITA, y no un `find` de todos los sp_w_ del repo.
  #
  # Querrys tiene más de 180 guiones de sp_w_, y la mayoría son de OTROS
  # proyectos (SL, Wahub, el dashboard) que no viven en estas bases: aplicarlos
  # todos crearía objetos ajenos en MacZ y tardaría una eternidad.
  #
  # Aquí van solo los que ESTA migración desplegó y que `volver` se lleva. Si
  # despliegas uno nuevo, agrégalo a esta lista — es la única forma de que
  # sobreviva a la siguiente restauración.
  local nuestros=(
    "31 Constantes Solo Lectura/01_sp_w_ConsultaConstantesSoloLectura.sql"
    "32 Reporte Autos Columna Mala/01_sp_w_rptAutos.sql"
  )

  local puestos=0
  local guion
  for relativo in "${nuestros[@]}"; do
    guion="$raiz/$relativo"
    [ -f "$guion" ] || { echo "AVISO: no está $relativo"; continue; }
    # Se aplica con USE explícito: fiarse de -d ya hizo que un procedimiento
    # acabara en la base equivocada y el error saliera media hora después.
    { printf 'USE [%s];
GO
' "$BASE"; cat "$guion"; } |       MSYS_NO_PATHCONV=1 sqlcmd -S "$SERVIDOR" -U "$USUARIO" -P "$CLAVE" -C -I -b >/dev/null 2>&1       && puestos=$((puestos+1))       || echo "AVISO: no se pudo reponer $(basename "$guion")"
  done

  echo "Procedimientos nuestros repuestos en $BASE: $puestos de ${#nuestros[@]}"
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
  reponer) reponer_procedimientos_nuestros ;;
  tomar)  mkdir -p "$CANDADOS"; : > "$CANDADOS/instantanea-en-uso-${MI_CANDADO:-anonimo}"; echo "candado tomado" ;;
  soltar-candado) rm -f "$CANDADOS/instantanea-en-uso-${MI_CANDADO:-anonimo}"; echo "candado soltado" ;;
  *) sed -n '2,40p' "$0" ; exit 2 ;;
esac
