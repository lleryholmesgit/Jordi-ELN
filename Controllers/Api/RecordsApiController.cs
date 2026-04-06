using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicLabNotebook.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/records")]
public sealed class RecordsApiController : ApiControllerBase
{
    private readonly IRecordService _recordService;
    private readonly UserManager<ApplicationUser> _userManager;

    public RecordsApiController(IRecordService recordService, UserManager<ApplicationUser> userManager)
    {
        _recordService = recordService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] RecordStatus? status, CancellationToken cancellationToken)
    {
        var items = await _recordService.SearchAsync(new RecordSearchRequest { Query = query ?? string.Empty, Status = status }, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _recordService.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    public async Task<IActionResult> Create([FromBody] RecordSaveRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        var entity = await _recordService.CreateAsync(request, actorUserId, "Api", null, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    public async Task<IActionResult> Update(int id, [FromBody] RecordSaveRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        var entity = await _recordService.UpdateAsync(id, request, actorUserId, "Api", null, cancellationToken);
        return entity is null ? BadRequest(new { message = "The record cannot be edited in its current state." }) : Ok(entity);
    }

    [HttpPost("{id:int}/submit")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    public async Task<IActionResult> Submit(int id, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        return await _recordService.SubmitAsync(id, actorUserId, "Api", cancellationToken)
            ? Ok(new { message = "Record submitted." })
            : BadRequest(new { message = "The record could not be submitted." });
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Reviewer)]
    public async Task<IActionResult> Approve(int id, [FromBody] RecordReviewRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        return await _recordService.ApproveAsync(id, actorUserId, request.Comment ?? string.Empty, "Api", cancellationToken)
            ? Ok(new { message = "Record approved." })
            : BadRequest(new { message = "The record could not be approved." });
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Reviewer)]
    public async Task<IActionResult> Reject(int id, [FromBody] RecordReviewRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        return await _recordService.RejectAsync(id, actorUserId, request.Comment ?? string.Empty, "Api", cancellationToken)
            ? Ok(new { message = "Record rejected." })
            : BadRequest(new { message = "The record could not be rejected." });
    }
}
