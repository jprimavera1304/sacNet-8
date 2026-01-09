using System.Net;

namespace ISL_Service.Application.DTOs.GenericResponse
{
    public class GenericResponseDataDTO<T>
    {
        public int id { get; set; }

        public HttpStatusCode status { get; set; }

        public T result { get; set; }


        public GenericResponseDataDTO()
        {
        }

        public GenericResponseDataDTO(int id, HttpStatusCode status, ref T result)
        {
            this.id = id;
            this.status = status;
            this.result = result;
        }

    }
}