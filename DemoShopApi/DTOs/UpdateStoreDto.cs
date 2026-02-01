using Microsoft.AspNetCore.Http;

namespace DemoShopApi.DTOs
{
    public class UpdateStoreDto
    {
        /// <summary>賣場名稱</summary>
        public string StoreName { get; set; } = null!;

        /// <summary>賣場描述（選填）</summary>
        public string? StoreDescription { get; set; }

        /// <summary>賣場封面圖片（選填）</summary>
        public IFormFile? StoreImage { get; set; }
    }
}