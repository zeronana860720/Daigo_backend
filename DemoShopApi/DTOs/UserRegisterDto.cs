using System.ComponentModel.DataAnnotations;
namespace DemoShopApi.DTOs;

public class UserRegisterDto
{
    // 註冊用的DTO
    /*
     * 安全起見,不直接操作資料表
     * 
     */
    [Required]
    public string Name { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(6, ErrorMessage = "密碼長度至少需要 6 位")]
    public string Password { get; set; } = null!;

    public string? Phone { get; set; }
    // 新增地址（待定）
}