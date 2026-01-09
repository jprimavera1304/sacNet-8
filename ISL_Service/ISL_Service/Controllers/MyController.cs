using ISL_Service.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MyController : Controller
    {
        private readonly MyService _service;

        public MyController(MyService service)
        {
            _service = service;
        }

        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var result = _service.GetById(id);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
