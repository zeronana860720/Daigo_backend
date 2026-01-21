using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DemoShopApi.DTOs
{
    public class CommissionCreateDto
    {
        [Required(ErrorMessage = "請填寫標題")]
        [StringLength(100, ErrorMessage = "標題不可超過 100 字")]
        public string? Title { get; set; }

        [Required(ErrorMessage = "請填寫描述")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "請填寫價格")]
        [Range(1, int.MaxValue, ErrorMessage = "價格必須大於 0")]
        public Decimal Price { get; set; }

        [Required(ErrorMessage = "請填寫數量")]
        [Range(1, 9999, ErrorMessage = "數量至少為 1")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "請選擇分類")]
        public string? Category { get; set; }


        [Required(ErrorMessage = "請填寫截止日期")]
        public DateTime Deadline { get; set; }

        [Required(ErrorMessage = "請填寫地點")]
        public string Location { get; set; } = null!;
        
        // google-map用的欄位
        [Required(ErrorMessage = "請選擇幣別")]
        public string Currency { get; set; } = "TWD"; // ✨ 新增幣別

        // ✨ 修正命名：改為跟前端 axios 傳出的 key 一致 (小寫加底線)
        public string? google_place_id { get; set; } 
        public string? formatted_address { get; set; }
        public decimal? latitude { get; set; }
        public decimal? longitude { get; set; }
    
        public IFormFile? Image { get; set; }
    }
}
