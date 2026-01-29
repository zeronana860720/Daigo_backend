using System.ComponentModel.DataAnnotations;

namespace DemoShopApi.DTOs
{
    public class CreateStoreDto
    {
        // [Required(ErrorMessage = "賣場ID必填")]
        // public string SellerUid { get; set; } = null!;

        [Required(ErrorMessage = "賣場名稱必填")]
        public string StoreName { get; set; } = null!;
        
        public IFormFile? StoreImage { get; set; }
    }
}
