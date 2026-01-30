using DemoShopApi.Models;

public class StoreProductPlace
{
    public int PlaceId { get; set; }  // 主鍵
    
    public string GooglePlaceId { get; set; }  // Google Place ID (必填)
    
    public string? Name { get; set; }  // 地點名稱
    
    public string FormattedAddress { get; set; }  // 完整地址 (必填)
    
    public decimal Latitude { get; set; }  // 緯度
    
    public decimal Longitude { get; set; }  // 經度
    
    public DateTime CreatedAt { get; set; }  // 建立時間
    
    public string? MapUrl { get; set; }  // Google 地圖網址 (選填)
    
    // ✨ 導覽屬性:一個地點可以被多個商品使用
    public virtual ICollection<StoreProduct>? Products { get; set; }
}