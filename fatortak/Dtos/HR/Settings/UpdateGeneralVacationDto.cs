namespace fatortak.Dtos.HR.Settings
{
    public class UpdateGeneralVacationDto
    {
        public string Name { get; set; }
        public DateOnly Date { get; set; }
        public int? DaysOfVacation { get; set; }
        public string? Notes { get; set; }
    }
}
