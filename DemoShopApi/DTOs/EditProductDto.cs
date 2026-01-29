using System.ComponentModel.DataAnnotations;

namespace DemoShopApi.DTOs
{
    public class EditProductDto
    {
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? Description { get; set; }

        [Required(ErrorMessage = "請上傳商品圖片")]
        public IFormFile? Image { get; set; }
        public string ProductName { get; set; } = null!;
        public DateTime? EndDate { get; set; }
        public string? Location { get; set; }

    }
}