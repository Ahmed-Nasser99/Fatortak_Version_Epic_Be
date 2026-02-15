namespace fatortak.Dtos.HR.Settings
{
    public class CreateGeneralVacationDto
    {
        public string Name { get; set; }
        public DateOnly Date { get; set; }
        public int? DaysOfVacation { get; set; }
        public string? Notes { get; set; }
    }
}
