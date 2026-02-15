using fatortak.Common.Enum;

namespace fatortak.Dtos.Subscription
{
    public class SubscriptionDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public SubscriptionPlan Plan { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsYearly { get; set; }
        public int AiUsageThisMonth { get; set; }
        public DateTime? AiUsageResetDate { get; set; }
    }
}
