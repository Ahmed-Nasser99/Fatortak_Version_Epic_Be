namespace fatortak.Dtos.Company
{
    public class UploadCompanyLogoDto
    {
        public Guid companyId { get; set; }
        public IFormFile file { get; set; }
    }
}
