using fatortak.Common.Enum;

namespace fatortak.Dtos.Subscription
{
    public class UpdateSubscriptionDto
    {
        public SubscriptionPlan? Plan { get; set; }
        public DateTime? EndDate { get; set; }
        public bool? IsYearly { get; set; }
    }
}
