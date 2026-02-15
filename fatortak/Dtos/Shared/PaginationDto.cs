using System.ComponentModel.DataAnnotations;

namespace fatortak.Dtos.Shared
{
    public class PaginationDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
