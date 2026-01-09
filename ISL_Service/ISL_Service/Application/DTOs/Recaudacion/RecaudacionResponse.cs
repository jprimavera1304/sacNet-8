namespace ISL_Service.Application.DTOs.Recaudacion
{
    public class RecaudacionResponse
    {
        public List<RecaudacionDTO> recaudaciones { get; set; }

        public RecaudacionResponse()
        {
        }

        public RecaudacionResponse(List<RecaudacionDTO> recaudaciones)
        {
            this.recaudaciones = recaudaciones;
        }

    }
}