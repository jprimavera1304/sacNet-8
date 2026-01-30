using ISL_Service.Application.DTOs.ProveedoresPagos;
using ISL_Service.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Application.Services
{
    public class ProveedoresPagosService
    {
        private readonly ProveedoresPagosRepository proveedoresPagosRepository;

        public ProveedoresPagosService(ProveedoresPagosRepository proveedoresPagosRepository)
        {
            this.proveedoresPagosRepository = proveedoresPagosRepository;
        }

        public List<ProveedorPagoDTO> Consultar(ProveedorPagoInputDTO input)
        {
            try
            {
                return proveedoresPagosRepository.Consultar(input);
            }
            catch (SqlException ex)
            {
                throw new Exception(ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }


        public int Insertar(ProveedorPagoInputDTO input)
        {
            try
            {
                if (input.IDPersona <= 0) throw new Exception("IDPersona es requerido.");
                //if (input.IDProveedor <= 0) throw new Exception("IDProveedor es requerido.");

                if (input.IDUsuario <= 0) throw new Exception("IDUsuario es requerido.");
                if (string.IsNullOrWhiteSpace(input.Equipo)) throw new Exception("Equipo es requerido.");
                if (string.IsNullOrWhiteSpace(input.FechaPago)) throw new Exception("FechaPago es requerido.");

                return proveedoresPagosRepository.Insertar(input);
            }
            catch (SqlException ex)
            {
                throw new Exception(ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }


        public void Cancelar(ProveedorPagoInputDTO input)
        {
            try
            {
                if (input.IDProveedorPago <= 0) throw new Exception("IDProveedorPago es requerido.");
                if (input.IDUsuarioCancelacion <= 0) throw new Exception("IDUsuarioCancelacion es requerido.");
                if (string.IsNullOrWhiteSpace(input.EquipoCancelacion)) throw new Exception("EquipoCancelacion es requerido.");

                proveedoresPagosRepository.Cancelar(input);
            }
            catch (SqlException ex)
            {
                throw new Exception(ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public void Actualizar(ProveedorPagoInputDTO input)
        {
            try
            {
                if (input.IDProveedorPago <= 0) throw new Exception("IDProveedorPago es requerido.");
                if (input.IDUsuarioModificacion <= 0) throw new Exception("IDUsuarioModificacion es requerido.");
                if (string.IsNullOrWhiteSpace(input.EquipoModificacion)) throw new Exception("EquipoModificacion es requerido.");

                proveedoresPagosRepository.Actualizar(input);
            }
            catch (SqlException ex)
            {
                throw new Exception(ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
