using Microsoft.EntityFrameworkCore;
using TireScheduler.Api.Database;
using TireScheduler.Api.Errors;

namespace TireScheduler.Api.Modules.MasterData.Products;

// F-MD-2.
internal sealed class ProductRegister(MasterDataDbContext context)
{
    private static readonly Dictionary<string, string> FieldByUniqueIndex = new() { [MasterDataDbContext.ProductCodeIndex] = "code" };

    public async Task<IReadOnlyList<ProductResponse>> ListAsync(CancellationToken cancellationToken) =>
        await context.Products
            .OrderBy(product => product.Code)
            .Select(product => new ProductResponse(product.Id, product.Code, product.Name, product.Spec))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<ProductResponse> GetAsync(int id, CancellationToken cancellationToken) =>
        ToResponse(await FindAsync(id, cancellationToken).ConfigureAwait(false));

    public async Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken cancellationToken)
    {
        Product product = new() { Code = request.Code.Trim(), Name = request.Name.Trim(), Spec = EmptyToNull(request.Spec) };
        context.Products.Add(product);
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken).ConfigureAwait(false);
        return ToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken)
    {
        Product product = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        product.Code = request.Code.Trim();
        product.Name = request.Name.Trim();
        product.Spec = EmptyToNull(request.Spec);
        await context.SaveChangesRejectingDuplicatesAsync(FieldByUniqueIndex, cancellationToken).ConfigureAwait(false);
        return ToResponse(product);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Product product = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        context.Products.Remove(product);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Product> FindAsync(int id, CancellationToken cancellationToken) =>
        await context.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken).ConfigureAwait(false)
        ?? throw ApiProblemException.NotFound("This product");

    private static string? EmptyToNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static ProductResponse ToResponse(Product product) => new(product.Id, product.Code, product.Name, product.Spec);
}
