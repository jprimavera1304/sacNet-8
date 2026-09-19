using ISL_Service.Application.DTOs.VentasDevolucion;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/*
  LAS REGLAS DE LA DEVOLUCION, LAS DE MAC31 Y NINGUNA MAS

  El formulario legacy es Legacy/Mac31/Mac31/Forms/Ventas/VentasDevolucion.cs y
  la entrada a la pantalla es ConsultarVentas.cs:3584 (btnDevolucion_Click). De
  ahi salen TRES reglas que no viven en ningun procedimiento y que por eso hay
  que repetir aqui:

    1. ConsultarVentas.cs:3610  la remision CANCELADA no se devuelve.
    2. ConsultarVentas.cs:3619  la remision PAGADA no se devuelve.
    3. VentasDevolucion.cs:83   si los cascos ENTREGADOS ya cubren los del
                                CARGO, primero hay que ajustar cascos.

  Las tres se comprueban aqui y no en el navegador porque quien sabe si una
  remision se pago es la base, y entre que se pinta la pantalla y se pulsa
  Guardar alguien pudo pagarla desde Mac31.

  LO QUE **NO** SE COMPRUEBA AQUI

  Que los pagos no superen el nuevo total, que los cascos entregados no superen
  el nuevo cargo, que quede al menos una partida: todo eso lo valida
  sp_n_DevolucionVenta dentro de su propia transaccion, y ahi es donde tiene que
  estar. Repetirlo aqui seria tener dos versiones de la misma regla que se
  despegan a la primera — y ademas seria la version equivocada, porque las
  cifras "nuevas" solo existen a media transaccion.

  LAS DOS EMPRESAS SE RESUELVEN SOLAS

  sp_n_DevolucionVenta es el MISMO texto en Produccion_svr (Tauro) y en MacZ
  (Zaragoza) —se comparo: solo cambian SET ANSI_WARNINGS/ANSI_NULLS— y se
  bifurca el solo por Constantes.Funcionalidad:

    ZARA: DEVOLUCION 002 (los pagos superan el nuevo importe) y
          DEVOLUCION 003 (los cascos entregados superan el nuevo cargo).
    TAU:  DEVOLUCION 004 (los pagos superan el nuevo total a pagar).

  Igual sp_n_VentasUsadosCargo y sp_n_VentasUsadosCredito, que tienen bloques
  IF @Funcionalidad = 'TAU' / 'ZARA' adentro. Asi que la "regla de negocio de
  las dos empresas" se cumple llamando al procedimiento, no copiandolo: si
  manana cambia una, cambia para las dos pantallas a la vez.

  En sp_n_ConsultaVentas si hay diferencias entre las dos bases, pero ninguna
  afecta a esta pantalla: el bloque que llena msgErr esta guardado por
  IF @Funcionalidad = 'ZARA' en LAS DOS, y solo se recorre consultando por la
  cadena @IDsVenta. Legacy consulta por @IDVenta, asi que el lblErrMsg de Mac31
  esta muerto aqui en las dos empresas — comprobado ejecutando los dos caminos
  contra Produccion_svr y MacZ. Se sigue respetando el campo por si ese camino
  cambia, pero no es lo que bloquea una devolucion hoy.
*/
public class VentasDevolucionService : IVentasDevolucionService
{
    private readonly IVentasDevolucionRepository _repo;

    public VentasDevolucionService(IVentasDevolucionRepository repo)
    {
        _repo = repo;
    }

    public async Task<VentasDevolucionVistaResponse> VistaAsync(int idVenta, CancellationToken ct)
    {
        var (vista, existe) = await _repo.ConsultarCabeceraAsync(idVenta, ct);
        if (!existe)
        {
            vista.Error = "No se encontró la remisión.";
            return vista;
        }

        vista.Funcionalidad = await _repo.ConsultarFuncionalidadAsync(ct);
        vista.Partidas = await _repo.ConsultarPartidasAsync(idVenta, ct);
        vista.CascosRemision = await _repo.ConsultarCascosRemisionAsync(idVenta, ct);
        vista.CascosEntregados = await _repo.ConsultarCascosEntregadosAsync(idVenta, ct);

        AplicarBloqueoDeCascos(vista);

        return vista;
    }

    /*
      LOS CASCOS YA ESTAN ENTREGADOS  (VentasDevolucion.cs:83-96)

      Mac31 compara los DOS totales tal como los acaba de pintar y, si el de
      entregados llega al de cargo, esconde la cantidad y los tres botones y
      enseña el mensaje. La condicion es literal: > 0 Y >= cargo. El ">= 0" solo
      no valdria — con ambos en cero (una remision sin cascos) bloquearia una
      devolucion perfectamente valida.

      El texto es el de Mac31, con sus mayusculas: es el que la gente lleva años
      leyendo y el que repite por telefono cuando llama a preguntar.
    */
    private static void AplicarBloqueoDeCascos(VentasDevolucionVistaResponse vista)
    {
        var credito = vista.CascosEntregados.Total;
        var cargo = vista.CascosRemision.Total;

        if (credito > 0 && credito >= cargo)
        {
            vista.CascosEntregadosBloquean = true;
            vista.MotivoBloqueo =
                "LOS CASCOS ESTAN ENTREGADOS.\n\n" +
                "DEBE REALIZAR EL AJUSTE DE CASCOS ANTES DE HACER LA DEVOLUCIÓN.";
        }
    }

    public async Task<VentasDevolucionResponse> DevolverAsync(
        VentasDevolucionRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdVenta <= 0)
            return No(0, "No se indicó la remisión.");

        /*
          "SELECCIONE LA DEVOLUCIÓN" (VentasDevolucion.cs:674). Mac31 lo enseña
          cuando la rejilla de abajo esta vacia, o sea cuando se pulso Guardar
          sin haber agregado nada.
        */
        var lineas = request.Lineas
            .Where(l => l.IdVentaDetalle > 0 && l.Cantidad > 0)
            .ToList();

        if (lineas.Count == 0)
            return No(request.IdVenta, "SELECCIONE LA DEVOLUCIÓN");

        /*
          UNA SOLA LINEA POR PARTIDA.

          Mac31 no puede mandar dos renglones de la misma partida: al agregar
          busca si ya esta y SUMA la cantidad (VentasDevolucion.cs:597-606). El
          SP recorre las dos listas en paralelo con un cursor y, con la partida
          repetida, la segunda vuelta compararia contra una cantidad ya
          descontada. Se suma aqui por si el cliente manda las dos.
        */
        var agrupadas = lineas
            .GroupBy(l => l.IdVentaDetalle)
            .Select(g => new VentasDevolucionLinea
            {
                IdVentaDetalle = g.Key,
                Cantidad = g.Sum(x => x.Cantidad)
            })
            .ToList();

        // Las tres reglas que no viven en el SP. Se vuelven a mirar aqui, ya con
        // los datos frescos, porque entre pintar y pulsar pasa tiempo.
        var vista = await VistaAsync(request.IdVenta, ct);
        if (vista.Error.Length > 0)
            return No(request.IdVenta, vista.Error);

        /*
          Los textos son los de Mac31 con su espaciado doble alrededor del folio
          (ConsultarVentas.cs:3612 y 3621). El SP tambien se niega si esta
          cancelada, pero con "EL FOLIO ... ESTA CANCELADO"; el de pagada NO lo
          comprueba nadie mas, asi que si esto faltara se podria devolver una
          remision ya cobrada desde el web y no desde Mac31.
        */
        if (vista.Cancelada)
            return No(request.IdVenta, $"LA REMISIÓN  {vista.Folio}  ESTA CANCELADA.");

        if (vista.Pagada)
            return No(request.IdVenta, $"LA REMISIÓN  {vista.Folio}  YA ESTA PAGADA.");

        if (vista.CascosEntregadosBloquean)
            return No(request.IdVenta, vista.MotivoBloqueo);

        /*
          Mac31 esconde el boton de Guardar cuando msgErr trae texto
          (VentasDevolucion.cs:161). Aqui el boton nunca se esconde, pero la
          operacion se niega con SU mensaje: es lo mismo que pasaba alla, dicho
          con todas sus letras. Hoy no entra nunca —ver el encabezado— y se deja
          por lo mismo: si esa consulta cambia de camino, el aviso tiene que
          seguir funcionando y no descubrirse tarde.
        */
        if (vista.MsgErr.Length > 0)
            return No(request.IdVenta, vista.MsgErr);

        /*
          El delimitador es '~', el mismo que usa MacServicios2
          (Variables.DELIMITADOR_PARAM_TILDE_SP) y que espera el SP para partir
          las dos cadenas. Las dos listas van EN EL MISMO ORDEN: el cursor del
          procedimiento las empareja por posicion, no por id.
        */
        var ids = string.Join("~", agrupadas.Select(l => l.IdVentaDetalle));
        var cantidades = string.Join("~", agrupadas.Select(l => l.Cantidad));

        return await _repo.DevolverAsync(request.IdVenta, idUsuario, ids, cantidades, equipo, ct);
    }

    private static VentasDevolucionResponse No(int idVenta, string mensaje) =>
        new() { Ok = false, IdVenta = idVenta, Mensaje = mensaje };
}
