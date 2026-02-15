using fatortak.Common.Enum;

namespace fatortak.Entities
{
    public class Subscription
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public SubscriptionPlan Plan { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsYearly { get; set; } = false;
        public int AiUsageThisMonth { get; set; } = 0;
        public DateTime? AiUsageResetDate { get; set; }

        public bool RemindersCreated { get; set; } = false;

    }
}
