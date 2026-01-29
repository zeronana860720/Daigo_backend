using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoShopApi.Models;

public partial class CommissionPlace
{
    public int PlaceId { get; set; }

    public string GooglePlaceId { get; set; } = null!;

    public string? Name { get; set; }

    public string FormattedAddress { get; set; } = null!;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public DateTime? CreatedAt { get; set; }
    
    [Column("map_url")]
    public string? MapUrl { get; set; } 

    public virtual ICollection<Commission> Commissions { get; set; } = new List<Commission>();
}
