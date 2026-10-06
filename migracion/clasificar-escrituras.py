#!/usr/bin/env python
"""
Decide qué capacidades ESCRIBEN en la base, leyendo los procedimientos.

De esto depende si alguien puede llamar un endpoint creyendo que solo consulta.
El caso que lo destapó:

    /api/empleadosprestamo/validar  ->  parecía una consulta por el nombre, y
                                        sp_n_ValidaEmpleadosPrestamos abre
                                        BEGIN TRAN, inserta en
                                        EmpleadoPrestamosPagos y actualiza
                                        EmpleadoPrestamos. Es el batch que
                                        descuenta los préstamos de la nómina.

Uso:
    python clasificar-escrituras.py            # informa, no cambia nada
    python clasificar-escrituras.py --aplicar  # corrige registro.tsv

CÓMO SE DECIDE, Y POR QUÉ ASÍ
-----------------------------
La detección corre DENTRO de SQL (`sql/detectar-escrituras.sql`). Costó tres
intentos llegar ahí:

1. Deducirlo del NOMBRE (`Consulta*` lee, `Actualizar*` escribe). Rápido y
   equivocado: dejó pasar el batch de préstamos.
2. Traer el texto de cada procedimiento y analizarlo en Python. Lo correcto en
   teoría, pero **sqlcmd recorta las definiciones largas**, así que el análisis
   se hacía sobre un texto incompleto. Fallaba en silencio y en la dirección
   cara: decía "no escribe" mirando solo el principio. Así se le escaparon
   cuatro procedimientos que sí escriben.
3. Detectarlo en SQL, donde vive el texto entero. Solo cruza un 0 o un 1.

La consulta descuenta las escrituras a tablas TEMPORALES, que son el ruido
principal: los `sp_n_` arman sus resultados en `#tmp`.

QUÉ SIGNIFICA CADA VALOR
------------------------
    no   comprobado: no toca ninguna tabla real. Se puede llamar tranquilo.
    si   PUEDE escribir. Trátalo como escritura.
    ?    el procedimiento no está en esta base. No es una lectura.

El `si` **sobre-reporta a propósito**: no distingue una escritura de verdad de
un INSERT que quedó dentro de un bloque `/* */` con consultas de apunte, y los
`sp_n_` tienen montones de esos arriba. Preferimos que alguien pruebe con más
cuidado del necesario a que ejecute algo que creía inofensivo.
"""

import csv
import os
import subprocess
import sys

RAIZ = os.path.dirname(os.path.abspath(__file__))

SERVIDOR = os.environ.get("MAC_SQL_SERVIDOR", "LAP-JUAND")
BASE = os.environ.get("MAC_SQL_BASE", "Produccion_svr")
USUARIO = os.environ.get("MAC_SQL_USUARIO", "sa")
CLAVE = os.environ.get("MAC_SQL_CLAVE", "123")


def marcas_de_escritura():
    """{nombre del procedimiento: escribe?} para toda la base, en un viaje."""
    guion = os.path.join(RAIZ, "sql", "detectar-escrituras.sql")
    cmd = ["sqlcmd", "-S", SERVIDOR, "-d", BASE, "-U", USUARIO, "-P", CLAVE,
           "-C", "-I", "-h", "-1", "-W", "-i", guion]
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    r = subprocess.run(cmd, capture_output=True, text=True, env=env,
                       encoding="utf-8", errors="replace")
    marcas = {}
    for linea in (r.stdout or "").splitlines():
        nombre, sep, flag = linea.strip().rpartition("|")
        if sep and flag in ("0", "1"):
            marcas[nombre] = (flag == "1")
    return marcas


def sps_de(celda):
    """La columna trae varios procedimientos separados por `;`."""
    return [t.strip() for t in (celda or "").replace(",", ";").split(";")
            if t.strip().startswith("sp_")]


def main(argv):
    aplicar = "--aplicar" in argv

    marcas = marcas_de_escritura()
    print("{} procedimientos leidos de {}".format(len(marcas), BASE))
    if len(marcas) < 100:
        print("Se leyeron muy pocos: algo salio mal, no se aplica nada.")
        return 2

    reg = os.path.join(RAIZ, "registro.tsv")
    with open(reg, "r", encoding="utf-8", newline="") as fh:
        lector = csv.DictReader(fh, delimiter="\t")
        columnas = lector.fieldnames
        filas = list(lector)

    cambios, ausentes = [], set()

    for f in filas:
        sps = sps_de(f["stored_procedures"])
        if not sps:
            continue

        veredicto, desconocido, culpable = False, False, ""
        for sp in sps:
            marca = marcas.get(sp)
            if marca is None:
                desconocido = True
                ausentes.add(sp)
            elif marca:
                veredicto = True
                culpable = culpable or sp

        nuevo = "si" if veredicto else ("?" if desconocido else "no")

        if nuevo != (f["escribe"] or "").strip():
            cambios.append((f["ruta_origen"], f["escribe"], nuevo, culpable))
            f["escribe"] = nuevo

    peligrosas = [c for c in cambios if c[1] == "no" and c[2] == "si"]
    print("{} capacidades con la clasificacion corregida".format(len(cambios)))
    if peligrosas:
        print("\nDECIAN LEER Y PUEDEN ESCRIBIR ({}):".format(len(peligrosas)))
        for ruta, _, _, sp in peligrosas[:25]:
            print("   {:<55} por {}".format(ruta, sp))
        if len(peligrosas) > 25:
            print("   ... y {} mas".format(len(peligrosas) - 25))
    if ausentes:
        print("\n{} procedimientos no estan en {} (quedan como '?')".format(
            len(ausentes), BASE))

    if aplicar:
        with open(reg, "w", encoding="utf-8", newline="") as fh:
            w = csv.DictWriter(fh, fieldnames=columnas, delimiter="\t",
                               lineterminator="\n")
            w.writeheader()
            w.writerows(filas)
        print("\nregistro.tsv actualizado.")
        cuenta = {}
        for f in filas:
            cuenta[f["escribe"]] = cuenta.get(f["escribe"], 0) + 1
        for k in sorted(cuenta):
            print("  escribe={:<3} {:>4}".format(k, cuenta[k]))
    else:
        print("\n(Solo informe. Usa --aplicar para corregir el registro.)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
