#!/usr/bin/env python
"""
Arma `registro.tsv`, el tablero de la migracion.

Cruza el inventario de Legacy contra lo que ya invoca el backend nuevo y le
pone a cada endpoint una clasificacion INICIAL. Es un punto de partida, no un
veredicto: lo que decide de verdad es el comparador.

Las clasificaciones iniciales:

  falta            ninguno de sus stored procedures lo toca el backend nuevo
  parcial          algunos si, otros no -> casi siempre esta incompleto
  por-verificar    todos sus SP ya se usan en el nuevo, pero NADIE ha
                   comprobado que devuelva lo mismo. No es "ya esta".
  sin-sp           no ejecuta ningun SP (util, pero se revisa aparte)
  dinamico         el SP sale de la tabla Reportes, no del codigo

Se vuelve a correr cuando cambia el inventario; nunca se edita a mano la parte
calculada.
"""

import csv
import os
import sys

RAIZ = os.path.dirname(os.path.abspath(__file__))


def leer_lista(nombre):
    ruta = os.path.join(RAIZ, nombre)
    if not os.path.exists(ruta):
        return set()
    with open(ruta, "r", encoding="utf-8") as fh:
        return {l.strip() for l in fh if l.strip()}


def sps_de(celda):
    """La columna trae varios SP separados por `;`."""
    fuera = []
    for trozo in (celda or "").replace(",", ";").split(";"):
        t = trozo.strip()
        if t.startswith("sp_"):
            fuera.append(t)
    return fuera


def main():
    cubiertos = leer_lista("sp-del-nuevo.txt")
    if not cubiertos:
        print("Falta sp-del-nuevo.txt. Generalo primero.")
        return 2

    entrada = os.path.join(RAIZ, "inventario-legacy.tsv")
    salida = os.path.join(RAIZ, "registro.tsv")

    columnas = [
        "capacidad", "origen", "ruta_origen", "verbo", "stored_procedures",
        "escribe", "sp_cubiertos", "sp_faltantes", "estado",
        "ubicacion_nueva", "metodo_validacion", "resultado",
        "diferencias_intencionales", "observaciones",
    ]

    filas = []
    with open(entrada, "r", encoding="utf-8", newline="") as fh:
        for r in csv.DictReader(fh, delimiter="\t"):
            sps = sps_de(r.get("stored_procedures"))
            dinamico = "dinamico" in (r.get("stored_procedures") or "")

            si = [s for s in sps if s in cubiertos]
            no = [s for s in sps if s not in cubiertos]

            if dinamico:
                estado = "dinamico"
            elif not sps:
                estado = "sin-sp"
            elif not no:
                estado = "por-verificar"
            elif si:
                estado = "parcial"
            else:
                estado = "falta"

            filas.append({
                "capacidad": r.get("capacidad", ""),
                "origen": "MacServicios2/" + (r.get("controlador") or ""),
                "ruta_origen": r.get("ruta_http", ""),
                "verbo": r.get("verbo", ""),
                "stored_procedures": ";".join(sps),
                "escribe": r.get("escribe", ""),
                "sp_cubiertos": ";".join(si),
                "sp_faltantes": ";".join(no),
                "estado": estado,
                "ubicacion_nueva": "",
                "metodo_validacion": "",
                "resultado": "",
                "diferencias_intencionales": "",
                "observaciones": r.get("notas", "")[:300],
            })

    with open(salida, "w", encoding="utf-8", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=columnas, delimiter="\t",
                           lineterminator="\n")
        w.writeheader()
        w.writerows(filas)

    cuenta = {}
    escriben = {}
    for f in filas:
        cuenta[f["estado"]] = cuenta.get(f["estado"], 0) + 1
        if f["escribe"] == "si":
            escriben[f["estado"]] = escriben.get(f["estado"], 0) + 1

    print("registro.tsv: {} capacidades".format(len(filas)))
    for estado in ("por-verificar", "parcial", "falta", "sin-sp", "dinamico"):
        if estado in cuenta:
            print("  {:<14} {:>4}   (escriben {})".format(
                estado, cuenta[estado], escriben.get(estado, 0)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
