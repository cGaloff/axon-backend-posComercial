namespace Axon.Application.Common.Models;

public record PagedResult<T>(int TotalCount, int Page, int PageSize, IEnumerable<T> Items)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
