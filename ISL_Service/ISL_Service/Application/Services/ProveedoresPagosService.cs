using ISL_Service.Application.DTOs.ProveedoresPagos;
using ISL_Service.Infrastructure.Repositories;

namespace ISL_Service.Application.Services
{
    /*
      SE QUITARON LOS OCHO `catch (SqlException ex) { throw new Exception(ex.Message); }`

      No atrapaban nada: volvian a lanzar el mismo texto, pero PERDIENDO el tipo.
      Y el tipo era lo unico que importaba, porque el
      ExceptionHandlingMiddleware mira `SqlException` para dos cosas:

        1. distinguir un RAISERROR de negocio (numeros 50000-99999, que debe
           llegar al usuario con su texto y un 400) de un error del motor (que
           debe salir como 500 generico);
        2. decidir si el `detail` tecnico se ensena o se guarda.

      Convertido a `Exception` generico, todo caia al catch de abajo: los
      mensajes de negocio de los SP se perdian y los nombres de procedimiento del
      motor se devolvian al cliente. Sin el catch, la SqlException sube con su
      tipo y el middleware hace su trabajo.

      LAS VALIDACIONES DE AUTORIA TAMBIEN SE FUERON. Exigian IDUsuario y Equipo
      del cuerpo; ahora los pone el controlador desde el token, asi que pedirlos
      al cliente no tenia sentido y ademas documentaba la falla como si fuera un
      contrato.
    */
    public class ProveedoresPagosService
    {
        private readonly ProveedoresPagosRepository proveedoresPagosRepository;

        public ProveedoresPagosService(ProveedoresPagosRepository proveedoresPagosRepository)
        {
            this.proveedoresPagosRepository = proveedoresPagosRepository;
        }

        public List<ProveedorPagoDTO> Consultar(ProveedorPagoInputDTO input)
        {
            return proveedoresPagosRepository.Consultar(input);
        }

        public int Insertar(ProveedorPagoInputDTO input)
        {
            if (input.IDPersona <= 0) throw new ArgumentException("IDPersona es requerido.");
            if (string.IsNullOrWhiteSpace(input.FechaPago)) throw new ArgumentException("FechaPago es requerido.");

            return proveedoresPagosRepository.Insertar(input);
        }

        public void Cancelar(ProveedorPagoInputDTO input)
        {
            if (input.IDProveedorPago <= 0) throw new ArgumentException("IDProveedorPago es requerido.");

            proveedoresPagosRepository.Cancelar(input);
        }

        public void Actualizar(ProveedorPagoInputDTO input)
        {
            if (input.IDProveedorPago <= 0) throw new ArgumentException("IDProveedorPago es requerido.");

            proveedoresPagosRepository.Actualizar(input);
        }
    }
}
