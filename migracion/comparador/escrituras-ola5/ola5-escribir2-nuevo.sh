#!/usr/bin/env bash
# Segunda tanda contra el BACKEND NUEVO: cierre de contrato y gasolinas.
# La firma NO va en el cuerpo: sale del token (y en esta base vale 0, porque
# dbo.UsuarioWeb no tiene columna que ligue con dbo.Usuarios de Mac31).
set -u
N=http://127.0.0.1:5083
TOK="$(cat /tmp/tok5083.txt)"
OUT="$1"
: > "$OUT"

llamar() {
  local etiqueta="$1" metodo="$2" ruta="$3" cuerpo="$4"
  printf '### %s\n' "$etiqueta" >> "$OUT"
  curl -s -m 60 -X "$metodo" "$N$ruta" -H "Content-Type: application/json" -H "Authorization: Bearer $TOK" -d "$cuerpo" >> "$OUT"
  printf '\n' >> "$OUT"
}

llamar contrato.cerrar   PUT  /api/obra/contratos/1/status '{"contratoCerrado":true}'
llamar contrato.reabrir  PUT  /api/obra/contratos/1/status '{"contratoCerrado":false}'
llamar gasolina.alta     POST /api/gasolinas '{"idAuto":3,"idRepartidor":5072,"fechaRecibo":"2026-05-01","numeroRecibo":"99990001","kmInicial":1000,"kmFinal":1500,"precioLitro":23.50,"totalRecibo":940.00}'
llamar gasolina.cambio   PUT  /api/gasolinas/37 '{"idAuto":3,"idRepartidor":5072,"fechaRecibo":"2026-05-02","numeroRecibo":"99990002","kmInicial":2000,"kmFinal":2600,"precioLitro":24.10,"totalRecibo":1205.00}'
llamar gasolina.cancelar PUT  /api/gasolinas/cancelar '{"idsGasolina":[38],"numerosRecibo":["99990003"]}'

echo "listo: $OUT"
