using System.ComponentModel.DataAnnotations;

namespace DemoShopApi.DTOs
{
    public class ShipOrderDto
    {
        [Required(ErrorMessage = "物流名稱不能為空")]
        public string LogisticsName { get; set; }

        [Required(ErrorMessage = "物流單號不能為空")]
        public string TrackingNumber { get; set; }
    }
}