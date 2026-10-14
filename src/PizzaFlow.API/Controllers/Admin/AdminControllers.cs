using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PizzaFlow.API.Extensions;
using PizzaFlow.Application.Common;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Application.Services;
using PizzaFlow.Domain.Enums;

namespace PizzaFlow.API.Controllers.Admin;

[ApiController, AllowAnonymous, Route("api/admin/auth")]
[Tags("Admin - Autenticação")]
public class AdminAuthController(AuthService service) : ControllerBase
{
    /// <summary>Login do dono/administrador. Retorna o JWT para o botão Authorize do Swagger.</summary>
    [HttpPost("login"), EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct) =>
        await service.LoginAsync(request, ct);
}

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/store")]
[Tags("Admin - Loja")]
public class AdminStoreController(StoreService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<StoreInfoDto>> Get(CancellationToken ct) => await service.GetAdminAsync(ct);

    /// <summary>Abrir/fechar a loja, taxa de entrega, pedido mínimo.</summary>
    [HttpPut] public async Task<ActionResult<StoreInfoDto>> Update(UpdateStoreRequest r, CancellationToken ct) => await service.UpdateAsync(r, ct);
}

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/categories")]
[Tags("Admin - Categorias")]
public class AdminCategoriesController(CategoryService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest r, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(r, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, CategoryRequest r, CancellationToken ct) => await service.UpdateAsync(id, r, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/promotions")]
[Tags("Admin - Promoções")]
public class AdminPromotionsController(PromotionService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<PromotionDto>>> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<PromotionDto>> Create(PromotionRequest r, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(r, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PromotionDto>> Update(Guid id, PromotionRequest r, CancellationToken ct) => await service.UpdateAsync(id, r, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/products")]
[Tags("Admin - Produtos")]
public class AdminProductsController(ProductService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> List([FromQuery] string? search, [FromQuery] Guid? categoryId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await service.ListAsync(search, categoryId, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct) => await service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(ProductRequest r, CancellationToken ct)
    {
        var created = await service.CreateAsync(r, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Atualiza dados e preço. O estoque é alterado apenas por /stock.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, ProductRequest r, CancellationToken ct) => await service.UpdateAsync(id, r, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }

    /// <summary>Entrada (+) ou saída (-) manual de estoque, com rastreabilidade.</summary>
    [HttpPost("{id:guid}/stock")]
    public async Task<ActionResult<ProductDto>> AdjustStock(Guid id, StockAdjustRequest r, CancellationToken ct) =>
        await service.AdjustStockAsync(id, r, ct);

    /// <summary>Upload de foto (JPG/PNG/WEBP até 10 MB) ou vídeo (MP4/WEBM até 50 MB). Vai para a nuvem; só a URL é salva.</summary>
    [HttpPost("{id:guid}/media"), Consumes("multipart/form-data"), RequestSizeLimit(55_000_000)]
    public async Task<ActionResult<MediaDto>> UploadMedia(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var media = await service.AddMediaAsync(id, stream, file.ContentType, file.Length, ct);
        return StatusCode(StatusCodes.Status201Created, media);
    }

    [HttpDelete("{id:guid}/media/{mediaId:guid}")]
    public async Task<IActionResult> DeleteMedia(Guid id, Guid mediaId, CancellationToken ct)
    {
        await service.RemoveMediaAsync(id, mediaId, ct);
        return NoContent();
    }
}

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/orders")]
[Tags("Admin - Pedidos")]
public class AdminOrdersController(OrderService service) : ControllerBase
{
    /// <summary>Lista para o Kanban. Sem filtro, retorna todos exceto "aguardando pagamento".</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminOrderListItemDto>>> List([FromQuery] OrderStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        await service.ListAsync(status, page, pageSize, ct);

    /// <summary>Pedidos pagos ainda não vistos (badge do sino ao carregar a página).</summary>
    [HttpGet("unseen")]
    public async Task<ActionResult<IReadOnlyList<AdminOrderListItemDto>>> Unseen(CancellationToken ct) => Ok(await service.ListUnseenAsync(ct));

    /// <summary>Detalhes completos do cliente e do pedido (modal do sino).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminOrderDetailDto>> Get(Guid id, CancellationToken ct) => await service.GetAsync(id, ct);

    [HttpPost("{id:guid}/seen")]
    public async Task<IActionResult> MarkSeen(Guid id, CancellationToken ct) { await service.MarkSeenAsync(id, ct); return NoContent(); }

    /// <summary>Recebido → Em preparo → Saiu para entrega / Pronto para retirada → Finalizado. Cancelar devolve o estoque.</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<AdminOrderDetailDto>> ChangeStatus(Guid id, ChangeStatusRequest r, CancellationToken ct) =>
        await service.ChangeStatusAsync(id, r.Status, ct);
}

[ApiController, Authorize(Roles = "Admin"), Route("api/admin/reports")]
[Tags("Admin - Relatórios")]
public class AdminReportsController(ReportService service) : ControllerBase
{
    /// <summary>Faturamento, nº de pedidos, ticket médio, série diária, top produtos, status e estoque baixo. Padrão: últimos 30 dias.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryDto>> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        await service.GetSummaryAsync(from, to, ct);
}
