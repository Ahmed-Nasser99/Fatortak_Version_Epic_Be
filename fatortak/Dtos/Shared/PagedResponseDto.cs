namespace fatortak.Dtos.Shared
{
    public class PagedResponseDto<T>
    {
        public IEnumerable<T> Data { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public object? MetaData { get; set; }
    }
}
