using System.ComponentModel.DataAnnotations.Schema;

namespace fatortak.Entities
{
    public class ChatSession
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public Guid UserId { get; set; }

        public string Title { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public Tenant Tenant { get; set; }
        public ApplicationUser User { get; set; }

        public ICollection<ChatMessage> Messages { get; set; }
    }
}
