using System.Net;

namespace ISL_Service.Application.DTOs.GenericRequest
{
    public class GenericRequestDataDTO
    {
        public int id { get; set; }

        public HttpStatusCode status { get; set; }

        public string result { get; set; }


        public GenericRequestDataDTO()
        {
        }

        public GenericRequestDataDTO(int id, HttpStatusCode status, string result)
        {
            this.id = id;
            this.status = status;
            this.result = result;
        }

    }
}