#!/usr/bin/env python
"""
Vuelve a decidir qué capacidades ESCRIBEN, leyendo los procedimientos de verdad.

Por qué hace falta: la primera clasificación se dedujo del nombre del stored
procedure (`sp_n_Consulta*` lee, `sp_n_Actualizar*` escribe). Casi siempre
acierta, pero no siempre, y cuando falla es peligroso:

    /api/empleadosprestamo/validar  ->  parecía una consulta por el nombre
                                        y `sp_n_ValidaEmpleadosPrestamos`
                                        abre BEGIN TRAN, INSERTA en
                                        EmpleadoPrestamosPagos y ACTUALIZA
                                        EmpleadoPrestamos.

Es el batch que descuenta los préstamos de la nómina. Un agente al que se le
diga "estos son de solo lectura, pruébalos" lo habría ejecutado contra la base.

Así que el nombre no decide: decide el cuerpo del procedimiento. Esto lo lee de
`sys.sql_modules` y busca lo que de verdad modifica datos.

Uso:
    python clasificar-escrituras.py            # informa, no cambia nada
    python clasificar-escrituras.py --aplicar  # corrige registro.tsv
"""

import csv
import os
import re
import subprocess
import sys

RAIZ = os.path.dirname(os.path.abspath(__file__))

SERVIDOR = os.environ.get("MAC_SQL_SERVIDOR", "LAP-JUAND")
BASE = os.environ.get("MAC_SQL_BASE", "Produccion_svr")
USUARIO = os.environ.get("MAC_SQL_USUARIO", "sa")
CLAVE = os.environ.get("MAC_SQL_CLAVE", "123")

# Lo que de verdad cambia datos. `SELECT ... INTO #tmp` NO cuenta: una tabla
# temporal es del scope de la llamada y desaparece. Tampoco cuentan las
# variables de tabla.
ESCRITURA = re.compile(
    r"""(?ix)
    \b(
        insert \s+ (?!into \s+ [\#@])        # INSERT que no sea a #temp ni @var
      | update \s+ (?![\#@])
      | delete \s+ from \s+ (?![\#@])
      | delete \s+ (?![\#@f])                # DELETE tabla  (no #temp, no FROM)
      | merge \s+ (?![\#@])
      | truncate \s+ table
      | drop \s+ table \s+ (?![\#@])
      | exec(ute)? \s+ sp_executesql          # dinámico: puede escribir
    )\b
    """,
)

# Un SELECT ... INTO que no sea a temporal también crea tabla.
SELECT_INTO = re.compile(r"(?i)\bselect\b[^;]*?\binto\s+(?![\#@])")


def sql(consulta):
    # -y 0 da el texto completo del procedimiento sin recortar. No se combina
    # con -W (sqlcmd los rechaza juntos), asi que el recorte se hace aqui.
    cmd = ["sqlcmd", "-S", SERVIDOR, "-d", BASE, "-U", USUARIO, "-P", CLAVE,
           "-C", "-I", "-y", "0", "-Q", consulta]
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    r = subprocess.run(cmd, capture_output=True, text=True, env=env,
                       encoding="utf-8", errors="replace")
    return r.stdout or ""


def cuerpo_de(sp):
    """El texto del procedimiento, o None si no existe en esta base."""
    salida = sql(
        "SET NOCOUNT ON; SELECT ISNULL(OBJECT_DEFINITION(OBJECT_ID('{}')),'@@NO@@')"
        .format(sp.replace("'", "''")))
    txt = salida.strip()
    if not txt or "@@NO@@" in txt:
        return None
    return txt


def marcas_de_escritura():
    """Para cada procedimiento, si modifica datos. Se decide DENTRO de SQL.

    El primer intento traia el texto completo de los ~600 procedimientos por
    sqlcmd y lo analizaba aqui; no sirvio, porque sqlcmd parte y recorta las
    lineas largas y el resultado llegaba destrozado. Preguntarlos de uno en uno
    si funciona pero son ~600 viajes.

    Asi que la deteccion se hace donde vive el texto. Es mas tosca que un
    analisis de verdad -un INSERT dentro de un comentario cuenta- y eso es
    DELIBERADO: equivocarse diciendo "escribe" cuando solo lee hace que alguien
    pruebe con mas cuidado del necesario; equivocarse al reves hace que alguien
    ejecute contra la base algo que creia inofensivo. El error barato es el
    primero.
    """
    consulta = (
        "SET NOCOUNT ON; "
        "SELECT p.name + '|' + CAST(CASE WHEN "
        "   m.definition LIKE '%INSERT %' OR m.definition LIKE '%UPDATE %' OR "
        "   m.definition LIKE '%DELETE %' OR m.definition LIKE '%MERGE %' OR "
        "   m.definition LIKE '%TRUNCATE %' OR m.definition LIKE '%sp_executesql%' "
        "THEN 1 ELSE 0 END AS char(1)) "
        "FROM sys.sql_modules m JOIN sys.procedures p ON p.object_id = m.object_id;")
    salida = sql(consulta)
    marcas = {}
    for linea in salida.splitlines():
        linea = linea.strip()
        if "|" not in linea or linea.startswith("-"):
            continue
        nombre, _, flag = linea.rpartition("|")
        flag = flag.strip()
        if flag in ("0", "1"):
            marcas[nombre.strip()] = (flag == "1")
    return marcas


def sin_comentarios(txt):
    txt = re.sub(r"/\*.*?\*/", " ", txt, flags=re.S)
    txt = re.sub(r"--[^\n]*", " ", txt)
    return txt


def escribe(cuerpo):
    limpio = sin_comentarios(cuerpo)
    return bool(ESCRITURA.search(limpio) or SELECT_INTO.search(limpio))


def main(argv):
    aplicar = "--aplicar" in argv
    reg = os.path.join(RAIZ, "registro.tsv")
    with open(reg, "r", encoding="utf-8", newline="") as fh:
        lector = csv.DictReader(fh, delimiter="\t")
        columnas = lector.fieldnames
        filas = list(lector)

    # Se piden UNO POR UNO a proposito.
    #
    # Traerlos todos en un SELECT es mas rapido pero sqlcmd parte y recorta las
    # lineas largas, y el texto llega destrozado. Y detectarlo dentro de SQL con
    # LIKE es robusto de transportar pero demasiado tosco: marcaba 138 lecturas
    # como escrituras -cualquier INSERT a una tabla temporal o dentro de un
    # comentario cuenta-, y una señal que se equivoca tanto no la mira nadie.
    #
    # Son ~600 viajes y un par de minutos. Vale la pena: lo que sale de aqui
    # decide si alguien puede ejecutar algo contra la base creyendo que solo
    # lee.
    print("Leyendo los procedimientos de {}, uno por uno...".format(BASE))
    cache = {}
    cambios, ausentes = [], set()

    for f in filas:
        sps = [s.strip() for s in (f["stored_procedures"] or "").split(";")
               if s.strip().startswith("sp_")]
        if not sps:
            continue

        veredicto = False
        desconocido = False
        culpable = ""
        for sp in sps:
            if sp not in cache:
                c = cuerpo_de(sp)
                cache[sp] = escribe(c) if c is not None else None
            if cache[sp] is None:
                desconocido = True
                ausentes.add(sp)
            elif cache[sp]:
                veredicto = True
                culpable = sp

        if desconocido and not veredicto:
            nuevo = "?"
        else:
            nuevo = "si" if veredicto else "no"

        if nuevo != (f["escribe"] or "").strip():
            cambios.append((f["ruta_origen"], f["escribe"], nuevo, culpable))
            f["escribe"] = nuevo

    print("{} capacidades con la clasificacion corregida".format(len(cambios)))
    peligrosas = [c for c in cambios if c[1] == "no" and c[2] == "si"]
    if peligrosas:
        print("\nLAS QUE DECIAN LEER Y EN REALIDAD ESCRIBEN ({}):".format(len(peligrosas)))
        for ruta, antes, ahora, sp in peligrosas:
            print("   {:<55} por {}".format(ruta, sp))
    otras = [c for c in cambios if not (c[1] == "no" and c[2] == "si")]
    if otras:
        print("\nOtros ajustes ({}):".format(len(otras)))
        for ruta, antes, ahora, sp in otras[:15]:
            print("   {:<55} {} -> {}".format(ruta, antes, ahora))
    if ausentes:
        print("\n{} procedimientos no existen en {} (quedan como '?')".format(
            len(ausentes), BASE))

    if aplicar:
        with open(reg, "w", encoding="utf-8", newline="") as fh:
            w = csv.DictWriter(fh, fieldnames=columnas, delimiter="\t",
                               lineterminator="\n")
            w.writeheader()
            w.writerows(filas)
        print("\nregistro.tsv actualizado.")
    else:
        print("\n(Solo informe. Usa --aplicar para corregir el registro.)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
