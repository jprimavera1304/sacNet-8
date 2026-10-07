#!/usr/bin/env bash
# Las escrituras de la ola 5 contra LEGACY. Mismas operaciones, mismo orden y
# mismos valores que ola5-escribir-nuevo.sh.
set -u
L=http://localhost:63479
TOK="$(cat /tmp/legacy.token)"
CORREO=app_web@tauro.com
OUT="$1"
: > "$OUT"

llamar() {   # llamar <etiqueta> <metodo> <ruta> <json-sin-token>
  local etiqueta="$1" metodo="$2" ruta="$3" cuerpo="$4"
  local completo
  completo="$(printf '%s' "$cuerpo" | sed "s|^{|{\"token\":\"$TOK\",\"correoElectronicoToken\":\"$CORREO\",|")"
  printf '### %s\n' "$etiqueta" >> "$OUT"
  curl -s -m 60 -X "$metodo" "$L$ruta" \
       -H "Content-Type: application/json" -H "Authorization: Bearer $TOK" \
       -d "$completo" >> "$OUT"
  printf '\n' >> "$OUT"
}

# --- Altas de los catalogos de obra ---
llamar tipoobra.alta           POST /api/mctiposobra            '{"TipoObra":"OLA5 PRUEBA OBRA","IDUsuario":0,"Equipo":"OLA5"}'
llamar tipocontratacion.alta   POST /api/mctiposcontratacion    '{"TipoContratacion":"OLA5 PRUEBA CONTRATACION","IDUsuario":0,"Equipo":"OLA5"}'
llamar tipodocumento.alta      POST /api/mctiposdocumento       '{"TipoDocumento":"OLA5 PRUEBA DOCUMENTO","IDUsuario":0,"Equipo":"OLA5"}'
llamar tiporecurso.alta        POST /api/mctiposrecurso         '{"TipoRecurso":"OLA5 PRUEBA RECURSO","IDUsuario":0,"Equipo":"OLA5"}'
llamar puesto.alta             POST /api/mcpuestos              '{"Puesto":"OLA5 PRUEBA PUESTO","EsRepresentanteLegal":1,"EsRepresentanteTecnico":0,"IDUsuario":0,"Equipo":"OLA5"}'
llamar unidadmedida.alta       POST /api/mcunidadesmedida       '{"UnidadMedida":"OLA5 PZA","SoloUnidades":1,"IDUsuario":0,"Equipo":"OLA5"}'
llamar residente.alta          POST /api/mcresidentes           '{"IDPuesto":1,"Residente":"OLA5 PRUEBA RESIDENTE","EsSupervision":0,"EsObra":1,"IDUsuario":0,"Equipo":"OLA5"}'

# --- Modificaciones (sobre renglones que ya existian en la instantanea) ---
llamar tipoobra.cambio         PUT  /api/mctiposobra            '{"IDTipoObra":1,"IDStatus":1,"TipoObra":"OLA5 OBRA CAMBIADA","IDUsuario":0,"Equipo":"OLA5"}'
llamar tipoobra.baja           PUT  /api/mctiposobra            '{"IDTipoObra":2,"IDStatus":2,"TipoObra":"OLA5 OBRA DE BAJA","IDUsuario":0,"Equipo":"OLA5"}'
llamar puesto.cambio           PUT  /api/mcpuestos              '{"IDPuesto":1,"IDStatus":1,"Puesto":"OLA5 PUESTO CAMBIADO","EsRepresentanteLegal":0,"EsRepresentanteTecnico":1,"IDUsuario":0,"Equipo":"OLA5"}'
llamar unidadmedida.cambio     PUT  /api/mcunidadesmedida       '{"IDUnidadMedida":1,"IDStatus":1,"UnidadMedida":"OLA5 UNIDAD CAMBIADA","SoloUnidades":0,"IDUsuario":0,"Equipo":"OLA5"}'
llamar tipocontratacion.cambio PUT  /api/mctiposcontratacion    '{"IDTipoContratacion":1,"IDStatus":1,"TipoContratacion":"OLA5 CONTRATACION CAMBIADA","IDUsuario":0,"Equipo":"OLA5"}'
llamar tipodocumento.cambio    PUT  /api/mctiposdocumento       '{"IDTipoDocumento":1,"IDStatus":1,"TipoDocumento":"OLA5 DOCUMENTO CAMBIADO","IDUsuario":0,"Equipo":"OLA5"}'
llamar tiporecurso.cambio      PUT  /api/mctiposrecurso         '{"IDTipoRecurso":1,"IDStatus":1,"TipoRecurso":"OLA5 RECURSO CAMBIADO","IDUsuario":0,"Equipo":"OLA5"}'

# --- Grupos del catalogo de conceptos de un contrato ---
llamar grupo.alta1             POST /api/mccontratos/grupos     '{"IDContrato":1,"Clave":"1","Grupo":"OLA5 GRUPO UNO","IDUsuario":0,"Equipo":"OLA5"}'
llamar grupo.alta2             POST /api/mccontratos/grupos     '{"IDContrato":1,"Clave":"2","Grupo":"OLA5 GRUPO DOS","IDUsuario":0,"Equipo":"OLA5"}'
llamar grupo.cambio            PUT  /api/mccontratos/grupos     '{"IDContratoGrupo":1,"IDContrato":1,"IDStatus":1,"Clave":"1A","Grupo":"OLA5 GRUPO UNO CAMBIADO","IDUsuario":0,"Equipo":"OLA5"}'
llamar grupo.bajar             PUT  /api/mccontratos/grupos/bajar  '{"IDContratoGrupo":1,"IDContrato":1,"Orden":1,"IDUsuario":0,"Equipo":"OLA5"}'
llamar grupo.subir             PUT  /api/mccontratos/grupos/subir  '{"IDContratoGrupo":1,"IDContrato":1,"Orden":1,"IDUsuario":0,"Equipo":"OLA5"}'

# Cerrar/reabrir contrato y las gasolinas van en la SEGUNDA tanda:
# el controlador de Legacy exige IDUsuario != 0 en esas rutas y en esta base
# ningun token puede llevar un IDUsuario distinto de 0.

echo "listo: $OUT"
