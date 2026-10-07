"""Compara ESCRITURAS entre Legacy y el backend nuevo, con el ciclo de instantanea.

EL CICLO, Y POR QUE ES ASI
--------------------------
Una escritura no se puede comparar como una consulta: la primera llamada cambia
la base, asi que la segunda ya no arranca del mismo sitio y lo que salga no
significa nada. Por eso:

    volver  ->  foto  ->  LEGACY   ->  foto  ->  anotar el delta
    volver  ->  foto  ->  NUEVO    ->  foto  ->  anotar el delta
    volver

Los dos lados salen EXACTAMENTE del mismo estado, y lo que se compara son DOS
cosas: la respuesta HTTP y el DELTA EN LA BASE. La segunda es la que importa.

LA FOTO
-------
Un SELECT por cada tabla que la operacion toca, con COUNT, SUM de los importes y
MAX del id. Lo que se compara es la DIFERENCIA entre la foto de antes y la de
despues, no la foto entera: asi no estorba lo que ya habia en la base.

LA FIRMA DEL USUARIO
--------------------
El backend nuevo escribe el IDUsuario del TOKEN; el usuario del banco de pruebas
no esta ligado a Mac31, asi que escribe 0. Legacy lo recibe en el cuerpo y varios
de sus controladores RECHAZAN el 0. Donde pasa eso, se le manda a legacy un id
real y la comparacion del delta EXCLUYE las columnas de firma (IDUsuario,
Equipo, y sus variantes de cancelacion), que es la unica diferencia esperada.
Queda anotado caso por caso.
"""
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request

SC = os.path.dirname(os.path.abspath(__file__))
COMPARADOR = r"D:\JuanD\GitHub\MAC\sacNet-8\migracion\comparador"

LEGACY = "http://localhost:63479"
NUEVO = "http://127.0.0.1:5081"
CORREO = "app_web@tauro.com"

SERVIDOR = "LAP-JUAND"
BASE = "Produccion_svr"
USUARIO = "sa"
CLAVE = "123"

legacy_token = open(os.path.join(SC, "legacy.token")).read().strip()
nuevo_token = open(os.path.join(SC, "nuevo.token")).read().strip()


# ---------------------------------------------------------------- base


def sql(consulta):
    r = subprocess.run(
        ["sqlcmd", "-S", SERVIDOR, "-d", BASE, "-U", USUARIO, "-P", CLAVE,
         "-C", "-I", "-W", "-h", "-1", "-Q", "SET NOCOUNT ON; " + consulta],
        capture_output=True, text=True, timeout=180,
        env=dict(os.environ, MSYS_NO_PATHCONV="1"))
    return (r.stdout or "") + (r.stderr or "")


def foto(consultas):
    """Una foto de la base: un renglon de numeros por cada consulta pedida.

    UNA FOTO QUE FALLA NO ES UNA FOTO.

    Si la consulta trae un error de SQL —un nombre de columna equivocado, por
    ejemplo— el texto del error es el MISMO antes y despues, asi que el delta sale
    vacio y los dos lados parecen coincidir. Es la peor forma de equivocarse aqui:
    da verde sin haber mirado nada. Asi que se revienta.
    """
    salida = {}
    for nombre, consulta in consultas.items():
        texto = sql(consulta).strip()
        if "Msg " in texto or "Invalid" in texto or "Login failed" in texto:
            raise RuntimeError(f"la foto '{nombre}' no se pudo tomar: {texto[:300]}")
        salida[nombre] = texto
    return salida


def delta(antes, despues):
    """Que cambio entre las dos fotos."""
    cambios = {}
    for nombre in antes:
        if antes[nombre] != despues.get(nombre):
            cambios[nombre] = {"antes": antes[nombre], "despues": despues.get(nombre)}
    return cambios


def instantanea(accion):
    r = subprocess.run(["bash", "instantanea.sh", accion], cwd=COMPARADOR,
                       capture_output=True, text=True, timeout=600)
    return (r.stdout or "") + (r.stderr or "")


def fecha_operacion():
    for linea in sql("SELECT CONVERT(varchar(10), FechaOperacion, 120) FROM dbo.Constantes;").splitlines():
        t = linea.strip()
        if len(t) == 10 and t[4] == "-":
            return t
    return "?"


# ---------------------------------------------------------------- http


def pedir(url, metodo, cuerpo, token):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(url, data=datos, method=metodo)
    req.add_header("Content-Type", "application/json")
    req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            return r.status, r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
    except Exception as e:
        return 0, json.dumps({"_error_de_red": str(e)})


def llamar_legacy(caso):
    """Llama a legacy, reintentando el fallo TRANSITORIO de conexion.

    Justo despues de `instantanea.sh volver`, SQL Server acaba de salir de
    SINGLE_USER y la primera conexion del pool de legacy puede fallar con
    "Login failed for user 'sa'". Eso no es un resultado: es la base
    reconectandose. Si no se reintenta, el caso sale rojo por un motivo que no
    tiene nada que ver con el codigo que se esta comparando — y es de los rojos
    que se persiguen durante horas.

    OJO: solo se reintenta cuando NO hubo operacion. Un reintento de una escritura
    que SI llego la haria dos veces.
    """
    cuerpo = dict(caso["legacy"].get("body") or {})
    cuerpo.setdefault("token", legacy_token)
    cuerpo.setdefault("correoElectronicoToken", CORREO)

    for intento in range(4):
        est, resp = pedir(LEGACY + caso["legacy"]["path"], caso["legacy"].get("method", "POST"),
                          cuerpo, legacy_token)
        if "Login failed for user" not in resp and "_error_de_red" not in resp:
            return est, resp
        import time
        time.sleep(3)
    return est, resp


def llamar_nuevo(caso):
    return pedir(NUEVO + caso["nuevo"]["path"], caso["nuevo"].get("method", "POST"),
                 caso["nuevo"].get("body"), nuevo_token)


# ---------------------------------------------------------------- ciclo


def correr(caso):
    nombre = caso["nombre"]
    consultas = caso["fotos"]

    instantanea("volver")
    antes_leg = foto(consultas)
    est_leg, resp_leg = llamar_legacy(caso)
    despues_leg = foto(consultas)
    delta_leg = delta(antes_leg, despues_leg)

    instantanea("volver")
    antes_nue = foto(consultas)
    est_nue, resp_nue = llamar_nuevo(caso)
    despues_nue = foto(consultas)
    delta_nue = delta(antes_nue, despues_nue)

    iguales = delta_leg == delta_nue

    return {
        "nombre": nombre,
        "legacy": {"status": est_leg, "cuerpo": resp_leg[:900], "delta": delta_leg},
        "nuevo": {"status": est_nue, "cuerpo": resp_nue[:900], "delta": delta_nue},
        "mismo_efecto_en_base": iguales,
        "notas": caso.get("notas", []),
    }


def main(ruta_casos, salida):
    fecha_inicio = fecha_operacion()
    print("fecha de operacion al empezar:", fecha_inicio)

    casos = json.load(open(ruta_casos, encoding="utf-8"))
    resultados = []
    for caso in casos:
        print("---", caso["nombre"])
        r = correr(caso)
        resultados.append(r)
        print("    legacy", r["legacy"]["status"], "delta", len(r["legacy"]["delta"]),
              "| nuevo", r["nuevo"]["status"], "delta", len(r["nuevo"]["delta"]),
              "|", "MISMO EFECTO" if r["mismo_efecto_en_base"] else "DISTINTO")

    instantanea("volver")
    fecha_fin = fecha_operacion()
    print("fecha de operacion al terminar:", fecha_fin)
    if fecha_inicio != fecha_fin:
        print("!!! LA FECHA DE OPERACION SE MOVIO: los resultados NO valen.")

    json.dump({"fecha_inicio": fecha_inicio, "fecha_fin": fecha_fin, "casos": resultados},
              open(salida, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    iguales = sum(1 for r in resultados if r["mismo_efecto_en_base"])
    print(f"\n{iguales} con el mismo efecto en base, "
          f"{len(resultados) - iguales} distintos, {len(resultados)} casos")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
