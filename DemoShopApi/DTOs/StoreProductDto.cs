namespace DemoShopApi.DTOs
{
    public class StoreProductDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? Location { get; set; }
        public string? ImagePath { get; set; }
        public DateTime? EndDate { get; set; }
    }
}