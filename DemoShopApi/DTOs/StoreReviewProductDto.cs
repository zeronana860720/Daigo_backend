namespace DemoShopApi.DTOs
{
    public class StoreReviewProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int ReviewFailCount { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
