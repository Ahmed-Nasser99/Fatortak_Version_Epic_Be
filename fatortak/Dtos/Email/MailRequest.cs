namespace fatortak.Dtos.Email
{
    public class MailRequest
    {
        public string[] ToEmail { get; set; }
        public string[]? CC { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }
}
