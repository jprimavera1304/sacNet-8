"""Verifica contra la FUENTE los dos casos que no se pueden comparar por HTTP.

EL PROBLEMA
-----------
El backend nuevo escribe el IDUsuario del TOKEN, y el usuario del banco de
pruebas no esta ligado a Mac31: escribe 0. Dos de los procedimientos de esta ola
solo registran la firma cuando el id NO es cero:

    sp_n_ActualizaStatusVentasDescuentosAjustes  (autoriza solo con id != 0)
    sp_n_CancelarPedido                          (escribe IDUsuarioCancelo)

Y el controlador de legacy RECHAZA el 0 en los dos, asi que no se le puede pedir
que haga lo mismo: no hay forma de que los dos lados manden el mismo id.

LA PRUEBA
---------
Se ejecuta el procedimiento DIRECTAMENTE con sqlcmd y los MISMOS parametros que
manda el backend nuevo (con el 0), y se comprueba que el efecto en la base es
exactamente el que dejo el endpoint nuevo. Con eso queda demostrado que la unica
diferencia contra legacy es el valor del id, no la operacion.
"""
import json
import os
import subprocess

SC = os.path.dirname(os.path.abspath(__file__))
COMPARADOR = r"D:\JuanD\GitHub\MAC\sacNet-8\migracion\comparador"

VDA = 27569
PEDIDO = 195033


def sql(consulta):
    r = subprocess.run(
        ["sqlcmd", "-S", "LAP-JUAND", "-d", "Produccion_svr", "-U", "sa", "-P", "123",
         "-C", "-I", "-W", "-h", "-1", "-Q", "SET NOCOUNT ON; " + consulta],
        capture_output=True, text=True, timeout=180,
        env=dict(os.environ, MSYS_NO_PATHCONV="1"))
    return ((r.stdout or "") + (r.stderr or "")).strip()


def volver():
    subprocess.run(["bash", "instantanea.sh", "volver"], cwd=COMPARADOR,
                   capture_output=True, text=True, timeout=600)


FOTOS = {
    "vda": f"SELECT ISNULL([IDUsuario Autorizacion],0), CASE WHEN [Fecha Autorizacion] IS NULL THEN 0 ELSE 1 END, ISNULL([IDUsuario Rechazo],0) FROM dbo.VentasDescuentosAjustes WHERE IDVentasDescuentosAjustes={VDA};",
    "pedido": f"SELECT IDStatusPedido, ISNULL(IDUsuarioCancelo,0), ISNULL(EquipoCancelo,'<null>') FROM dbo.Pedidos WHERE IDPedido={PEDIDO};",
}


def foto():
    return {k: sql(v) for k, v in FOTOS.items()}


print("=== sp_n_ActualizaStatusVentasDescuentosAjustes con IDUsuario = 0 (lo que manda el nuevo)")
volver()
antes = foto()
sql(f"EXEC dbo.sp_n_ActualizaStatusVentasDescuentosAjustes "
    f"@IDVentasDescuentosAjustes={VDA}, @IDUsuarioAutorizacion=0, @IDUsuarioRechazo=0, "
    f"@Fecha='2026-10-06 00:00:00', @MotivoRechazo='';")
despues_cero = foto()
print("  antes  :", antes["vda"])
print("  con 0  :", despues_cero["vda"])

print("=== el mismo, con IDUsuario = 3 (lo que se le manda a legacy)")
volver()
sql(f"EXEC dbo.sp_n_ActualizaStatusVentasDescuentosAjustes "
    f"@IDVentasDescuentosAjustes={VDA}, @IDUsuarioAutorizacion=3, @IDUsuarioRechazo=0, "
    f"@Fecha='2026-10-06 00:00:00', @MotivoRechazo='';")
despues_tres = foto()
print("  con 3  :", despues_tres["vda"])

print()
print("=== sp_n_CancelarPedido con IDUsuario = 0")
volver()
antes_p = foto()
sql(f"EXEC dbo.sp_n_CancelarPedido @IDPedido={PEDIDO}, @IDUsuario=0, @Equipo='BANCOPRUEBAS';")
cero_p = foto()
print("  antes  :", antes_p["pedido"])
print("  con 0  :", cero_p["pedido"])

print("=== el mismo, con IDUsuario = 3")
volver()
sql(f"EXEC dbo.sp_n_CancelarPedido @IDPedido={PEDIDO}, @IDUsuario=3, @Equipo='BANCOPRUEBAS';")
tres_p = foto()
print("  con 3  :", tres_p["pedido"])

volver()

salida = {
    "vda": {"antes": antes["vda"], "con_id_0": despues_cero["vda"], "con_id_3": despues_tres["vda"]},
    "pedido": {"antes": antes_p["pedido"], "con_id_0": cero_p["pedido"], "con_id_3": tres_p["pedido"]},
}
json.dump(salida, open(os.path.join(SC, "contra_fuente.json"), "w", encoding="utf-8"),
          ensure_ascii=False, indent=2)
print("\nguardado contra_fuente.json")
