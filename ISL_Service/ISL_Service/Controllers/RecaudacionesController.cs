using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ISL_Service.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecaudacionesController : ControllerBase
    {
        // GET: api/<RecaudacionesController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<RecaudacionesController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<RecaudacionesController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<RecaudacionesController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<RecaudacionesController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
