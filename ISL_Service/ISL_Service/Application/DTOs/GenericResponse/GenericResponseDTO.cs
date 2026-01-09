using System.Net;

namespace ISL_Service.Application.DTOs.GenericResponse
{
    public class GenericResponseDTO
    {

        public int id { get; set; }

        public HttpStatusCode status { get; set; }

        public string result { get; set; }


        public GenericResponseDTO()
        {
        }

        public GenericResponseDTO(int id)
        {
            this.id = id;
            this.status = HttpStatusCode.OK;
            this.result = "";
        }

        public GenericResponseDTO(int id, string result)
        {
            this.id = id;
            this.result = "";
        }

        public GenericResponseDTO(int id, HttpStatusCode status, string result)
        {
            this.id = id;
            this.status = status;
            this.result = result;
        }

    }
}