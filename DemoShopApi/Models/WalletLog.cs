namespace DemoShopApi.Models;

public class WalletLog
{
    public int Id { get; set; }           // 系統自動生成的唯一 ID
    public string Uid { get; set; } = string.Empty; // 是哪位使用者的紀錄
    public string Action { get; set; } = string.Empty; // 動作：例如 "儲值"、"提現"
    public decimal Amount { get; set; }    // 變動的金額
    public decimal Balance { get; set; }   // 變動後的可用餘額
    public decimal EscrowBalance { get; set; } // 變動後的圈存金額
    public DateTime CreatedAt { get; set; } = DateTime.Now; // 發生時間
}