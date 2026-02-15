namespace fatortak.Entities
{
    public class GeneralVacation : baseEntitiy, ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TenantId { get; set; }

        public string Name { get; set; }
        public DateOnly Date { get; set; }
        public int? DaysOfVacation { get; set; } = 1;
        public string? Notes { get; set; }

        public Guid WorkSettingId { get; set; }
        public WorkSetting WorkSetting { get; set; }
    }
}
