using ISL_Service.Utils;
using ISL_Service.Application.DTOs.Recaudacion;
using Microsoft.Data.SqlClient;
using ISL_Service.Infrastructure.Repositories;

namespace ISL_Service.Application.Services
{
    public class RecaudacionService
    {

        private readonly RecaudacionRepository recaudacionRepository;

        public RecaudacionService(RecaudacionRepository recaudacionRepository)
        {
            this.recaudacionRepository = recaudacionRepository;
        }

        public List<RecaudacionDTO> GetById(RecaudacionInputDTO recaudacionInputDTO)
        {

            try
            {

                if (recaudacionInputDTO.IDsRecaudacionLst.Count > 0)
                {
                    // Busqueda por uno o varios IDRecaudacion
                    string IDsRecaudacion = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, recaudacionInputDTO.IDsRecaudacionLst);

                    recaudacionInputDTO = new RecaudacionInputDTO();
                    recaudacionInputDTO.IDsRecaudacion = IDsRecaudacion;
                }

                else if (recaudacionInputDTO.IDRecaudacion > 0)
                {
                    // Busqueda por un IDRecaudacion e IDCaja
                    int IDRecaudacion = recaudacionInputDTO.IDRecaudacion;
                    int IDCaja = recaudacionInputDTO.IDCaja;

                    recaudacionInputDTO = new RecaudacionInputDTO();
                    recaudacionInputDTO.IDsRecaudacion = IDRecaudacion.ToString();
                    recaudacionInputDTO.IDCaja = IDCaja;
                }

                // Si es por folio, entonces se ignoran todos los demas filtros, en otro caso si se toman los filtros
                else if (recaudacionInputDTO.FolioInicial != 0)
                {
                    int FolioInicial = recaudacionInputDTO.FolioInicial;
                    int FolioFinal = recaudacionInputDTO.FolioFinal;

                    recaudacionInputDTO = new RecaudacionInputDTO();
                    recaudacionInputDTO.FolioInicial = FolioInicial;
                    recaudacionInputDTO.FolioFinal = FolioFinal;
                }

                else

                {

                    //if (recaudacionInputDTO.FechaInicial != "")
                    //{
                    //    recaudacionInputDTO.FechaInicial = Funciones.ConvertFtmDate(formatoFechaDB, recaudacionInputDTO.FechaInicial) + " 00:00:00";
                    //    recaudacionInputDTO.FechaFinal = Funciones.ConvertFtmDate(formatoFechaDB, recaudacionInputDTO.FechaFinal) + " 23:59:59";
                    //}

                    recaudacionInputDTO.IDsRecaudacion = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, recaudacionInputDTO.IDsRecaudacionLst);
                    recaudacionInputDTO.IDsCaja = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, recaudacionInputDTO.IDsCajaLst);
                    recaudacionInputDTO.IDsConcepto = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, recaudacionInputDTO.IDsConceptoLst);
                    recaudacionInputDTO.IDsCuenta = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, recaudacionInputDTO.IDsCuentaLst);
                    recaudacionInputDTO.IDsEconomico = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, recaudacionInputDTO.IDsEconomicoLst);
                    recaudacionInputDTO.IDsPermisionario = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, recaudacionInputDTO.IDsPermisionarioLst);
                    recaudacionInputDTO.IDsOperador = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, recaudacionInputDTO.IDsOperadorLst);
                }


                return recaudacionRepository.GetById(recaudacionInputDTO);
            }
            catch (SqlException ex) // Manejo de errores de conexión y SP
            {
                // Aquí podrías registrar el error si es necesario
                throw new Exception(ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

        }

    }

}