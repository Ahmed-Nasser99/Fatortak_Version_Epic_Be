namespace fatortak.Entities
{
    public class WorkSetting : baseEntitiy, ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }

        public TimeSpan WorkStartTime { get; set; } = new TimeSpan(9, 0, 0);
        public TimeSpan WorkEndTime { get; set; } = new TimeSpan(17, 0, 0);
        public string WeekendDays { get; set; } = "Friday,Saturday";
        public int GracePeriodMinutes { get; set; } = 15;
        public int BreakMinutes { get; set; } = 60;

        public bool IsRespectGeneralVacation { get; set; } = true;

        public Tenant Tenant { get; set; }
        public ICollection<GeneralVacation> GeneralVacations { get; set; } = new List<GeneralVacation>();
    }
}
