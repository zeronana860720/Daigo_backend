namespace DemoShopApi.DTOs
{
    public class BuyerOrderResponseDto
    {
        public int BuyerOrderId { get; set; }
        public string BuyerUid { get; set; }
        public int StoreId { get; set; }
        public decimal TotalAmount { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<BuyerOrderDetailDto> Items { get; set; }
    }
}
