namespace DemoShopApi.DTOs
{
    public class UserCommissionManageDto
    {
        public string ServiceCode { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? EndAt { get; set; }
        public string ImageUrl { get; set; }

        public bool CanEdit { get; set; }
        public bool CanViewDetail { get; set; }
        public bool CanViewShipping { get; set; }
        
        public string Location { get; set; } // ✨ 新增這行
    }
}
