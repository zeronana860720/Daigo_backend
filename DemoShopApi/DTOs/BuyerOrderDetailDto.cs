    namespace DemoShopApi.DTOs
{
    public class BuyerOrderDetailDto
    {
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal SubtotalAmount { get; set; }
    }
}
