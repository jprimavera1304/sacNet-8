#!/usr/bin/env python
"""
Pasa al registro lo que una ola dejó hecho.

El registro lo calcula `construir-registro.py` cruzando inventarios, pero el
resultado del trabajo real -qué quedó migrado, dónde, y cómo se comprobó- vive
en el reporte de cada ola. Esto lo lee de ahí en vez de que alguien lo teclee,
para que no haya dos verdades.

Lee las tablas markdown de `ola-*.md` con las columnas
`capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas`
y actualiza las filas del registro que coincidan por ruta legacy.

Uso:
    python actualizar-registro.py ola-1-catalogos.md [ola-0-lecturas.md ...]
"""

import csv
import os
import re
import sys

RAIZ = os.path.dirname(os.path.abspath(__file__))

# Cómo se traduce el veredicto de la ola al estado del registro.
VEREDICTOS = {
    "OK": ("hecho", "comparado contra Legacy"),
    "COINCIDE": ("hecho", "comparado contra Legacy"),
    "CORREGIDO": ("hecho", "comparado contra Legacy (se corrigio un fallo)"),
    "INTENCIONAL": ("hecho", "comparado; diferencia a proposito"),
    "NO VERIFICABLE": ("hecho-sin-comparar", "Legacy no responde"),
    "FALTA": ("falta", ""),
}


def limpiar(celda):
    """Quita el formato markdown: `codigo`, **negrita**, espacios."""
    t = (celda or "").strip()
    t = re.sub(r"`([^`]*)`", r"\1", t)
    t = re.sub(r"\*\*([^*]*)\*\*", r"\1", t)
    return t.strip()


def ruta_sola(celda):
    """`POST /api/catestados/consultar` -> `/api/catestados/consultar`."""
    t = limpiar(celda)
    m = re.search(r"(/api/\S+)", t)
    return m.group(1) if m else ""


def filas_de_ola(ruta):
    """Saca las filas de la tabla que tiene la cabecera que nos interesa."""
    fuera = []
    dentro = False
    with open(ruta, "r", encoding="utf-8") as fh:
        for linea in fh:
            if not linea.lstrip().startswith("|"):
                dentro = False
                continue
            celdas = [c for c in linea.strip().strip("|").split("|")]
            cab = limpiar(celdas[0]).lower()
            if cab == "capacidad" and len(celdas) >= 5:
                dentro = True
                continue
            if set(limpiar(celdas[0])) <= set("-: "):
                continue
            if dentro and len(celdas) >= 5:
                fuera.append({
                    "capacidad": limpiar(celdas[0]),
                    "legacy": ruta_sola(celdas[1]),
                    "nueva": limpiar(celdas[2]),
                    "permiso": limpiar(celdas[3]),
                    "veredicto": limpiar(celdas[4]).upper(),
                    "notas": limpiar(celdas[5]) if len(celdas) > 5 else "",
                })
    return fuera


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2

    reg = os.path.join(RAIZ, "registro.tsv")
    with open(reg, "r", encoding="utf-8", newline="") as fh:
        lector = csv.DictReader(fh, delimiter="\t")
        columnas = lector.fieldnames
        filas = list(lector)

    # Indice por ruta legacy, que es la llave estable entre los dos documentos.
    por_ruta = {}
    for f in filas:
        por_ruta.setdefault(f["ruta_origen"].strip().lower(), []).append(f)

    tocadas, sin_pareja = 0, []
    for nombre in argv[1:]:
        ruta_ola = nombre if os.path.isabs(nombre) else os.path.join(RAIZ, nombre)
        if not os.path.exists(ruta_ola):
            print("No existe {}".format(ruta_ola))
            continue
        for item in filas_de_ola(ruta_ola):
            destino = por_ruta.get(item["legacy"].lower())
            if not destino:
                if item["legacy"]:
                    sin_pareja.append(item["legacy"])
                continue
            estado, metodo = VEREDICTOS.get(item["veredicto"], ("", ""))
            for f in destino:
                if estado:
                    f["estado"] = estado
                f["ubicacion_nueva"] = item["nueva"]
                f["metodo_validacion"] = metodo
                f["resultado"] = item["veredicto"]
                if item["notas"]:
                    f["observaciones"] = item["notas"][:300]
                tocadas += 1

    with open(reg, "w", encoding="utf-8", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=columnas, delimiter="\t",
                           lineterminator="\n")
        w.writeheader()
        w.writerows(filas)

    print("{} filas actualizadas".format(tocadas))
    if sin_pareja:
        print("{} rutas del reporte no estan en el registro:".format(len(sin_pareja)))
        for r in sin_pareja[:10]:
            print("   " + r)

    cuenta = {}
    for f in filas:
        cuenta[f["estado"]] = cuenta.get(f["estado"], 0) + 1
    print("\nEstado del tablero:")
    for k in sorted(cuenta, key=lambda x: -cuenta[x]):
        print("  {:<20} {:>4}".format(k, cuenta[k]))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
