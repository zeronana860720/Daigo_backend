using System.ComponentModel.DataAnnotations;

namespace DemoShopApi.DTOs
{
    public class CreateStoreProductDto
    {
        // 原本的欄位
        public string ProductName { get; set; }
    
        public decimal Price { get; set; }
    
        public int Quantity { get; set; }
    
        public string Description { get; set; }
    
        public DateTime EndDate { get; set; }
    
        public IFormFile Image { get; set; }
    
        public string? Location { get; set; }  // 原本就有的地點名稱欄位
    
        // ✨ 新增：地點相關欄位
        [MaxLength(255)]
        public string? GooglePlaceId { get; set; }  // Google Place ID
    
        [MaxLength(255)]
        public string? LocationName { get; set; }  // 地點名稱
    
        [MaxLength(500)]
        public string? FormattedAddress { get; set; }  // 完整地址
    
        public decimal Latitude { get; set; }  // 緯度
    
        public decimal Longitude { get; set; }  // 經度
        
        [MaxLength(50)]
        public string? Category { get; set; }  // ✓ 新增這行
    }

    }