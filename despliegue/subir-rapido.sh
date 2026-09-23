#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# DESPLIEGUE RAPIDO AL SERVIDOR DE CONTABO
#
#   ./subir-rapido.sh                -> las tres empresas
#   ./subir-rapido.sh tauro          -> solo una
#
# Es la via de "sube rapido": compila, sube y reinicia, sin pasar por GitHub.
# Toma ~40 s contra los ~3 min de Actions, que gasta casi todo el tiempo en la
# fila, el clonado y la instalacion de dependencias.
#
# LO QUE NO HACE, Y HAY QUE SABERLO: no deja registro de que commit quedo en
# produccion. Para eso esta el camino normal (commit + PR + Actions). Esta via
# es para iterar, no para cerrar.
# ---------------------------------------------------------------------------
set -euo pipefail

SERVIDOR="mac"
PROYECTO="$(dirname "$0")/../ISL_Service/ISL_Service/ISL_Service.csproj"
SITIOS=("tauro" "zaragoza" "sac")
[ $# -gt 0 ] && SITIOS=("$@")

TMP="/c/tmp/publish-rapido"
rm -rf "$TMP"

echo "==> compilando"
dotnet publish "$PROYECTO" -c Release -o "$TMP" --nologo -v q

echo "==> empaquetando"
( cd "$TMP" && tar -czf /c/tmp/rapido.tgz . )

echo "==> subiendo ($(du -h /c/tmp/rapido.tgz | cut -f1))"
scp -q /c/tmp/rapido.tgz "$SERVIDOR:C:/inst/rapido.tgz"

# El despliegue corre EN el servidor, con su propio script: ahi vive la parte
# delicada (apagar la aplicacion para que suelte los archivos) y conviene que
# este versionada y no incrustada en una linea de ssh.
echo "==> desplegando en: ${SITIOS[*]}"
# Un solo argumento separado por comas, que el script de alla parte. Pasarlos
# separados por espacios se veia bien y solo llegaba el PRIMERO: los otros dos
# se perdian y el despliegue quedaba a medias sin que nada protestara.
LISTA=$(IFS=,; echo "${SITIOS[*]}")
if ! ssh "$SERVIDOR" "powershell -NoProfile -ExecutionPolicy Bypass -File C:\desplegar.ps1 -Sitios $LISTA"; then
  echo
  echo "!! FALLO ALGUN SITIO. La linea de arriba dice cual y por que."
  echo "!! Los que no aparecen ahi SI quedaron actualizados."
  exit 1
fi

# La comprobacion va contra la IP del servidor con --resolve, no por DNS. Asi
# se comprueba EL SITIO QUE SE ACABA DE TOCAR aunque su nombre todavia apunte a
# otro lado — que es justo el caso de los que aun no se han mudado.
echo "==> comprobando"
for s in "${SITIOS[@]}"; do
  # sportsleague solo tiene atadura de http: su nombre sigue en Azure y no se
  # le emitio certificado. Sin distinguirlo, el curl a https no encuentra
  # atadura y el script se cae al final, despues de haber desplegado bien.
  ESQUEMA=https; PUERTO=443
  case $s in
    tauro)        H=api.mactauro.com ;;
    zaragoza)     H=api.zaragozamac.com ;;
    sac)          H=api.sacmac.net ;;
    sportsleague) H=api.integralsportsleague.net; ESQUEMA=http; PUERTO=80 ;;
    *)            echo "    $s : sin comprobacion definida"; continue ;;
  esac
  printf "    %-13s " "$s"
  curl -sk --max-time 30 --resolve "$H:$PUERTO:217.77.4.233" "$ESQUEMA://$H/dbcheck" | head -c 120
  echo
done
