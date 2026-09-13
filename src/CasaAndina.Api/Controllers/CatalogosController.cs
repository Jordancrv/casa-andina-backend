using CasaAndina.Application.Catalogos.DTOs;
using CasaAndina.Application.Catalogos.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CasaAndina.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatalogosController : ControllerBase
{
    private readonly ISender _sender;

    public CatalogosController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<ActionResult<CatalogosResponse>> GetCatalogos()
    {
        var response = await _sender.Send(new GetCatalogosQuery());
        return Ok(response);
    }
}
