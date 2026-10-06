#!/usr/bin/env python
"""
Comparador Legacy vs backend nuevo.

La idea es simple y es la que sostiene toda la migracion: se manda la MISMA
operacion a los dos backends, contra la MISMA base, y se comparan los datos.
Si coinciden, el endpoint nuevo esta bien. Si no, hay algo que investigar.

Lo que NO se compara: la cascara. Legacy envuelve en
{"id":..,"status":..,"result":{"agentes":[...]}} y el nuevo en
{"ok":..,"message":..,"data":[...]}. Esa diferencia es a proposito (ver
COMO-HABLARLE-A-LEGACY.md) y el comparador la quita antes de mirar nada.

Lo que SI se compara: los datos de adentro, campo por campo, por NOMBRE.
    {"a":1,"b":2} contra {"b":2,"a":1}  ->  iguales
    {"a":1,"b":2} contra {"a":2,"b":1}  ->  distintos, y dice en que campo

Uso:
    python comparar.py casos/catalogos-agentes.json
    python comparar.py casos/            # todos los casos de la carpeta
    python comparar.py casos/ --verbose  # ademas imprime las respuestas crudas
"""

import json
import os
import sys
import re
import unicodedata
import datetime
import decimal
import urllib.request
import urllib.error

RAIZ = os.path.dirname(os.path.abspath(__file__))
SCRATCH = os.environ.get("MAC_SCRATCH", "")

# ---------------------------------------------------------------- credenciales


def _leer(ruta, por_defecto=""):
    try:
        with open(ruta, "r", encoding="utf-8") as fh:
            return fh.read().strip()
    except OSError:
        return por_defecto


class Credenciales:
    """De donde salen los tokens de cada lado.

    Se leen de archivos para no tenerlos escritos aqui, y se pueden pisar con
    variables de entorno cuando se corre en otra maquina.
    """

    def __init__(self):
        self.legacy_base = os.environ.get("LEGACY_BASE", "http://localhost:63479")
        self.nuevo_base = os.environ.get("NUEVO_BASE", "http://127.0.0.1:5055")
        self.legacy_token = os.environ.get("LEGACY_TOKEN", "")
        self.legacy_correo = os.environ.get("LEGACY_CORREO", "app_web@tauro.com")
        self.nuevo_token = os.environ.get("NUEVO_TOKEN", "")

        if not self.legacy_token and SCRATCH:
            self.legacy_token = _leer(os.path.join(SCRATCH, "legacy.token"))
        if not self.nuevo_token and SCRATCH:
            self.nuevo_token = _leer(os.path.join(SCRATCH, "nuevo.token"))


# ---------------------------------------------------------------- normalizacion

# Lo que cambia solo entre una llamada y otra y por lo tanto nunca se compara.
# No es pereza: comparar una fecha de impresion o un folio recien generado da
# siempre "distinto" y esconde las diferencias que si importan.
CAMPOS_NO_DETERMINISTAS = re.compile(
    r"(^|_)(token|fechahora|timestamp|guid|uuid|correlation|requestid|elapsed|duracion)($|_)",
    re.IGNORECASE,
)

_FECHA_PATRONES = (
    "%Y-%m-%dT%H:%M:%S.%f", "%Y-%m-%dT%H:%M:%S", "%Y-%m-%d %H:%M:%S.%f",
    "%Y-%m-%d %H:%M:%S", "%Y-%m-%d", "%d/%m/%Y %H:%M:%S", "%d/%m/%Y %H:%M",
    "%d/%m/%Y", "%d-%m-%Y %H:%M:%S", "%d-%m-%Y",
)


def clave_normal(nombre):
    """`IDAgente`, `idAgente` y `id_agente` son el mismo campo.

    Legacy devuelve PascalCase pegado al nombre de la columna y el backend
    nuevo camelCase. Comparar por el nombre crudo daria "le falta idAgente" en
    todos los campos de todos los endpoints.
    """
    txt = unicodedata.normalize("NFKD", str(nombre))
    txt = "".join(c for c in txt if not unicodedata.combining(c))
    return re.sub(r"[^a-z0-9]", "", txt.lower())


def _como_fecha(txt):
    for patron in _FECHA_PATRONES:
        try:
            return datetime.datetime.strptime(txt, patron)
        except ValueError:
            continue
    return None


def valor_normal(valor):
    """Deja el valor en una forma comparable entre los dos backends.

    Las diferencias que se borran aqui son de REPRESENTACION, no de contenido:
    Legacy manda el dinero como texto ("1234.50") y el nuevo como numero; las
    fechas van en formatos distintos; uno manda null y el otro "". Eso no es
    una diferencia funcional y no debe gastar el tiempo de nadie.
    """
    if valor is None:
        return None
    if isinstance(valor, bool):
        return valor
    if isinstance(valor, (int, float, decimal.Decimal)):
        d = decimal.Decimal(str(valor)).normalize()
        # -0 y 0 son el mismo numero.
        return d + decimal.Decimal(0)
    if isinstance(valor, str):
        txt = valor.strip()
        if txt == "":
            return None  # "" y null se tratan igual
        bajo = txt.lower()
        if bajo in ("true", "false"):
            return bajo == "true"
        try:
            return decimal.Decimal(txt).normalize() + decimal.Decimal(0)
        except (decimal.InvalidOperation, ValueError):
            pass
        fecha = _como_fecha(txt)
        if fecha is not None:
            return fecha.replace(microsecond=0).isoformat()
        return txt
    return valor


def normalizar(nodo, mapa=None):
    """Aplica `clave_normal`/`valor_normal` a todo el arbol.

    `mapa` renombra campos del lado Legacy a como se llaman en el backend
    nuevo. Hace falta porque el contrato nuevo NO conserva los nombres de
    Legacy a proposito: `IDAgente` se llama `id`, `NombreAgente` se llama
    `nombre`. Sin el mapa, el comparador dice "falta todo" en todos los
    endpoints y no sirve para nada.
    """
    if isinstance(nodo, dict):
        salida = {}
        for k, v in nodo.items():
            if CAMPOS_NO_DETERMINISTAS.search(str(k)):
                continue
            kn = clave_normal(k)
            if mapa and kn in mapa:
                kn = mapa[kn]
            salida[kn] = normalizar(v, mapa)
        return salida
    if isinstance(nodo, list):
        return [normalizar(x, mapa) for x in nodo]
    return valor_normal(nodo)


def recortar(nodo, campos):
    """Deja solo los campos pedidos.

    Se usa cuando el contrato nuevo entrega A PROPOSITO menos informacion que
    Legacy. Entonces lo honesto no es comparar todo y perdonar lo que sobra:
    es decir exactamente que campos se prometio conservar y comparar ESOS. Lo
    que Legacy manda de mas queda anotado como diferencia intencional.
    """
    if not campos:
        return nodo
    quiero = {clave_normal(c) for c in campos}
    if isinstance(nodo, list):
        return [recortar(x, campos) for x in nodo]
    if isinstance(nodo, dict):
        return {k: v for k, v in nodo.items() if k in quiero}
    return nodo


def huella(nodo):
    """Cadena estable para poder ORDENAR listas antes de compararlas.

    Hace falta porque un stored procedure sin ORDER BY puede devolver los
    renglones en distinto orden entre una llamada y otra. Comparar por posicion
    inventaria diferencias que no existen.
    """
    return json.dumps(nodo, sort_keys=True, default=str, ensure_ascii=False)


# ---------------------------------------------------------------- desenvolver

def desenvolver_legacy(cuerpo, camino=None):
    """Quita {"id","status","result"} y, si dentro hay una sola propiedad que
    es la coleccion (`agentes`, `clientes`...), tambien la quita."""
    if camino:
        return _por_camino(cuerpo, camino)
    if not isinstance(cuerpo, dict):
        return cuerpo
    dentro = cuerpo.get("result", cuerpo)
    if isinstance(dentro, dict):
        utiles = {k: v for k, v in dentro.items()
                  if k.lower() not in ("result", "mensaje", "message", "status", "id")}
        if len(utiles) == 1:
            return next(iter(utiles.values()))
    return dentro


def desenvolver_nuevo(cuerpo, camino=None):
    """Quita {"ok","message","data"}."""
    if camino:
        return _por_camino(cuerpo, camino)
    if not isinstance(cuerpo, dict):
        return cuerpo
    dentro = cuerpo.get("data", cuerpo)
    if isinstance(dentro, dict):
        utiles = {k: v for k, v in dentro.items()
                  if k.lower() not in ("ok", "message", "mensaje")}
        if len(utiles) == 1:
            return next(iter(utiles.values()))
    return dentro


def _por_camino(cuerpo, camino):
    """`result.agentes` o `data.rows.0` -> el pedazo que se quiere comparar."""
    actual = cuerpo
    for paso in camino.split("."):
        if actual is None:
            return None
        if isinstance(actual, list):
            try:
                actual = actual[int(paso)]
            except (ValueError, IndexError):
                return None
        elif isinstance(actual, dict):
            encontrado = None
            for k in actual:
                if clave_normal(k) == clave_normal(paso):
                    encontrado = k
                    break
            if encontrado is None:
                return None
            actual = actual[encontrado]
        else:
            return None
    return actual


# ---------------------------------------------------------------- comparacion

class Diferencia:
    def __init__(self, donde, legacy, nuevo, motivo):
        self.donde = donde
        self.legacy = legacy
        self.nuevo = nuevo
        self.motivo = motivo

    def __str__(self):
        return "  {:<42} {}\n      legacy: {!r}\n      nuevo : {!r}".format(
            self.donde or "(raiz)", self.motivo, self.legacy, self.nuevo)


def comparar(izq, der, ignorar, donde="", diffs=None, ordenar_por=None):
    if diffs is None:
        diffs = []
    if _ignorado(donde, ignorar):
        return diffs

    if isinstance(izq, dict) and isinstance(der, dict):
        for k in sorted(set(izq) | set(der)):
            sub = "{}.{}".format(donde, k) if donde else k
            if _ignorado(sub, ignorar):
                continue
            if k not in izq:
                diffs.append(Diferencia(sub, "(no viene)", der[k], "solo en el nuevo"))
            elif k not in der:
                diffs.append(Diferencia(sub, izq[k], "(no viene)", "FALTA en el nuevo"))
            else:
                comparar(izq[k], der[k], ignorar, sub, diffs, ordenar_por)
        return diffs

    if isinstance(izq, list) and isinstance(der, list):
        a, b = _emparejar(izq, der, ordenar_por)
        if len(a) != len(b):
            diffs.append(Diferencia(donde, "{} renglones".format(len(a)),
                                    "{} renglones".format(len(b)),
                                    "distinto numero de renglones"))
        for i in range(min(len(a), len(b))):
            comparar(a[i], b[i], ignorar, "{}[{}]".format(donde, i), diffs, ordenar_por)
        return diffs

    if izq != der:
        diffs.append(Diferencia(donde, izq, der, "valor distinto"))
    return diffs


def _emparejar(a, b, ordenar_por):
    """Ordena las dos listas igual para que la comparacion sea por contenido."""
    if ordenar_por:
        clave = clave_normal(ordenar_por)

        def por_campo(x):
            return huella(x.get(clave)) if isinstance(x, dict) else huella(x)
        return sorted(a, key=por_campo), sorted(b, key=por_campo)
    return sorted(a, key=huella), sorted(b, key=huella)


def _ignorado(donde, ignorar):
    if not donde:
        return False
    plano = re.sub(r"\[\d+\]", "[]", donde)
    for patron in ignorar:
        p = re.sub(r"\[\d+\]", "[]", patron)
        if plano == p or plano.startswith(p + ".") or clave_normal(donde.split(".")[-1]) == clave_normal(patron):
            return True
    return False


# ---------------------------------------------------------------- llamadas

def pedir(url, metodo="GET", cuerpo=None, cabeceras=None, timeout=120):
    datos = json.dumps(cuerpo).encode("utf-8") if cuerpo is not None else None
    req = urllib.request.Request(url, data=datos, method=metodo)
    req.add_header("Content-Type", "application/json")
    for k, v in (cabeceras or {}).items():
        req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            crudo = resp.read().decode("utf-8", "replace")
            return resp.status, _json_o_texto(crudo)
    except urllib.error.HTTPError as e:
        crudo = e.read().decode("utf-8", "replace")
        return e.code, _json_o_texto(crudo)
    except Exception as e:  # red caida, backend apagado...
        return 0, {"_error_de_red": str(e)}


def _json_o_texto(crudo):
    try:
        return json.loads(crudo)
    except ValueError:
        return {"_no_es_json": crudo[:2000]}


def correr_caso(caso, cred, verbose=False):
    nombre = caso.get("nombre", "(sin nombre)")

    leg = caso["legacy"]
    cuerpo_leg = dict(leg.get("body") or {})
    # Legacy pide el token DENTRO del cuerpo, ademas de la cabecera.
    cuerpo_leg.setdefault("token", cred.legacy_token)
    cuerpo_leg.setdefault("correoElectronicoToken", cred.legacy_correo)
    est_leg, resp_leg = pedir(
        cred.legacy_base + leg["path"],
        leg.get("method", "POST"),
        cuerpo_leg,
        {"Authorization": "Bearer " + cred.legacy_token},
    )

    nue = caso["nuevo"]
    url_nue = cred.nuevo_base + nue["path"]
    if nue.get("query"):
        import urllib.parse
        url_nue += "?" + urllib.parse.urlencode(nue["query"])
    est_nue, resp_nue = pedir(
        url_nue,
        nue.get("method", "GET"),
        nue.get("body"),
        {"Authorization": "Bearer " + cred.nuevo_token},
    )

    if verbose:
        print("  legacy crudo:", huella(resp_leg)[:600])
        print("  nuevo  crudo:", huella(resp_nue)[:600])

    # El mapa traduce los nombres de Legacy a los del contrato nuevo.
    mapa = {clave_normal(k): clave_normal(v)
            for k, v in (caso.get("mapa_campos") or {}).items()}

    datos_leg = normalizar(desenvolver_legacy(resp_leg, caso.get("camino_legacy")), mapa)
    datos_nue = normalizar(desenvolver_nuevo(resp_nue, caso.get("camino_nuevo")))

    solo = caso.get("solo_campos")
    datos_leg = recortar(datos_leg, solo)
    datos_nue = recortar(datos_nue, solo)

    diffs = comparar(datos_leg, datos_nue, caso.get("ignorar", []),
                     ordenar_por=caso.get("ordenar_por"))

    return {
        "nombre": nombre,
        "estado_legacy": est_leg,
        "estado_nuevo": est_nue,
        "diferencias": diffs,
        "intencionales": caso.get("diferencias_intencionales", []),
        "ok": not diffs and est_leg == 200 and est_nue in (200, 204),
    }


# ---------------------------------------------------------------- principal

def casos_de(ruta):
    if os.path.isdir(ruta):
        return sorted(os.path.join(ruta, f) for f in os.listdir(ruta)
                      if f.endswith(".json"))
    return [ruta]


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    verbose = "--verbose" in argv
    objetivo = argv[1]
    cred = Credenciales()

    if not cred.legacy_token or not cred.nuevo_token:
        print("FALTA UN TOKEN. legacy={} nuevo={}".format(
            bool(cred.legacy_token), bool(cred.nuevo_token)))
        print("Pon MAC_SCRATCH, o LEGACY_TOKEN / NUEVO_TOKEN en el entorno.")
        return 2

    rutas = casos_de(objetivo)
    bien, mal = [], []

    for ruta in rutas:
        with open(ruta, "r", encoding="utf-8") as fh:
            caso = json.load(fh)
        res = correr_caso(caso, cred, verbose)
        marca = "OK  " if res["ok"] else "DIF "
        print("{} {}  (legacy {} / nuevo {})".format(
            marca, res["nombre"], res["estado_legacy"], res["estado_nuevo"]))
        if res["intencionales"]:
            for nota in res["intencionales"]:
                print("     intencional: {}".format(nota))
        if res["diferencias"]:
            for d in res["diferencias"][:25]:
                print(d)
            if len(res["diferencias"]) > 25:
                print("  ... y {} diferencias mas".format(len(res["diferencias"]) - 25))
            mal.append(res["nombre"])
        else:
            bien.append(res["nombre"])

    print("\n{} coinciden, {} con diferencias, {} casos".format(
        len(bien), len(mal), len(rutas)))
    return 1 if mal else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
