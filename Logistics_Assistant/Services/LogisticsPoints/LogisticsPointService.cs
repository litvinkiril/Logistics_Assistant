using Logistics_Assistant.Data;
using Logistics_Assistant.Models;
using Microsoft.EntityFrameworkCore;

namespace Logistics_Assistant.Services.LogisticsPoints
{
    public class LogisticsPointService
    {
        private readonly ApplicationDbContext _db;

        public LogisticsPointService(ApplicationDbContext db)
        {
            _db = db;
        }

        public Task<List<LogisticsPoint>> GetPointsAsync(
            string? type,
            CancellationToken cancellationToken)
        {
            return SearchPointsAsync(
                query: null,
                type: type,
                cancellationToken: cancellationToken);
        }

        public Task<LogisticsPoint?> GetPointByIdAsync(
            long id,
            CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

            return _db.LogisticsPoints
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    point => point.Id == id,
                    cancellationToken);
        }

        public Task<List<LogisticsPoint>> SearchPointsAsync(
            string? query,
            string? type,
            CancellationToken cancellationToken)
        {
            var points = _db.LogisticsPoints.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(type))
            {
                var pointType = type.Trim();

                points = points.Where(
                    point => point.Type == pointType);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var text = query.Trim()
                    .Replace("\\", "\\\\")
                    .Replace("%", "\\%")
                    .Replace("_", "\\_");

                var pattern = $"%{text}%";

                points = points.Where(point =>
                    EF.Functions.ILike(point.Name, pattern, "\\") ||
                    EF.Functions.ILike(point.City, pattern, "\\") ||
                    EF.Functions.ILike(point.Address, pattern, "\\"));
            }

            return points
                .OrderBy(point => point.Name)
                .ThenBy(point => point.Id)
                .ToListAsync(cancellationToken);
        }
    }
}