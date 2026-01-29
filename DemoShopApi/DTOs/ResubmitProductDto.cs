namespace DemoShopApi.DTOs
{
    public class ResubmitProductDto
    {
        public string ProductName { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; }

        // 圖片：可選（不一定每次都改）
        public IFormFile? Image { get; set; }
    }

}
