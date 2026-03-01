using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactPosApi.DTOs;
using ReactPosApi.Services;

namespace ReactPosApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class StoresController : ControllerBase
{
    private readonly IStoreService _service;
    public StoresController(IStoreService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<DropdownOptionDto>>> GetAll()
        => Ok(await _service.GetAllAsync());
}
