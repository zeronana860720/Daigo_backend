namespace DemoShopApi.DTOs;

public class UpdateProfileDto
{
    public string? Phone { get; set; }
    public string? Address { get; set; }

    // 🌟 這是關鍵！IFormFile 是 C# 專門用來接收「檔案」的類型
    public IFormFile? AvatarFile { get; set; }
}