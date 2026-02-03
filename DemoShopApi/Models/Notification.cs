namespace DemoShopApi.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public string Uid { get; set; } = null!;

        public string? Title { get; set; }

        public string? Content { get; set; }

        public bool? IsRead { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;

        public virtual User User { get; set; } = null!;

    }
}
