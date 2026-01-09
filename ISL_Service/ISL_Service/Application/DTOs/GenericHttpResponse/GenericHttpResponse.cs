using ISL_Service.Application.DTOs.GenericResponse;
using System.Net;
using System.Net.Http;

namespace ISL_Service.Application.DTOs.GenericHttpResponse
{
    public static class GenericHttpResponse
    {
        public static int Id { get; set; }
        public static HttpStatusCode StatusCode { get; set; }
        public static string Message { get; set; }



        public static HttpResponseMessage Response(HttpRequestMessage request, HttpStatusCode httpStatusCode, string message)
        {
            int id = 0;
            //return request.CreateResponse(HttpStatusCode.OK, new GenericResponseDTO(id, httpStatusCode, message));
            return null;
        }

        //public static HttpResponseMessage Response(HttpRequestMessage request, HttpStatusCode httpStatusCode, int id)
        //{
        //    string message = "";
        //    return request.CreateResponse(HttpStatusCode.OK, new GenericResponseDTO(id, httpStatusCode, message));
        //}

        //public static HttpResponseMessage Response(HttpRequestMessage request, HttpStatusCode httpStatusCode, int id, string message)
        //{
        //    return request.CreateResponse(HttpStatusCode.OK, new GenericResponseDTO(id, httpStatusCode, message));
        //}

        //public static HttpResponseMessage Response(HttpRequestMessage request, HttpStatusCode httpStatusCode, dynamic data)
        //{
        //    int id = 0;
        //    return request.CreateResponse(HttpStatusCode.OK,
        //                                  new GenericResponseDataDTO<dynamic>(id, httpStatusCode, ref data));
        //}

        //public static HttpResponseMessage Response(HttpRequestMessage request, HttpStatusCode httpStatusCode, int id, dynamic data)
        //{
        //    return request.CreateResponse(HttpStatusCode.OK,
        //                                  new GenericResponseDataDTO<dynamic>(id, httpStatusCode, ref data));
        //}

    }
}