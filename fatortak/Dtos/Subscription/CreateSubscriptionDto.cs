using fatortak.Common.Enum;

namespace fatortak.Dtos.Subscription
{
    public class CreateSubscriptionDto
    {
        public Guid TenantId { get; set; }
        public SubscriptionPlan Plan { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsYearly { get; set; }
    }
}
