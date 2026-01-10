using System.Diagnostics;
using System.Net;
using ISL_Service.Utils;
using ISL_Service.Application.DTOs.Recaudacion;
using ISL_Service.Application.DTOs.GenericHttpResponse;
using ISL_Service.Infrastructure.Repositories.RecaudacionRepo;
using Microsoft.Data.SqlClient;

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
                    // Busqueda por un IDRecaudacion
                    int IDRecaudacion = recaudacionInputDTO.IDRecaudacion;
                    recaudacionInputDTO = new RecaudacionInputDTO();
                    recaudacionInputDTO.IDsRecaudacion = IDRecaudacion.ToString();
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


        public async Task<HttpResponseMessage> findAsync(RecaudacionInputDTO recaudacionInputDTO, HttpRequestMessage request)
        {

            try
            {
                ////// Se permite cualquier Token (App/ClienteWeb/Usuario)
                ////JwtToken jwtToken = await JwtTokenGenerator.validateJwtTokenAsync(recaudacionInputDTO.token, recaudacionInputDTO.correoElectronicoToken,
                ////                                                                  Enums.enumToken.Todos);
                ////if (!jwtToken.valido)
                ////    return GenericHttpResponse.Response(request, HttpStatusCode.Unauthorized, "No Autorizado");


                ////if (!await constanteRepository.spConsultar(new ConstanteInputDTO()))
                ////    return GenericHttpResponse.Response(request, HttpStatusCode.InternalServerError,
                ////                                        "[No se pudo procesar " + constanteRepository.sp + "] - " + constanteRepository.msgError);

                ////string formatoFechaDB = constanteRepository.dtConstantes.Rows[0]["formatoFechaDB"].ToString();


                // Busqueda por uno o vario IDsRecaudacion
                if ((recaudacionInputDTO.IDsRecaudacionLst.Count > 0) || (recaudacionInputDTO.IDRecaudacion > 0))
                {
                    string IDsRecaudacion = string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, recaudacionInputDTO.IDsRecaudacionLst);

                    recaudacionInputDTO = new RecaudacionInputDTO();
                    recaudacionInputDTO.IDsRecaudacion = IDsRecaudacion;
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


                //if (!await recaudacionRepository.spConsultar(recaudacionInputDTO))
                //    return GenericHttpResponse.Response(request, HttpStatusCode.InternalServerError,
                //                                        "[No se pudo procesar " + recaudacionRepository.sp + "] - " + recaudacionRepository.msgError);

                RecaudacionResponse r = new RecaudacionResponse(recaudacionRepository.recaudacionDTOList);

                //return GenericHttpResponse.Response(request, HttpStatusCode.OK, r);

                //return GenericHttpResponse.Response(request, HttpStatusCode.OK, new RecaudacionResponse(recaudacionRepository.recaudacionDTOList));

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("============= Exception =============");
                Debug.WriteLine(ex.ToString());
                return GenericHttpResponse.Response(request, HttpStatusCode.BadRequest, ex.ToString());
            }
        }

    }

}