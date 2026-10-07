#!/usr/bin/env bash
# Las mismas escrituras, contra el BACKEND NUEVO en 5083.
# Mismo orden y mismos valores que ola5-escribir-legacy.sh. Lo que no va en el
# cuerpo es la firma (IDUsuario / Equipo): sale del token.
set -u
N=http://127.0.0.1:5083
TOK="$(cat /tmp/tok5083.txt)"
OUT="$1"
: > "$OUT"

llamar() {   # llamar <etiqueta> <metodo> <ruta> <json|->
  local etiqueta="$1" metodo="$2" ruta="$3" cuerpo="$4"
  printf '### %s\n' "$etiqueta" >> "$OUT"
  if [ "$cuerpo" = "-" ]; then
    curl -s -m 60 -X "$metodo" "$N$ruta" -H "Authorization: Bearer $TOK" >> "$OUT"
  else
    curl -s -m 60 -X "$metodo" "$N$ruta" \
         -H "Content-Type: application/json" -H "Authorization: Bearer $TOK" \
         -d "$cuerpo" >> "$OUT"
  fi
  printf '\n' >> "$OUT"
}

# --- Altas de los catalogos de obra ---
llamar tipoobra.alta           POST /api/obra/catalogos/tipos-obra          '{"nombre":"OLA5 PRUEBA OBRA"}'
llamar tipocontratacion.alta   POST /api/obra/catalogos/tipos-contratacion  '{"nombre":"OLA5 PRUEBA CONTRATACION"}'
llamar tipodocumento.alta      POST /api/obra/catalogos/tipos-documento     '{"nombre":"OLA5 PRUEBA DOCUMENTO"}'
llamar tiporecurso.alta        POST /api/obra/catalogos/tipos-recurso       '{"nombre":"OLA5 PRUEBA RECURSO"}'
llamar puesto.alta             POST /api/obra/catalogos/puestos             '{"puesto":"OLA5 PRUEBA PUESTO","esRepresentanteLegal":true,"esRepresentanteTecnico":false}'
llamar unidadmedida.alta       POST /api/obra/catalogos/unidades-medida     '{"unidadMedida":"OLA5 PZA","soloUnidades":true}'
llamar residente.alta          POST /api/obra/catalogos/residentes          '{"idPuesto":1,"residente":"OLA5 PRUEBA RESIDENTE","esSupervision":false,"esObra":true}'

# --- Modificaciones ---
llamar tipoobra.cambio         PUT  /api/obra/catalogos/tipos-obra/1          '{"nombre":"OLA5 OBRA CAMBIADA","idStatus":1}'
llamar tipoobra.baja           PUT  /api/obra/catalogos/tipos-obra/2          '{"nombre":"OLA5 OBRA DE BAJA","idStatus":2}'
llamar puesto.cambio           PUT  /api/obra/catalogos/puestos/1             '{"puesto":"OLA5 PUESTO CAMBIADO","esRepresentanteLegal":false,"esRepresentanteTecnico":true,"idStatus":1}'
llamar unidadmedida.cambio     PUT  /api/obra/catalogos/unidades-medida/1     '{"unidadMedida":"OLA5 UNIDAD CAMBIADA","soloUnidades":false,"idStatus":1}'
llamar tipocontratacion.cambio PUT  /api/obra/catalogos/tipos-contratacion/1  '{"nombre":"OLA5 CONTRATACION CAMBIADA","idStatus":1}'
llamar tipodocumento.cambio    PUT  /api/obra/catalogos/tipos-documento/1     '{"nombre":"OLA5 DOCUMENTO CAMBIADO","idStatus":1}'
llamar tiporecurso.cambio      PUT  /api/obra/catalogos/tipos-recurso/1       '{"nombre":"OLA5 RECURSO CAMBIADO","idStatus":1}'

# --- Grupos del catalogo de conceptos de un contrato ---
llamar grupo.alta1             POST /api/obra/contratos/grupos            '{"idContrato":1,"clave":"1","grupo":"OLA5 GRUPO UNO"}'
llamar grupo.alta2             POST /api/obra/contratos/grupos            '{"idContrato":1,"clave":"2","grupo":"OLA5 GRUPO DOS"}'
llamar grupo.cambio            PUT  /api/obra/contratos/grupos/1          '{"idContrato":1,"idStatus":1,"clave":"1A","grupo":"OLA5 GRUPO UNO CAMBIADO"}'
llamar grupo.bajar             PUT  /api/obra/contratos/grupos/1/orden    '{"idContrato":1,"direccion":"bajar"}'
llamar grupo.subir             PUT  /api/obra/contratos/grupos/1/orden    '{"idContrato":1,"direccion":"subir"}'

# Cerrar/reabrir contrato y las gasolinas van en la SEGUNDA tanda:
# el controlador de Legacy exige IDUsuario != 0 en esas rutas y en esta base
# ningun token puede llevar un IDUsuario distinto de 0.

echo "listo: $OUT"
