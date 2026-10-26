using Logistics_Assistant.Data;
using Logistics_Assistant.Models;
using Microsoft.EntityFrameworkCore;

namespace Logistics_Assistant.Services.Products
{
    public class ProductSearchService
    {
        private const int PageSize = 10;
        private readonly ApplicationDbContext _db;

        public ProductSearchService(ApplicationDbContext db)
        {
            _db = db;
        }

        public Task<List<Product>> SearchByIdAsync(
            long id,
            CancellationToken cancellationToken)
        {
            return _db.Products
                .AsNoTracking()
                .Where(product => product.Id == id)
                .ToListAsync(cancellationToken);
        }

        public Task<List<Product>> SearchByNameAsync(
            string name,
            int offset,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);

            var searchName = name.Trim();

            return _db.Products
                .AsNoTracking()
                .Where(product =>
                    EF.Functions.ILike(product.Name, $"%{searchName}%"))
                .OrderBy(product => product.Id)
                .Skip(offset)
                .Take(PageSize)
                .ToListAsync(cancellationToken);
        }

        public Task<List<Product>> SearchByUrlAsync(
            string url,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            var searchUrl = url.Trim();

            return _db.Products
                .AsNoTracking()
                .Where(product => product.Url == searchUrl)
                .OrderBy(product => product.Id)
                .Take(PageSize)
                .ToListAsync(cancellationToken);
        }
    }
}
