using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Persistence;

/// <summary>
/// Query shapes that every provider can run. SQLite cannot translate
/// ordering or range comparisons over <see cref="DateTimeOffset"/>, so on
/// SQLite the remaining filters still execute in SQL and only the
/// timestamp ordering, paging or predicate is applied locally. Every other
/// provider runs the whole query server-side.
/// </summary>
internal static class PortableQueryableExtensions
{
    extension<T>(IQueryable<T> query)
    {
        /// <summary>
        /// Orders by <paramref name="key"/>, optionally pages, and
        /// materialises the rows.
        /// </summary>
        public async Task<List<T>> ToListOrderedAsync<TKey>(
            DbContext db,
            Expression<Func<T, TKey>> key,
            bool descending = false,
            int? skip = null,
            int? take = null,
            CancellationToken ct = default)
        {
            if (!db.Database.IsSqlite())
            {
                IQueryable<T> ordered = descending ? query.OrderByDescending(key) : query.OrderBy(key);
                if (skip is { } s)
                {
                    ordered = ordered.Skip(s);
                }

                if (take is { } t)
                {
                    ordered = ordered.Take(t);
                }

                return await ordered.ToListAsync(ct);
            }

            List<T> rows = await query.ToListAsync(ct);
            Func<T, TKey> selector = key.Compile();
            IEnumerable<T> local = descending ? rows.OrderByDescending(selector) : rows.OrderBy(selector);
            if (skip is { } localSkip)
            {
                local = local.Skip(localSkip);
            }

            if (take is { } localTake)
            {
                local = local.Take(localTake);
            }

            return [.. local];
        }

        /// <summary>
        /// Filters by a predicate that may compare <see cref="DateTimeOffset"/>
        /// values and materialises the matching rows.
        /// </summary>
        public async Task<List<T>> ToListWhereAsync(
            DbContext db,
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default)
        {
            if (!db.Database.IsSqlite())
            {
                return await query.Where(predicate).ToListAsync(ct);
            }

            Func<T, bool> local = predicate.Compile();
            return await query.AsAsyncEnumerable().Where(local).ToListAsync(ct);
        }
    }
}
