using Microsoft.Extensions.Logging;
using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.Common;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Common;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Enums;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Application.Services;

public class ProductService(IProductRepository products, ICategoryRepository categories, IPromotionRepository promotions,
    IMediaStorage storage, ICurrentUser user, IUnitOfWork uow, ILogger<ProductService> logger)
{
    private static readonly Dictionary<string, MediaType> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = MediaType.Image, ["image/png"] = MediaType.Image, ["image/webp"] = MediaType.Image,
        ["video/mp4"] = MediaType.Video, ["video/webm"] = MediaType.Video,
    };
    private const long MaxImageBytes = 10L * 1024 * 1024;
    private const long MaxVideoBytes = 50L * 1024 * 1024;
    private const int MaxMediaPerProduct = 10;

    public async Task<PagedResult<ProductDto>> ListAsync(string? search, Guid? categoryId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var term = TextNormalizer.Normalize(search);
        var result = await products.ListAsync(user.StoreId, term.Length == 0 ? null : term, categoryId, page, pageSize, ct);
        var now = DateTime.UtcNow;
        return new PagedResult<ProductDto>(result.Items.Select(p => p.ToAdminDto(now)).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<ProductDto> GetAsync(Guid id, CancellationToken ct) =>
        (await Load(id, ct)).ToAdminDto(DateTime.UtcNow);

    public async Task<ProductDto> CreateAsync(ProductRequest r, CancellationToken ct)
    {
        await EnsureReferencesAsync(r, ct);
        var p = new Product
        {
            StoreId = user.StoreId, CategoryId = r.CategoryId, Name = r.Name, Description = r.Description?.Trim(),
            Price = r.Price, PromotionId = r.PromotionId, TrackStock = r.TrackStock,
            StockQuantity = r.TrackStock ? r.StockQuantity : 0, LowStockAlert = r.LowStockAlert, IsActive = r.IsActive
        };
        products.Add(p);
        if (p.TrackStock && p.StockQuantity > 0)
            products.AddStockMovement(new StockMovement { ProductId = p.Id, Quantity = p.StockQuantity, Reason = StockReason.Restock });
        await uow.SaveChangesAsync(ct);
        return (await Load(p.Id, ct)).ToAdminDto(DateTime.UtcNow);
    }

    /// <summary>Não altera a quantidade em estoque (use AdjustStockAsync, que gera rastreabilidade).</summary>
    public async Task<ProductDto> UpdateAsync(Guid id, ProductRequest r, CancellationToken ct)
    {
        await EnsureReferencesAsync(r, ct);
        var p = await Load(id, ct);
        p.CategoryId = r.CategoryId; p.Name = r.Name; p.Description = r.Description?.Trim();
        p.Price = r.Price; p.PromotionId = r.PromotionId; p.TrackStock = r.TrackStock;
        p.LowStockAlert = r.LowStockAlert; p.IsActive = r.IsActive;
        await uow.SaveChangesAsync(ct);
        return (await Load(id, ct)).ToAdminDto(DateTime.UtcNow);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var p = await Load(id, ct);
        foreach (var m in p.Media.Where(m => m.PublicId is not null))
        {
            try { await storage.DeleteAsync(m.PublicId!, m.MediaType, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Falha ao remover mídia {PublicId} da nuvem", m.PublicId); }
        }
        products.Remove(p);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<ProductDto> AdjustStockAsync(Guid id, StockAdjustRequest r, CancellationToken ct)
    {
        var p = await Load(id, ct);
        if (!p.TrackStock) throw new DomainException("Este produto não controla estoque.");
        if (p.StockQuantity + r.Quantity < 0) throw new DomainException("O ajuste deixaria o estoque negativo.");
        p.StockQuantity += r.Quantity;
        products.AddStockMovement(new StockMovement
        {
            ProductId = p.Id, Quantity = r.Quantity, Reason = r.Quantity > 0 ? StockReason.Restock : StockReason.Adjustment
        });
        await uow.SaveChangesAsync(ct);
        return p.ToAdminDto(DateTime.UtcNow);
    }

    // ---------- mídia (somente URL no banco; arquivo vai para a nuvem) ----------
    public async Task<MediaDto> AddMediaAsync(Guid productId, Stream content, string contentType, long length, CancellationToken ct)
    {
        if (!AllowedContentTypes.TryGetValue(contentType, out var type))
            throw new DomainException("Formato não suportado. Use JPG, PNG, WEBP, MP4 ou WEBM.");
        if (length <= 0) throw new DomainException("Arquivo vazio.");
        if (length > (type == MediaType.Image ? MaxImageBytes : MaxVideoBytes))
            throw new DomainException(type == MediaType.Image ? "Imagem acima de 10 MB." : "Vídeo acima de 50 MB.");

        var p = await Load(productId, ct);
        if (p.Media.Count >= MaxMediaPerProduct) throw new DomainException($"Máximo de {MaxMediaPerProduct} mídias por produto.");

        var uploaded = await storage.UploadAsync(content, type, user.StoreId, ct);
        var media = new ProductMedia
        {
            ProductId = p.Id, MediaType = type, Url = uploaded.Url, PublicId = uploaded.PublicId,
            SortOrder = p.Media.Count == 0 ? 0 : p.Media.Max(m => m.SortOrder) + 1
        };
        products.AddMedia(media);
        try { await uow.SaveChangesAsync(ct); }
        catch
        {
            try { await storage.DeleteAsync(uploaded.PublicId, type, CancellationToken.None); } catch { /* best effort */ }
            throw;
        }
        return media.ToDto();
    }

    public async Task RemoveMediaAsync(Guid productId, Guid mediaId, CancellationToken ct)
    {
        var p = await Load(productId, ct);
        var media = p.Media.FirstOrDefault(m => m.Id == mediaId) ?? throw new NotFoundException("Mídia não encontrada.");
        if (media.PublicId is not null)
        {
            try { await storage.DeleteAsync(media.PublicId, media.MediaType, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Falha ao remover mídia {PublicId} da nuvem", media.PublicId); }
        }
        products.RemoveMedia(media);
        await uow.SaveChangesAsync(ct);
    }

    // ---------- helpers ----------
    private async Task<Product> Load(Guid id, CancellationToken ct) =>
        await products.GetAsync(user.StoreId, id, ct) ?? throw new NotFoundException("Produto não encontrado.");

    private async Task EnsureReferencesAsync(ProductRequest r, CancellationToken ct)
    {
        if (await categories.GetAsync(user.StoreId, r.CategoryId, ct) is null)
            throw new DomainException("Categoria inválida.");
        if (r.PromotionId is { } promoId && await promotions.GetAsync(user.StoreId, promoId, ct) is null)
            throw new DomainException("Promoção inválida.");
    }
}
