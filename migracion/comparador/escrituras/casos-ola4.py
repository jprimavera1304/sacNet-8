# Los casos de ESCRITURA de la ola 4.
#
# La firma del usuario: el backend nuevo escribe el IDUsuario del TOKEN, y el
# usuario del banco de pruebas no esta ligado a Mac31, asi que escribe 0. Legacy
# lo recibe en el cuerpo y varios de sus controladores RECHAZAN el 0, asi que a
# legacy se le manda 3 (JUAN). Por eso las fotos NO miran las columnas de firma
# (IDUsuario, Equipo y sus variantes de cancelacion): la diferencia en esas
# columnas es esperada y conocida, y lo que importa es el resto del efecto.
import json
import os

SC = os.path.dirname(os.path.abspath(__file__))

ID_USUARIO_LEGACY = 3          # JUAN, el unico usuario web ligado a Mac31 con id propio
EQUIPO = "BANCOPRUEBAS"
ALMACEN, PRODUCTO = 2, 10316
ALMACEN_USADO = 1
TIPO_USADO = 1
REPARTIDOR = 1
VENTA = 370852
VENTA_CON_SALDO = 370842   # 1,360 de saldo: sin saldo el SP de incobrable no escribe nada
CLIENTE, AGENTE = 2597, 7
DESCUENTO, CATEGORIA, MARCA, DESC_DETALLE = 53518, 86, 87, 2285617
PEDIDO = 195033
GARANTIA = 3997
VDA = 27569
AJUSTE = 71928
INVPED = 1071
COBRANZA = 208623

NOTA_FIRMA = ("La firma difiere a proposito: legacy recibe IDUsuario = 3 en el cuerpo y "
              "el nuevo escribe el 0 del token (el usuario del banco no esta ligado a "
              "Mac31). Las fotos no miran las columnas de firma.")

CASOS = []


def caso(nombre, legacy, nuevo, fotos, notas=None):
    CASOS.append({
        "nombre": nombre,
        "legacy": legacy,
        "nuevo": nuevo,
        "fotos": fotos,
        "notas": notas or [NOTA_FIRMA],
    })


# ------------------------------------------------ ajustes de inventario
caso(
    "ajuste-inventario-crear",
    {"path": "/api/ajusteinventario", "method": "POST",
     "body": {"IDAlmacen": ALMACEN, "IDProducto": PRODUCTO, "IDUsuario": ID_USUARIO_LEGACY,
              "Entrada": 5, "Salida": 0, "Motivo": "PRUEBA OLA4", "Equipo": EQUIPO}},
    {"path": "/api/inventario/ajustes", "method": "POST",
     "body": {"idAlmacen": ALMACEN, "idProducto": PRODUCTO, "entrada": 5, "salida": 0,
              "motivo": "PRUEBA OLA4"}},
    {
        "ajustes": f"SELECT COUNT(*), ISNULL(SUM(Entrada),0), ISNULL(SUM(Salida),0), ISNULL(MAX(IDAjusteInventario),0) FROM dbo.[Ajuste Inventario] WHERE IDProducto={PRODUCTO} AND IDAlmacen={ALMACEN};",
        "existencia": f"SELECT Existencia FROM dbo.AlmacenProducto WHERE IDAlmacen={ALMACEN} AND IDProducto={PRODUCTO};",
    },
)

caso(
    "ajuste-inventario-cancelar",
    {"path": "/api/ajusteinventario", "method": "PUT",
     "body": {"IDAjusteInventario": AJUSTE, "IDUsuario": ID_USUARIO_LEGACY, "Equipo": EQUIPO}},
    {"path": "/api/inventario/ajustes/cancelar", "method": "POST",
     "body": {"idAjusteInventario": AJUSTE}},
    {
        "cancelado": f"SELECT CASE WHEN [Fecha Cancelacion] IS NULL THEN 0 ELSE 1 END FROM dbo.[Ajuste Inventario] WHERE IDAjusteInventario={AJUSTE};",
        "existencia": f"SELECT ap.Existencia FROM dbo.AlmacenProducto ap JOIN dbo.[Ajuste Inventario] ai ON ai.IDAlmacen=ap.IDAlmacen AND ai.IDProducto=ap.IDProducto WHERE ai.IDAjusteInventario={AJUSTE};",
    },
)

# ------------------------------------------------ catalogo de tipos de casco
caso(
    "tipo-usado-crear",
    {"path": "/api/cattipousado", "method": "POST",
     "body": {"TipoUsado": "PRUEBA OLA4", "TipoUsadoCorto": "P4",
              "VentaCostoCargoSinIva": 10, "VentaCostoCargoConIva": 11.6,
              "VentaCostoCreditoSinIva": 9, "VentaCostoCreditoConIva": 10.44,
              "Bonificacion": 1, "CostoExcedentes": 2, "PesoKg": 3, "Orden": 99}},
    {"path": "/api/inventario/tipos-usado", "method": "POST",
     "body": {"tipoUsado": "PRUEBA OLA4", "tipoUsadoCorto": "P4",
              "ventaCostoCargoSinIva": 10, "ventaCostoCargoConIva": 11.6,
              "ventaCostoCreditoSinIva": 9, "ventaCostoCreditoConIva": 10.44,
              "bonificacion": 1, "costoExcedentes": 2, "pesoKg": 3, "orden": 99}},
    {
        "tipos": "SELECT COUNT(*), ISNULL(MAX(IDTipoUsado),0) FROM dbo.[Catalogo TiposUsados];",
        "nuevo": "SELECT [Tipo de Usado], TipoUsadoCorto, [Costo Cargo], CostoCargoConIva, Bonificacion, CostoExcedentes, PesoKg, Orden FROM dbo.[Catalogo TiposUsados] WHERE [Tipo de Usado] = 'PRUEBA OLA4';",
    },
    notas=["Este procedimiento no recibe firma: no declara IDUsuario ni Equipo."],
)

caso(
    "tipo-usado-actualizar",
    {"path": "/api/cattipousado", "method": "PUT",
     "body": {"IDTipoUsado": TIPO_USADO, "IDStatus": 1, "TipoUsado": "PRUEBA MOD",
              "TipoUsadoCorto": "PM", "VentaCostoCargoSinIva": 20, "VentaCostoCargoConIva": 23.2,
              "VentaCostoCreditoSinIva": 18, "VentaCostoCreditoConIva": 20.88,
              "Bonificacion": 2, "CostoExcedentes": 3, "PesoKg": 4, "Orden": 5}},
    {"path": "/api/inventario/tipos-usado", "method": "PUT",
     "body": {"idTipoUsado": TIPO_USADO, "tipoUsado": "PRUEBA MOD", "tipoUsadoCorto": "PM",
              "ventaCostoCargoSinIva": 20, "ventaCostoCargoConIva": 23.2,
              "ventaCostoCreditoSinIva": 18, "ventaCostoCreditoConIva": 20.88,
              "bonificacion": 2, "costoExcedentes": 3, "pesoKg": 4, "orden": 5}},
    {
        "fila": f"SELECT IDStatus, [Tipo de Usado], TipoUsadoCorto, [Costo Cargo], CostoCargoConIva, [Costo Credito], CostoCreditoConIva, Bonificacion, CostoExcedentes, PesoKg, Orden FROM dbo.[Catalogo TiposUsados] WHERE IDTipoUsado={TIPO_USADO};",
    },
    notas=["El nuevo CONSERVA el IDStatus que tenia el renglon (lo lee antes); legacy lo "
           "toma del cuerpo. En este caso se le manda a legacy el mismo que ya tiene, "
           "para que el efecto sea comparable."],
)

# ------------------------------------------------ movimientos de usados
caso(
    "usados-movimiento-crear",
    {"path": "/api/usadosmovimientos", "method": "POST",
     "body": {"IDAlmacenUsado": ALMACEN_USADO, "IDGallero": 0, "IDRepartidor": REPARTIDOR,
              "Motivo": "PRUEBA OLA4", "IDsTipoUsadoLst": [TIPO_USADO],
              "PreciosCambioLst": [1.5], "PreciosUnitarioLst": [2.5],
              "CantidadesSalidaLst": [1], "CantidadesEntradaLst": [2],
              "TotalPagarEntrada": 5, "IDCobro": 0, "IDVenta": 0,
              "IDTipoCompra": 0, "IDTipoVenta": 0,
              "IDUsuario": ID_USUARIO_LEGACY, "Equipo": EQUIPO}},
    {"path": "/api/ventas/usados/movimientos", "method": "POST",
     "body": {"idAlmacenUsado": ALMACEN_USADO, "idRepartidor": REPARTIDOR,
              "motivo": "PRUEBA OLA4", "idsTipoUsado": [TIPO_USADO],
              "preciosCambio": [1.5], "preciosUnitario": [2.5],
              "cantidadesSalida": [1], "cantidadesEntrada": [2], "totalPagarEntrada": 5}},
    {
        "movimientos": "SELECT COUNT(*), ISNULL(MAX(IDUsadoMovimiento),0) FROM dbo.UsadosMovimientos;",
        "detalle": "SELECT COUNT(*), ISNULL(SUM(CantidadEntrada),0), ISNULL(SUM(CantidadSalida),0) FROM dbo.UsadosMovimientosDetalle;",
    },
)

# ------------------------------------------------ inventario de pedidos
caso(
    "inventario-pedidos-crear",
    {"path": "/api/inventariopedidos", "method": "POST",
     "body": {"IDStatus": 1, "Observaciones": "PRUEBA OLA4",
              "IDsProductoLst": [PRODUCTO], "CodigosLst": ["COD4"],
              "CantidadesInventarioLst": [10], "CantidadesVentasLst": [2],
              "CantidadesFaltanteLst": [1], "CantidadesPedidoLst": [3],
              "IDUsuario": ID_USUARIO_LEGACY, "Equipo": EQUIPO}},
    {"path": "/api/inventario/pedidos", "method": "POST",
     "body": {"idStatus": 1, "observaciones": "PRUEBA OLA4",
              "partidas": [{"idProducto": PRODUCTO, "codigo": "COD4",
                            "cantidadInventario": 10, "cantidadVentas": 2,
                            "cantidadFaltante": 1, "cantidadPedido": 3}]}},
    {
        "cabeceras": "SELECT COUNT(*), ISNULL(MAX(IDInventarioPedido),0) FROM dbo.InventarioPedidos;",
        "detalle": "SELECT COUNT(*), ISNULL(SUM(Inventario),0), ISNULL(SUM(Ventas),0), ISNULL(SUM(Faltante),0), ISNULL(SUM(Pedido),0) FROM dbo.InventarioPedidosDetalle;",
    },
)

caso(
    "inventario-pedidos-cancelar",
    {"path": "/api/inventariopedidos/cancelar", "method": "POST",
     "body": {"IDInventarioPedido": INVPED, "IDUsuario": ID_USUARIO_LEGACY, "Equipo": EQUIPO}},
    {"path": "/api/inventario/pedidos/cancelar", "method": "POST",
     "body": {"idInventarioPedido": INVPED}},
    {
        "fila": f"SELECT IDStatus, CASE WHEN FechaCancelacion IS NULL THEN 0 ELSE 1 END FROM dbo.InventarioPedidos WHERE IDInventarioPedido={INVPED};",
    },
)

# ------------------------------------------------ garantias
caso(
    "garantia-crear",
    {"path": "/api/garantias", "method": "POST",
     "body": {"IDEmpresa": 1, "IDCliente": CLIENTE, "IDRepartidor": REPARTIDOR,
              "IDAlmacen": ALMACEN, "IDUsuario": ID_USUARIO_LEGACY, "IDProducto": PRODUCTO,
              "FechaIngreso": "06-10-2026", "Ticket": "T-OLA4", "Vigencia": "12",
              "Garantia": "G-OLA4", "Motivo": "PRUEBA OLA4", "Equipo": EQUIPO,
              "GarantiaMayorista": 0, "IDEmpresaMayorista": 0, "IDProductoCambio": 0}},
    {"path": "/api/ventas/garantias", "method": "POST",
     "body": {"idEmpresa": 1, "idCliente": CLIENTE, "idRepartidor": REPARTIDOR,
              "idAlmacen": ALMACEN, "idProducto": PRODUCTO,
              "fechaIngreso": "2026-10-06", "ticket": "T-OLA4", "vigencia": "12",
              "garantia": "G-OLA4", "motivo": "PRUEBA OLA4"}},
    {
        "garantias": "SELECT COUNT(*), ISNULL(MAX(IDGarantia),0) FROM dbo.Garantias;",
        "nueva": "SELECT IDCliente, IDProducto, IDAlmacen, IDStatusGarantia, Ticket, Vigencia, Garantia, Motivo, CONVERT(varchar(19), FechaIngreso, 120) FROM dbo.Garantias WHERE Ticket = 'T-OLA4';",
    },
)

caso(
    "garantia-registro-crear",
    {"path": "/api/garantias/registrar", "method": "POST",
     "body": {"IDEmpresa": 1, "IDProducto": PRODUCTO, "Ticket": "TR-OLA4", "Vigencia": "12",
              "Garantia": "GR-OLA4", "Observaciones": "PRUEBA OLA4",
              "IDUsuario": ID_USUARIO_LEGACY, "Equipo": EQUIPO}},
    {"path": "/api/ventas/garantias/registros", "method": "POST",
     "body": {"idEmpresa": 1, "idProducto": PRODUCTO, "ticket": "TR-OLA4", "vigencia": "12",
              "garantia": "GR-OLA4", "observaciones": "PRUEBA OLA4"}},
    {
        "registros": "SELECT COUNT(*), ISNULL(MAX(IDGarantiaRegistro),0) FROM dbo.GarantiasRegistro;",
        "nuevo": "SELECT IDProducto, IDStatus, Folio, Ticket, Vigencia, Garantia, Observaciones FROM dbo.GarantiasRegistro WHERE Ticket = 'TR-OLA4';",
    },
)

# ------------------------------------------------ cobranza
caso(
    "cobranza-crear",
    {"path": "/api/cobranzas", "method": "POST",
     "body": {"IDRepartidor": REPARTIDOR, "IDsVentaAgregar": str(VENTA),
              "IDUsuario": ID_USUARIO_LEGACY, "Equipo": EQUIPO,
              "Observaciones": "PRUEBA OLA4", "IDCobro": 0, "esCobro": 0, "IDAlmacen": 0}},
    {"path": "/api/ventas/cobranza/cobranzas", "method": "POST",
     "body": {"idRepartidor": REPARTIDOR, "idsVentaAgregar": [VENTA],
              "observaciones": "PRUEBA OLA4"}},
    {
        "cobranzas": "SELECT COUNT(*), ISNULL(MAX(IDCobranza),0), ISNULL(MAX(Folio),0) FROM dbo.Cobranza;",
        "detalle": f"SELECT COUNT(*) FROM dbo.CobranzaDetalle WHERE IDVenta={VENTA};",
    },
)

caso(
    "cobranza-cancelar",
    {"path": "/api/cobranzas/cancelar", "method": "POST",
     "body": {"IDsCobranza": str(COBRANZA), "IDUsuario": ID_USUARIO_LEGACY,
              "Equipo": EQUIPO, "IDsCobro": ""}},
    {"path": "/api/ventas/cobranza/cobranzas/cancelar", "method": "POST",
     "body": {"idsCobranza": [COBRANZA]}},
    {
        "fila": f"SELECT CASE WHEN [Fecha Cancelacion] IS NULL THEN 0 ELSE 1 END FROM dbo.Cobranza WHERE IDCobranza={COBRANZA};",
        "detalle": f"SELECT COUNT(*) FROM dbo.CobranzaDetalle WHERE IDCobranza={COBRANZA};",
    },
)

# ------------------------------------------------ descuentos detalle
caso(
    "descuento-detalle-crear",
    {"path": "/api/descuentosdetalle", "method": "POST",
     "body": {"IDDescuento": DESCUENTO, "IDCategoria": CATEGORIA, "IDMarca": MARCA,
              "Porcentaje": "5", "AplicarTodos": 0,
              "IDUsuario": ID_USUARIO_LEGACY, "Equipo": EQUIPO}},
    {"path": "/api/ventas/descuentos/detalle", "method": "POST",
     "body": {"idDescuento": DESCUENTO, "idCategoria": CATEGORIA, "idMarca": MARCA,
              "porcentaje": "5", "aplicarTodos": 0}},
    {
        "detalle": f"SELECT COUNT(*), ISNULL(SUM(Porcentaje),0) FROM dbo.[Detalle Descuentos] WHERE IDDescuento={DESCUENTO};",
        "log": f"SELECT COUNT(*) FROM dbo.[Detalle Descuentos Log] WHERE IDDescuento={DESCUENTO};",
    },
)

caso(
    "descuento-detalle-eliminar",
    {"path": "/api/descuentosdetalle", "method": "DELETE",
     "body": {"IDDescuentoDetalle": DESC_DETALLE, "IDDescuento": DESCUENTO,
              "IDCategoria": CATEGORIA, "IDMarca": MARCA,
              "IDUsuario": ID_USUARIO_LEGACY, "Equipo": EQUIPO}},
    {"path": "/api/ventas/descuentos/detalle/eliminar", "method": "POST",
     "body": {"idDescuentoDetalle": DESC_DETALLE, "idDescuento": DESCUENTO,
              "idCategoria": CATEGORIA, "idMarca": MARCA}},
    {
        "detalle": f"SELECT COUNT(*) FROM dbo.[Detalle Descuentos] WHERE IDDescuentoDetalle={DESC_DETALLE};",
        "total": f"SELECT COUNT(*) FROM dbo.[Detalle Descuentos] WHERE IDDescuento={DESCUENTO};",
    },
)

# ------------------------------------------------ ventas
caso(
    "venta-actualizar-observaciones",
    {"path": "/api/ventas/actualizar", "method": "POST",
     "body": {"IDVenta": VENTA, "Observaciones": "PRUEBA OLA4"}},
    {"path": "/api/ventas/actualizar", "method": "POST",
     "body": {"idVenta": VENTA, "observaciones": "PRUEBA OLA4"}},
    {
        "fila": f"SELECT ISNULL(ObservacionesPago,'<null>'), ISNULL(Observaciones,'<null>') FROM dbo.Ventas WHERE IDVenta={VENTA};",
    },
    notas=["Este procedimiento no recibe firma: declara solo @IDVenta y @Observaciones.",
           "OJO: escribe en Ventas.ObservacionesPago, NO en Ventas.Observaciones. La "
           "primera foto miraba la columna equivocada y daba verde sin mirar nada."],
)

caso(
    "venta-incobrable",
    {"path": "/api/ventaspagos/incobrable", "method": "POST",
     "body": {"IDVenta": VENTA_CON_SALDO, "IDUsuario": ID_USUARIO_LEGACY, "Equipo": EQUIPO}},
    {"path": "/api/ventas/pagos/incobrable", "method": "POST",
     "body": {"idVenta": VENTA_CON_SALDO}},
    {
        "pagos": f"SELECT COUNT(*), ISNULL(SUM(Abono),0), ISNULL(SUM(Cargo),0), ISNULL(MAX(IDTipoPago),0) FROM dbo.[Ventas Pagos] WHERE IDVenta={VENTA_CON_SALDO};",
        "venta": f"SELECT Abonos, [Total a Pagar], Descuentos, ISNULL(SaldoIncobrable,0), ISNULL(SaldoIncobrableDinero,0), ISNULL(SaldoIncobrableCascos,0), CASE WHEN [Fecha Pago] IS NULL THEN 0 ELSE 1 END FROM dbo.Ventas WHERE IDVenta={VENTA_CON_SALDO};",
        "pagosUsados": f"SELECT COUNT(*) FROM dbo.[Ventas Pagos Usados] WHERE IDVenta={VENTA_CON_SALDO};",
    },
)

caso(
    "venta-reasignar-agente",
    {"path": "/api/ventasreasignacion", "method": "POST",
     "body": {"IDCliente": CLIENTE, "IDAgente": AGENTE, "IDUsuario": ID_USUARIO_LEGACY,
              "CambiarCliente": 0, "IDsVenta": str(VENTA), "Equipo": EQUIPO,
              "Motivo": "PRUEBA OLA4"}},
    {"path": "/api/ventas/reasignar", "method": "POST",
     "body": {"idCliente": CLIENTE, "idAgente": AGENTE, "cambiarCliente": 0,
              "idsVenta": [VENTA], "motivo": "PRUEBA OLA4"}},
    {
        "ventaAgente": f"SELECT IDCliente, IDAgente FROM dbo.Ventas WHERE IDVenta={VENTA};",
        "bitacora": f"SELECT COUNT(*), ISNULL(MAX(IDAgenteAnterior),0), ISNULL(MAX(IDAgenteNuevo),0) FROM dbo.[Ventas Reasignacion] WHERE IDVenta={VENTA};",
    },
)

# ------------------------------------------------ ajuste de descuento (alta)
caso(
    "descuento-ajuste-crear",
    {"path": "/api/ventasdescuentosajustes", "method": "POST",
     "body": {"IDTipoLiquidacion": 1, "IDVenta": 370853, "IDEmpresa": 11, "Folio": 1,
              "IDUsuario": ID_USUARIO_LEGACY, "DescuentoGlobal": "5",
              "IDVentasDetalle": "1069325", "VentaDetalleDescuento1": "5",
              "VentaDetalleDescuento2": "0", "VentaDetalleDescuento3": "0",
              "DescuentoAdicional": "0", "Fecha": "06-10-2026",
              "Observaciones": "PRUEBA OLA4", "NuevoTotalPagar": 100,
              "FechaLiquidacion": "06-10-2026", "TextoDescuentos": "5 %",
              "TextoDescuentosPorLinea": "L 5%", "DescuentoAplicar": 5,
              "Diferencia": 0}},
    {"path": "/api/ventas/descuentos/ajustes", "method": "POST",
     "body": {"idTipoLiquidacion": 1, "idVenta": 370853, "idEmpresa": 11, "folio": 1,
              "descuentoGlobal": [5], "idsVentaDetalle": [1069325],
              "descuento1": [5], "descuento2": [0], "descuento3": [0],
              "descuentoAdicional": [0], "fecha": "2026-10-06",
              "observaciones": "PRUEBA OLA4", "nuevoTotalPagar": 100,
              "fechaLiquidacion": "2026-10-06", "textoDescuentos": "5 %",
              "textoDescuentosPorLinea": "L 5%", "descuentoAplicar": 5,
              "diferencia": 0}},
    {
        "ajustes": "SELECT COUNT(*), ISNULL(MAX(IDVentasDescuentosAjustes),0) FROM dbo.VentasDescuentosAjustes;",
        "nuevo": "SELECT IDTipoLiquidacion, IDVenta, Folio, DescuentoGlobal, IDVentasDetalle, VentaDetalleDescuento1, CONVERT(varchar(10), Fecha, 120), CONVERT(varchar(10), [Fecha Liquidacion], 120), [Nuevo Total Pagar], [Texto Descuentos], [Descuento Aplicar] FROM dbo.VentasDescuentosAjustes WHERE Observaciones = 'PRUEBA OLA4';",
    },
    notas=["LA FECHA VA SIN HORA: el procedimiento le pega la hora el mismo "
           "(`@Fecha = @Fecha + ' ' + <hora>`), y con la hora dentro guarda NULL sin "
           "dar error (ANSI_WARNINGS OFF). La foto compara la fecha guardada.",
           NOTA_FIRMA],
)


def main():
    ruta = os.path.join(SC, "casos_escritura.json")
    json.dump(CASOS, open(ruta, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(len(CASOS), "casos de escritura en", ruta)


if __name__ == "__main__":
    main()
