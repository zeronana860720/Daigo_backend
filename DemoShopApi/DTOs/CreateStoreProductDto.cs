using System.ComponentModel.DataAnnotations;

namespace DemoShopApi.DTOs
{
        public class CreateStoreProductDto
        {
            [Required(ErrorMessage = "商品名稱必填")]
            [StringLength(100, ErrorMessage = "商品名稱不可超過 100 字")]
            public string ProductName { get; set; } = null!;

            public string? Description { get; set; }

            [Range(0.1, 500000)]
            public decimal Price { get; set; }

            [Range(1, 500)]
            public int Quantity { get; set; }

            public string? Location { get; set; }
            
            [Required(ErrorMessage = "請上傳商品圖片")]
        public IFormFile? Image { get; set; }

        [Required(ErrorMessage = "請設定下單截止日期")]


            public DateTime? EndDate { get; set; }
        }
    }