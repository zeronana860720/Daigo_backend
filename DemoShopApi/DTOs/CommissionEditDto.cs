public class CommissionEditDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? Category { get; set; }
    public string? Location { get; set; } // 這是地點的名稱
    public DateTime Deadline { get; set; }
    
    // ✨ 圖片處理：名稱要跟妳 Service 裡用的 dto.Image 一致
    public IFormFile? Image { get; set; } 

    // ✨ 地圖相關欄位：為了對照 Create 邏輯，這些是必須的喔！
    public string? GooglePlaceId { get; set; }
    public string? FormattedAddress { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Currency { get; set; } = "TWD"; // ✨ 補上這一個欄位
}