namespace DemoShopApi.DTOs
{
    public class ReviewDto
    {
        /// <summary>審核者帳號 / UID</summary>
        public string ReviewerUid { get; set; } = null!;
        /// <summary>退件原因（通過時可為 null）</summary>
        public string? Comment { get; set; }
    }

}
