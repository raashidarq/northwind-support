using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Features.Reports.Queries.GetSlaSummary;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController : ControllerBase
{
    /// <summary>
    /// SLA counts, breach rate and average resolution time per agent or per priority, for
    /// tickets created in [from, to). 400 when groupBy is not agent or priority.
    /// </summary>
    [HttpGet("sla-summary")]
    public Task<IReadOnlyList<SlaSummaryRowDto>> SlaSummary(
        [FromQuery] SlaSummaryQuery query,
        [FromServices] GetSlaSummaryQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(query, ct);
}
