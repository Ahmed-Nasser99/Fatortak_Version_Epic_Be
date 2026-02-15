namespace fatortak.Dtos.HR.Settings
{
    public class WorkSettingDto
    {
        public Guid Id { get; set; }
        public TimeSpan WorkStartTime { get; set; }
        public TimeSpan WorkEndTime { get; set; }
        public string WeekendDays { get; set; }
        public int GracePeriodMinutes { get; set; }
        public int BreakMinutes { get; set; }
        public bool IsRespectGeneralVacation { get; set; }

        public List<GeneralVacationDto> GeneralVacations { get; set; }
    }
}
