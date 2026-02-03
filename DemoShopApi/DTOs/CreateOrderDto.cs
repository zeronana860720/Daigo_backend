using System.ComponentModel.DataAnnotations;

namespace DemoShopApi.DTOs
{
    public class CreateOrderDto
    {
        [Required(ErrorMessage = "賣場 ID 不能為空")]
        public int StoreId { get; set; }

        [Required(ErrorMessage = "總金額不能為空")]
        [Range(0.01, double.MaxValue, ErrorMessage = "總金額必須大於 0")]
        public decimal TotalAmount { get; set; }

        [Required(ErrorMessage = "收件人姓名不能為空")]
        [MaxLength(50, ErrorMessage = "收件人姓名最多 50 字")]
        public string ReceiverName { get; set; }

        [Required(ErrorMessage = "收件人電話不能為空")]
        [MaxLength(20, ErrorMessage = "收件人電話最多 20 字")]
        public string ReceiverPhone { get; set; }

        [Required(ErrorMessage = "收件地址不能為空")]
        [MaxLength(255, ErrorMessage = "收件地址最多 255 字")]
        public string ShippingAddress { get; set; }
    }
}