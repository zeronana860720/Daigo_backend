using System;
using System.Collections.Generic;

namespace DemoShopApi.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string? CreatorId { get; set; }

    public string? ProductType { get; set; }

    public string Title { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int? Quantity { get; set; }

    public string? Category { get; set; }

    public string? Location { get; set; }

    public string? ShippingAddress { get; set; }

    public DateTime? Deadline { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? Creator { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
