namespace fatortak.Dtos.HR.Settings
{
    public class GeneralVacationDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DateOnly Date { get; set; }
        public int? DaysOfVacation { get; set; }
        public string? Notes { get; set; }
    }
}
