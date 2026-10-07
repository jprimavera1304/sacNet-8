using ISL_Service.Application.DTOs.Recaudacion;
using Microsoft.Data.SqlClient;
using ISL_Service.Infrastructure.Repositories;

namespace ISL_Service.Application.Services
{
    /*
      EL TICKET DE RECAUDACION QUE SE LEE POR QR, Y NADA MAS.

      Este servicio tiene UN solo llamador en todo el backend:
      RecaudacionesController.Get(qr) — la pantalla publica donde el ciudadano
      escanea el ticket de papel que le acaban de dar. La consulta de
      recaudaciones con los treinta y un filtros de la pantalla de Mac31 NO esta
      aqui: vive en AviacionRecaudacionesController
      (POST api/aviacion/recaudaciones/consultar), que si los aplica todos
      juntos y tiene su propio DTO.

      QUE ESTABA MAL: TRES RAMAS QUE TIRABAN FILTROS, Y NINGUNA ALCANZABLE.

      El metodo copiaba la estructura de Legacy (RecaudacionService.cs:51-80 de
      MacServicios2) con cuatro ramas, y tres de ellas hacian

          recaudacionInputDTO = new RecaudacionInputDTO();   // todo al suelo

      perdiendo FechaInicial, FechaFinal, IDStatus, IDConcepto, IDCuenta,
      IDEconomico, IDPermisionario e IDOperador sin avisar. En Legacy eso al
      menos tenia un motivo —la pantalla manda SIEMPRE el rango de fechas y la
      caja, y al teclear un folio hay que ignorarlos—, pero aqui no hay pantalla
      que mande nada: el unico llamador llena tres campos (IDRecaudacion, IDCaja,
      Dv1) y nunca entra por las otras tres ramas. Eran ramas muertas que solo
      servian para que alguien las copiara manana creyendo que filtran.

      Se quitaron. Queda la unica busqueda que este servicio hace de verdad: un
      ticket por (IDRecaudacion, IDCaja). Si manana hace falta la consulta
      filtrada, ya existe y es la de Aviacion.

      Y EL Dv1 SE RECIBIA Y SE TIRABA — SIENDO EL CANDADO DEL ENDPOINT ANONIMO.

      El doc-comment del controlador promete: "los tres datos tienen que cuadrar
      entre si". No cuadraban. El QR cifrado trae IDRecaudacion|IDCaja|Dv1, el
      controlador los desencriptaba, metia los tres en el DTO... y el repositorio
      no manda @Dv1 al procedimiento (no existe tal parametro), asi que el tercer
      dato no decidia nada: bastaba con que el par (IDRecaudacion, IDCaja)
      existiera. Ahora el digito se compara contra el DV1 que devuelve el propio
      renglon y, si no coincide, no se entrega el ticket.
    */
    public class RecaudacionService
    {

        private readonly RecaudacionRepository recaudacionRepository;

        public RecaudacionService(RecaudacionRepository recaudacionRepository)
        {
            this.recaudacionRepository = recaudacionRepository;
        }

        /// <summary>
        /// El ticket que representa un QR: se busca por (IDRecaudacion, IDCaja) y
        /// se exige que el digito verificador del QR cuadre con el del renglon.
        /// </summary>
        /// <returns>
        /// La lista de renglones del ticket, o una lista vacia si no existe o si
        /// el digito verificador no cuadra. El controlador lo traduce a 404.
        /// </returns>
        /*
          SE QUITO EL `catch (SqlException ex) { throw new Exception(ex.Message); }`

          Relanzaba el mismo texto perdiendo el tipo, y el tipo es lo que el
          ExceptionHandlingMiddleware necesita para distinguir un RAISERROR de
          negocio de un error del motor y para decidir si el detalle tecnico se
          ensena o solo se registra. Sin el catch, la SqlException sube entera.
        */
        public List<RecaudacionDTO> GetById(RecaudacionInputDTO recaudacionInputDTO)
        {
            {
                // Busqueda por un IDRecaudacion y su IDCaja. Los demas campos del
                // DTO no se tocan: el constructor los deja en 0 / "" y asi viajan
                // al procedimiento, que es "sin filtro".
                //
                // OJO con el detalle que Legacy tenia mal aqui: su rama armaba
                // IDsRecaudacion con string.Join de la LISTA, que en este caso
                // viene vacia, asi que mandaba cadena vacia e id 0 — el
                // procedimiento sin filtros y de vuelta TODAS las recaudaciones
                // de la base. Pedir una y recibir todas. Aqui se manda el id que
                // se pidio.
                recaudacionInputDTO.IDsRecaudacion = recaudacionInputDTO.IDRecaudacion.ToString();

                var renglones = recaudacionRepository.GetById(recaudacionInputDTO);

                return FiltrarPorDigitoVerificador(renglones, recaudacionInputDTO.Dv1);
            }
        }

        /// <summary>
        /// El tercer dato del QR tiene que cuadrar con el del renglon.
        /// </summary>
        /// <remarks>
        /// Se compara como NUMERO y no como texto porque los dos lados lo
        /// guardan distinto: del QR sale un int y en la base DV1 es varchar, asi
        /// que "07" y 7 son el mismo digito y no deben parecer distintos. Si el
        /// renglon no trae DV1 (bases donde la columna no existe, como
        /// Produccion_svr) no se puede comprobar nada: se deja pasar en vez de
        /// tumbar la consulta de tickets de una instalacion entera, y es el mismo
        /// grado de proteccion que habia antes.
        /// </remarks>
        private static List<RecaudacionDTO> FiltrarPorDigitoVerificador(List<RecaudacionDTO> renglones, int dv1DelQr)
        {
            if (renglones == null || renglones.Count == 0)
                return new List<RecaudacionDTO>();

            var dv1DelRenglon = (renglones[0].DV1 ?? string.Empty).Trim();
            if (dv1DelRenglon.Length == 0)
                return renglones;

            if (!int.TryParse(dv1DelRenglon, out var dv1Guardado) || dv1Guardado != dv1DelQr)
                return new List<RecaudacionDTO>();

            return renglones;
        }

    }

}
