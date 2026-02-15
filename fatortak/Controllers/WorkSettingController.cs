using fatortak.Dtos.HR.Settings;
using fatortak.Services.HR.WorkSettingService;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace fatortak.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkSettingController : ControllerBase
    {
        private readonly IWorkSettingService _service;

        public WorkSettingController(IWorkSettingService service)
        {
            _service = service;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get()
        {
            var result = await _service.GetAsync();
            return Ok(result);
        }

        [HttpPost("update/{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkSettingDto dto)
        {
            var result = await _service.UpdateAsync(id, dto);
            return Ok(result);
        }

        [HttpPost("vacation/create")]
        public async Task<IActionResult> CreateVacation([FromBody] CreateGeneralVacationDto dto)
        {
            var result = await _service.CreateVacationAsync(dto);
            return Ok(result);
        }

        [HttpPost("vacation/update/{id}")]
        public async Task<IActionResult> UpdateVacation(Guid id, [FromBody] UpdateGeneralVacationDto dto)
        {
            var result = await _service.UpdateVacationAsync(id, dto);
            return Ok(result);
        }

        [HttpPost("vacation/delete/{id}")]
        public async Task<IActionResult> DeleteVacation(Guid id)
        {
            var result = await _service.DeleteVacationAsync(id);
            return Ok(result);
        }
    }
}
