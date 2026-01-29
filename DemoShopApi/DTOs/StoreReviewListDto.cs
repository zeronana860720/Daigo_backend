namespace DemoShopApi.DTOs
{
    public class StoreReviewListDto
    {
        public int StoreId { get; set; }
        public string SellerId { get; set; }
        public string StoreName { get; set; }
        public int Status { get; set; }
        public int ReviewFailCount { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<StoreReviewProductDto> StoreProducts{ get; set; }
    }


}
