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
