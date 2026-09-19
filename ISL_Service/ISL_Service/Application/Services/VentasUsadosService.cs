using ISL_Service.Application.DTOs.VentasUsados;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/*
  MODIFICACION DE USADOS  —  LAS REGLAS, Y DE DONDE SALE CADA UNA

  Traduccion de Legacy/Mac31/Mac31/Forms/Ventas/VentasUsados.cs (1019 lineas) y
  del boton que la abre, ConsultarVentas.cs:3529 (btnUsados_Click). Cada regla
  lleva la linea de legacy de la que viene; si alguna se discute, se lee alla.

  ─────────────────────────────────────────────────────────────────────────
  QUE SE PUEDE MODIFICAR
  ─────────────────────────────────────────────────────────────────────────
  SOLO la CANTIDAD de cada tipo de usado en el bloque de CREDITOS —los cascos
  que el cliente SI entrego—. Nada mas. En Mac31 los dos grids se pintan
  poniendo ReadOnly = true en todas sus columnas (VentasUsados.cs:269-273 para
  cargos y 423-427 para creditos) y el unico control de captura de la forma es
  txtCantidadUsadoCredito. No se puede tocar:

    - el COSTO de un usado          (viene del [Catalogo TiposUsados])
    - los CARGOS                    (lo que se le cobra; se fijo al vender)
    - importe, descuentos, abonos   (son de la venta, no de esta pantalla)

  Por eso GuardarAsync ignora cualquier peso que venga del navegador y vuelve a
  leer los costos de la base antes de calcular. Lo unico que se le cree al
  cliente son las cantidades.

  ─────────────────────────────────────────────────────────────────────────
  CUANDO NO SE PUEDE MODIFICAR NADA
  ─────────────────────────────────────────────────────────────────────────
    - Folio "CV-"           no es una remision de venta (ConsultarVentas.cs:3546)
    - Remision CANCELADA    (ConsultarVentas.cs:3550-3557)
    - Folio SIN usados      (VentasUsados.cs:72-79: se avisa y se cierra)
    - Venta con FECHA DE PAGO: la pantalla se abre pero de SOLO LECTURA. Mac31
      esconde guardar, actualizar y la caja de cantidad (VentasUsados.cs:82-94).

  ─────────────────────────────────────────────────────────────────────────
  EL MAXIMO DE DIFERENCIA — DE DONDE SALE
  ─────────────────────────────────────────────────────────────────────────
  De la tabla Constantes, columna MaximaDiferenciaUsados (hoy 301.99 en las dos
  empresas). Es un monto CON IVA. Mac31 lo lee de Globales.dtConstante y lo
  pinta arriba a la derecha (VentasUsados.cs:67) y lo compara en dos sitios
  (lineas 643 y 717). No es un porcentaje ni un numero del programa: es un dato
  de la empresa, y cada base puede tener el suyo.

  Que pasa al rebasarlo: la pantalla se pone en ROJO y marca usadosIncorrecto,
  y btnGuardar se niega a guardar (VentasUsados.cs:904-909). Que pasa si NO se
  rebasa: se permite y se AJUSTA el monto de los creditos hacia abajo, con un
  aviso azul. Ojo con el borde: la comparacion es `>=`, asi que una diferencia
  de exactamente 301.99 ya NO pasa.

  ─────────────────────────────────────────────────────────────────────────
  TAURO Y ZARAGOZA
  ─────────────────────────────────────────────────────────────────────────
  Estas reglas son las MISMAS para las dos: VentasUsados.cs no se ramifica ni
  una vez por Funcionalidad. Lo que cambia esta dentro de los procedimientos, y
  lo unico que hay que decidir aqui es cual de los dos de ajuste se llama
  (Constantes.EsquemaPago), que es lo que hace el repositorio.
*/
public class VentasUsadosService : IVentasUsadosService
{
    private readonly IVentasUsadosRepository _repo;

    public VentasUsadosService(IVentasUsadosRepository repo)
    {
        _repo = repo;
    }

    /* ------------------------------------------------------------------ */
    /* Consultar: armar la pantalla                                        */
    /* ------------------------------------------------------------------ */

    public async Task<VentasUsadosPantallaDto> ConsultarAsync(int idVenta, CancellationToken ct)
    {
        if (idVenta <= 0)
            throw new ArgumentException("Falta la remisión.");

        var constantes = await _repo.ConsultarConstantesAsync(ct);
        var venta = await _repo.ConsultarVentaAsync(idVenta, ct)
            ?? throw new ConflictException($"La remisión {idVenta} no existe.");

        ValidarQueSePuedaAbrir(venta);

        var cargos = await _repo.ConsultarCargosAsync(idVenta, ct);

        /*
          "EL FOLIO NO TIENE USADOS." Mac31 lo decide por el bloque de CARGOS,
          no por el de creditos (VentasUsados.cs:233-246): si no se le cobro
          ningun casco, no hay nada contra que acreditar y la forma se cierra.
        */
        if (cargos.Renglones.Count == 0)
            throw new ConflictException("El folio no tiene usados.");

        var creditos = await _repo.ConsultarCreditosAsync(idVenta, ct);
        var anteriores = await _repo.ConsultarAnterioresAsync(idVenta, ct);

        return new VentasUsadosPantallaDto
        {
            IdVenta = idVenta,
            Folio = venta.FolioFtm.Length > 0 ? venta.FolioFtm : venta.Folio,
            Cliente = venta.Cliente,
            Empresa = venta.Empresa,
            Agente = venta.Agente,
            FechaPago = venta.FechaPagoFtm,
            /* Con fecha de pago la pantalla es de consulta (VentasUsados.cs:82-94). */
            PuedeGuardar = venta.FechaPagoFtm.Length == 0,
            MaximaDiferenciaUsados = constantes.MaximaDiferenciaUsados,
            PorcentajeIva = constantes.PorcentajeIva,
            Totales = TotalesDe(venta),
            Cargos = cargos,
            Creditos = creditos,
            Anteriores = anteriores
        };
    }

    /*
      Los seis numeros del recuadro de arriba, tal cual los arma Mac31 en
      ConsultarVenta (VentasUsados.cs:177-191). Se copian las dos sumas que NO
      son obvias y que, si se omiten, dejan la pantalla descuadrada contra
      Mac31:

        Creditos   = Creditos + IvaCreditos   (linea 181)
        TotalPagar = TotalPagar + Cargos      (linea 183)

      La segunda existe porque los cargos NO forman parte del total guardado:
      el procedimiento de ajuste se los resta antes de escribirlo
      (sp_n_VentasUsadosCreditoAjuste: "Los cargos no forman parte del total de
      la remision"). Aqui se vuelven a sumar para enseñarlos.
    */
    private static VentasUsadosTotalesVentaDto TotalesDe(VentaDeUsados v) => new()
    {
        Importe = v.Importe,
        Cargos = v.Cargos,
        Creditos = v.Creditos + v.IvaCreditos,
        TotalPagar = v.TotalPagar + v.Cargos,
        Descuentos = v.Descuentos,
        Abonos = v.Abonos,
        Saldo = v.Saldo
    };

    private static void ValidarQueSePuedaAbrir(VentaDeUsados venta)
    {
        /*
          "CV-" es un cambio de vacios, no una venta. Mac31 simplemente no abre
          la pantalla (ConsultarVentas.cs:3546-3547, un return sin mensaje).
          Aqui si se dice, porque un boton que no hace NADA es peor que uno que
          explica.
        */
        if (venta.FolioFtm.Contains("CV-", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("El folio no es una remisión de venta; no tiene usados que modificar.");

        if (venta.Cancelada.Equals("SI", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException($"La remisión {venta.FolioFtm} está cancelada.");
    }

    /* ------------------------------------------------------------------ */
    /* Guardar                                                             */
    /* ------------------------------------------------------------------ */

    public async Task<VentasUsadosGuardarResultadoDto> GuardarAsync(
        VentasUsadosGuardarRequest peticion, int idUsuario, string equipo, CancellationToken ct)
    {
        if (peticion.IdVenta <= 0)
            throw new ArgumentException("Falta la remisión.");
        if (peticion.Renglones.Count == 0)
            throw new ArgumentException("No se recibió ningún renglón de usados.");
        if (peticion.Renglones.Any(r => r.Cantidad < 0))
            throw new ArgumentException("Las cantidades de usados no pueden ser negativas.");

        var constantes = await _repo.ConsultarConstantesAsync(ct);
        var venta = await _repo.ConsultarVentaAsync(peticion.IdVenta, ct)
            ?? throw new ConflictException($"La remisión {peticion.IdVenta} no existe.");

        ValidarQueSePuedaAbrir(venta);

        /*
          Ya pagada = pantalla de consulta. En Mac31 ni siquiera hay boton que
          pulsar (VentasUsados.cs:82-94); aqui se vuelve a comprobar porque el
          navegador puede haber tenido la pantalla abierta desde antes del pago.
        */
        if (venta.FechaPagoFtm.Length > 0)
            throw new ConflictException(
                $"La remisión {venta.FolioFtm} ya está pagada ({venta.FechaPagoFtm}); sus usados ya no se modifican.");

        var cargos = await _repo.ConsultarCargosAsync(peticion.IdVenta, ct);
        if (cargos.Renglones.Count == 0)
            throw new ConflictException("El folio no tiene usados.");

        var creditosActuales = await _repo.ConsultarCreditosAsync(peticion.IdVenta, ct);
        if (creditosActuales.Renglones.Count == 0)
            throw new ConflictException("El folio no tiene renglones de usados que modificar.");

        /*
          LOS COSTOS SALEN DE LA BASE, NO DEL NAVEGADOR

          De la peticion se toma UNICAMENTE la cantidad, y se busca por
          IDTipoUsado sobre los renglones que acaba de devolver el procedimiento.
          Un tipo que el cliente mande y que no este en la venta se ignora: el
          grid de Mac31 ya trae el catalogo completo, asi que no hay nada
          legitimo fuera de esa lista.
        */
        var pedidas = new Dictionary<int, int>();
        foreach (var r in peticion.Renglones) pedidas[r.IdTipoUsado] = r.Cantidad;

        var nuevos = creditosActuales.Renglones
            .Select(r => new RenglonCalculado
            {
                IdTipoUsado = r.IdTipoUsado,
                Costo = r.Costo,
                CostoConIva = r.CostoConIva,
                CantidadActual = r.Cantidad,
                Cantidad = pedidas.TryGetValue(r.IdTipoUsado, out var c) ? c : r.Cantidad
            })
            .ToList();

        /*
          "NO HAY CAMBIOS EN LOS USADOS" (VentasUsados.cs:959-964). Mac31 compara
          la firma del grid nuevo contra la del actual; aqui basta comparar
          cantidad por cantidad, que es lo mismo sin el pegoteo de cadenas.
        */
        if (nuevos.All(r => r.Cantidad == r.CantidadActual))
            throw new ConflictException("No hay cambios en los usados.");

        var calculo = Calcular(nuevos, cargos.TotalTotal, venta, constantes.MaximaDiferenciaUsados);

        if (calculo.Excede)
            throw new ConflictException(calculo.Mensaje);

        /*
          EL TOTAL A PAGAR QUE SE MANDA

          Es lblTotalPagarNuevo de Mac31: Importe + Cargos - CreditosNuevos
          (VentasUsados.cs:795). Se manda CON los cargos dentro porque el
          procedimiento se los resta el solo antes de escribir; mandarlo ya
          restado los quitaria dos veces.
        */
        var totalPagar = venta.Importe + venta.Cargos - calculo.TotalCreditos;
        var saldoNuevo = totalPagar - venta.Descuentos - venta.Abonos;

        /*
          Si aun asi el saldo queda negativo, se pago mas de lo que vale el
          folio y esa diferencia "se la comen" los cascos: sube el total a pagar
          y baja el credito, exactamente como VentasUsados.cs:988-995.
        */
        if (saldoNuevo < 0)
            totalPagar += Math.Abs(saldoNuevo);

        var importeCreditos = constantes.PorcentajeIva > 0
            ? Math.Round(calculo.TotalCreditos / (1 + constantes.PorcentajeIva), 2)
            : calculo.TotalCreditos;

        var ajuste = new AjusteDeUsados
        {
            IdVenta = peticion.IdVenta,
            IdUsuario = idUsuario,
            Equipo = equipo,
            EsquemaPago = constantes.EsquemaPago,
            /*
              Creditos / IvaCreditos van por compatibilidad de firma: los DOS
              procedimientos de ajuste los recalculan antes de usarlos
              (@Creditos = @TotalCreditos / (1+@PorcentajeIVA)), asi que lo que
              se mande aqui no llega a escribirse. Se manda el numero correcto
              de todas formas, para que el parametro no mienta si alguien lee
              el log.
            */
            Creditos = importeCreditos,
            IvaCreditos = calculo.TotalCreditos - importeCreditos,
            TotalPagar = totalPagar
        };

        foreach (var r in nuevos)
        {
            ajuste.IdsTipoUsado.Add(r.IdTipoUsado);
            ajuste.Costos.Add(r.Costo);
            ajuste.Cantidades.Add(r.Cantidad);
            /*
              La cantidad NUEVA es la diferencia contra la que habia, y puede ser
              negativa cuando se bajan usados (VentasUsados.cs:930). Es lo que se
              escribe en [Ventas Usados Creditos Log]: el movimiento, no el saldo.
            */
            ajuste.CantidadesNuevo.Add(r.Cantidad - r.CantidadActual);
            ajuste.Importes.Add(r.CostoConIva * r.Cantidad);
        }

        var resultado = await _repo.AjustarAsync(ajuste, ct);

        /*
          El procedimiento puede negarse por su cuenta (sin existencias de
          usados en el almacen, saldo negativo). Ese "no" tambien es un
          conflicto, no un exito con mensaje.
        */
        if (!resultado.Aplicado)
            throw new ConflictException(
                resultado.Mensaje.Length > 0 ? resultado.Mensaje : "No se pudieron guardar los usados.");

        resultado.Mensaje = calculo.Mensaje.Length > 0
            ? calculo.Mensaje
            : "Los usados han sido registrados.";
        return resultado;
    }

    /* ------------------------------------------------------------------ */
    /* El calculo, que es el corazon de la pantalla                        */
    /* ------------------------------------------------------------------ */

    private sealed class RenglonCalculado
    {
        public int IdTipoUsado { get; init; }
        public decimal Costo { get; init; }
        public decimal CostoConIva { get; init; }
        public int CantidadActual { get; init; }
        public int Cantidad { get; init; }
    }

    private sealed class ResultadoCalculo
    {
        public decimal TotalCreditos { get; set; }
        public bool Excede { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }

    /*
      Es btnAgregarUsadoCredito_Click (VentasUsados.cs:563-780) sin la parte de
      pintar. El orden de las dos comprobaciones NO es intercambiable: primero
      se mide contra los CARGOS y, solo si ahi no hubo excedente, contra el
      SALDO. Mac31 hace `return` en cuanto la primera aplica.
    */
    private static ResultadoCalculo Calcular(
        List<RenglonCalculado> renglones,
        decimal totalUsadosCargo,
        VentaDeUsados venta,
        decimal maximaDiferencia)
    {
        /*
          Total de creditos = suma de (costo CON IVA REDONDEADO) x cantidad, y
          solo de los renglones con cantidad > 0 (VentasUsados.cs:604 y 826).

          El costo con iva NO se calcula aqui: llega ya redondeado a pesos
          enteros desde sp_n_VentasUsadosCargo / sp_n_VentasUsadosCredito, que
          hacen el redondeo "hacia arriba desde .50" sobre Costo*(1+IVA).
          Multiplicar por Costo*(1.16) en lugar de por ese entero deja centavos
          de diferencia contra Mac31 en cada renglon.
        */
        var total = renglones.Where(r => r.Cantidad > 0).Sum(r => r.CostoConIva * r.Cantidad);
        var res = new ResultadoCalculo { TotalCreditos = total };

        /* 1) Contra los CARGOS  (VentasUsados.cs:637-675) */
        if (total > totalUsadosCargo)
        {
            var diferencia = total - totalUsadosCargo;

            if (diferencia >= maximaDiferencia)
            {
                res.Excede = true;
                res.Mensaje =
                    $"El monto de los créditos es de {Pesos(total)} y el de los cargos {Pesos(totalUsadosCargo)}. " +
                    $"Se excede por {Pesos(diferencia)}, y el máximo permitido es {Pesos(maximaDiferencia)}.";
                return res;
            }

            /* Dentro de la tolerancia: se ajusta a los cargos y se avisa. */
            res.TotalCreditos = total - diferencia;
            res.Mensaje =
                $"El monto de los créditos es de {Pesos(total)} y el de los cargos {Pesos(totalUsadosCargo)}. " +
                $"Hubo un excedente y se aplicó un ajuste por {Pesos(diferencia)}.";
            return res;
        }

        /* 2) Contra el SALDO, sin contar los creditos actuales (VentasUsados.cs:692-770) */
        var saldoSinCreditosActuales = venta.Importe + venta.Cargos - venta.Descuentos - venta.Abonos;

        if (total > saldoSinCreditosActuales)
        {
            var diferencia = total - saldoSinCreditosActuales;

            if (diferencia >= maximaDiferencia)
            {
                res.Excede = true;
                res.Mensaje =
                    $"El monto de los créditos es de {Pesos(total)} y el saldo es de {Pesos(venta.Saldo)}. " +
                    $"Se excede por {Pesos(diferencia)}, y el máximo permitido es {Pesos(maximaDiferencia)}.";
                return res;
            }

            res.TotalCreditos = total - diferencia;
            res.Mensaje =
                $"El monto de los créditos es de {Pesos(total)} y el saldo es de {Pesos(venta.Saldo)}. " +
                $"Hubo un excedente y se aplicó un ajuste por {Pesos(diferencia)}.";
            return res;
        }

        return res;
    }

    private static string Pesos(decimal v) => "$ " + v.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
}
