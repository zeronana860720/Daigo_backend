using System.ComponentModel.DataAnnotations;

namespace DemoShopApi.DTOs
{
    public class UpdateNewProductReviewDto
    {
        public string ProductName { get; set; } = null!;

       
        [Required(ErrorMessage = "請上傳商品圖片")]
        public IFormFile? Image { get; set; }
    }
}
