using ISL_Service.Application.DTOs.GenericResponse;
using ISL_Service.Infrastructure.Repositories;

namespace ISL_Service.Application.Services
{
    public class MyService
    {

        private readonly MyRepository _repository;

        public MyService(MyRepository repository)
        {
            _repository = repository;
        }

        public GenericResponseDTO GetById(int id)
        {
            var entity = _repository.GetById(id);
            return entity != null ? new GenericResponseDTO(entity.Id, "Retrieved successfully.") : null;
        }

        //public void Create(MyEntity entity)
        //{
        //    _repository.Insert(entity);
        //}

        //public void Update(MyEntity entity)
        //{
        //    _repository.Update(entity);
        //}

    }
}