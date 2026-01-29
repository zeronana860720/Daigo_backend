namespace DemoShopApi.DTOs
{

    public class CreateBuyerOrderDto
    {
        public int StoreId { get; set; }

        // 若你之後用 JWT，這個欄位可以不用從前端傳
        public string BuyerUid { get; set; }

        public string ReceiverName { get; set; }
        public string ReceiverPhone { get; set; }
        public string ShippingAddress { get; set; }

        public List<CreateBuyerOrderItemDto> Items { get; set; }
    }
}
