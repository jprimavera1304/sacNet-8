using System.Data;

namespace ISL_Service.Application.DTOs.GenericResponse
{
    public class GenericResponse
    {
        public List<DataRow> tablaLst { get; set; }

        public string IDResult { get; set; }
        public int result { get; set; }
        public string mensaje { get; set; }
        public string mensajeFacturama { get; set; }


        public GenericResponse()
        {
        }

        public GenericResponse(int result, string mensaje)
        {
            this.result = result;
            this.mensaje = mensaje;
        }

        public GenericResponse(dynamic obj)
        {
            this.IDResult = obj.IDResult;
            this.result = obj.result;
            this.mensaje = obj.mensaje;
        }

        public GenericResponse(string IDResult, int result, string mensaje)
        {
            this.IDResult = IDResult;
            this.result = result;
            this.mensaje = mensaje;
        }

        public GenericResponse(List<DataRow> tablaLst)
        {
            this.tablaLst = tablaLst;
        }

        public GenericResponse(string mensajeFacturama)
        {
            this.mensajeFacturama = mensajeFacturama;
        }

    }
}