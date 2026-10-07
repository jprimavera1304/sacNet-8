#!/usr/bin/env bash
# Segunda tanda contra LEGACY: cierre de contrato y gasolinas.
# IDUsuario = 3 porque el controlador de Legacy RECHAZA el 0 en estos endpoints.
set -u
L=http://localhost:63479
TOK="$(cat /tmp/legacy.token)"
CORREO=app_web@tauro.com
OUT="$1"
: > "$OUT"

llamar() {
  local etiqueta="$1" metodo="$2" ruta="$3" cuerpo="$4"
  local completo
  completo="$(printf '%s' "$cuerpo" | sed "s|^{|{\"token\":\"$TOK\",\"correoElectronicoToken\":\"$CORREO\",|")"
  printf '### %s\n' "$etiqueta" >> "$OUT"
  curl -s -m 60 -X "$metodo" "$L$ruta" -H "Content-Type: application/json" -H "Authorization: Bearer $TOK" -d "$completo" >> "$OUT"
  printf '\n' >> "$OUT"
}

llamar contrato.cerrar   PUT  /api/mccontratos/status '{"IDContrato":1,"ContratoCerrado":1,"IDUsuario":3,"Equipo":"OLA5"}'
llamar contrato.reabrir  PUT  /api/mccontratos/status '{"IDContrato":1,"ContratoCerrado":0,"IDUsuario":3,"Equipo":"OLA5"}'
llamar gasolina.alta     POST /api/gasolinas '{"IDAuto":3,"IDRepartidor":5072,"FechaRecibo":"01-05-2026","NumeroRecibo":"99990001","KmInicial":1000,"KmFinal":1500,"PrecioLitro":23.50,"TotalRecibo":940.00,"IDUsuario":3,"Equipo":"OLA5"}'
llamar gasolina.cambio   PUT  /api/gasolinas '{"IDGasolina":37,"IDAuto":3,"IDRepartidor":5072,"FechaRecibo":"02-05-2026","NumeroRecibo":"99990002","KmInicial":2000,"KmFinal":2600,"PrecioLitro":24.10,"TotalRecibo":1205.00,"IDUsuario":3,"Equipo":"OLA5"}'
llamar gasolina.cancelar POST /api/gasolinas/cancelar '{"IDsGasolina":"38","NumerosRecibo":"99990003","IDUsuario":3,"Equipo":"OLA5"}'

echo "listo: $OUT"
