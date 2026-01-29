using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DemoShopApi.Models;

public partial class StoreDbContext : DbContext
{
    public StoreDbContext()
    {
    }

    public StoreDbContext(DbContextOptions<StoreDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BuyerOrder> BuyerOrders { get; set; }

    public virtual DbSet<BuyerOrderDetail> BuyerOrderDetails { get; set; }

    public virtual DbSet<Store> Stores { get; set; }

    public virtual DbSet<StoreProduct> StoreProducts { get; set; }

    public virtual DbSet<StoreProductReview> StoreProductReviews { get; set; }

    public virtual DbSet<StoreReview> StoreReviews { get; set; }

//     protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
// #warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
//         => optionsBuilder.UseSqlServer("Server=.\\sqlexpress;Database=StoreDb;Integrated Security=True;Encrypt=False;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BuyerOrder>(entity =>
        {
            entity.HasKey(e => e.BuyerOrderId).HasName("PK__BuyerOrd__A73AB6402703142F");

            entity.ToTable("BuyerOrder");

            entity.Property(e => e.BuyerOrderId).HasColumnName("buyer_order_id");
            entity.Property(e => e.BuyerUid)
                .HasMaxLength(50)
                .HasColumnName("buyer_uid");
            entity.Property(e => e.CompletedAt)
                .HasColumnType("datetime")
                .HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.ReceiverName)
                .HasMaxLength(50)
                .HasColumnName("receiver_name");
            entity.Property(e => e.ReceiverPhone)
                .HasMaxLength(20)
                .HasColumnName("receiver_phone");
            entity.Property(e => e.ShippedAt)
                .HasColumnType("datetime")
                .HasColumnName("shipped_at");
            entity.Property(e => e.ShippingAddress)
                .HasMaxLength(255)
                .HasColumnName("shipping_address");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.StoreId).HasColumnName("store_id");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("total_amount");

            entity.HasOne(d => d.Store).WithMany(p => p.BuyerOrders)
                .HasForeignKey(d => d.StoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BuyerOrder_Store");
        });

        modelBuilder.Entity<BuyerOrderDetail>(entity =>
        {
            entity.HasKey(e => e.BuyerOrderDetailId).HasName("PK__BuyerOrd__A3064022B2A50535");

            entity.ToTable("BuyerOrderDetail");

            entity.Property(e => e.BuyerOrderDetailId).HasColumnName("buyer_order_detail_id");
            entity.Property(e => e.BuyerOrderId).HasColumnName("buyer_order_id");
            entity.Property(e => e.ProductName)
                .HasMaxLength(255)
                .HasColumnName("product_name");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.StoreProductId).HasColumnName("store_product_id");
            entity.Property(e => e.SubtotalAmount)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("subtotal_amount");
            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("unit_price");

            entity.HasOne(d => d.BuyerOrder).WithMany(p => p.BuyerOrderDetails)
                .HasForeignKey(d => d.BuyerOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BuyerOrderDetail_Order");

            entity.HasOne(d => d.StoreProduct).WithMany(p => p.BuyerOrderDetails)
                .HasForeignKey(d => d.StoreProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BuyerOrderDetail_Product");
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.StoreId).HasName("PK__Store__A2F2A30C48FE1917");

            entity.ToTable("Store");

            entity.Property(e => e.StoreId).HasColumnName("store_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.RecoverAt).HasColumnType("datetime");
            entity.Property(e => e.ReviewFailCount).HasColumnName("review_fail_count");
            entity.Property(e => e.SellerUid)
                .HasMaxLength(50)
                .HasColumnName("seller_uid");
            entity.Property(e => e.StoreName)
                .HasMaxLength(100)
                .HasColumnName("store_name");
            entity.Property(e => e.SubmittedAt).HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.StoreImage)
                .HasColumnName("store_image")
                .HasMaxLength(500);

        });

        modelBuilder.Entity<StoreProduct>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__StorePro__47027DF574809336");

            entity.ToTable("StoreProduct");

            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndDate)
                .HasColumnType("datetime")
                .HasColumnName("end_date");
            entity.Property(e => e.ImagePath)
                .HasMaxLength(255)
                .HasColumnName("image_path");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastReportedAt).HasColumnType("datetime");
            entity.Property(e => e.Location)
                .HasMaxLength(100)
                .HasColumnName("location");
            entity.Property(e => e.Price)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("price");
            entity.Property(e => e.ProductName)
                .HasMaxLength(100)
                .HasColumnName("product_name");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.RejectReason).HasMaxLength(500);
            entity.Property(e => e.StoreId).HasColumnName("store_id");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Store).WithMany(p => p.StoreProducts)
                .HasForeignKey(d => d.StoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StoreProduct_Store");
        });

        modelBuilder.Entity<StoreProductReview>(entity =>
        {
            entity.HasKey(e => e.ProductReviewId).HasName("PK__StorePro__8440EB03E1A8198D");

            entity.ToTable("StoreProductReview");

            entity.Property(e => e.ProductReviewId).HasColumnName("product_review_id");
            entity.Property(e => e.Comment)
                .HasMaxLength(500)
                .HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Result).HasColumnName("result");
            entity.Property(e => e.ReviewerUid)
                .HasMaxLength(50)
                .HasColumnName("reviewer_uid");

            entity.HasOne(d => d.Product).WithMany(p => p.StoreProductReviews)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductReview_Product");
        });

        modelBuilder.Entity<StoreReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK__StoreRev__60883D908F908BAB");

            entity.ToTable("StoreReview");

            entity.Property(e => e.ReviewId).HasColumnName("review_id");
            entity.Property(e => e.Comment)
                .HasMaxLength(255)
                .HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Result).HasColumnName("result");
            entity.Property(e => e.ReviewerUid)
                .HasMaxLength(50)
                .HasColumnName("reviewer_uid");

            entity.HasOne(d => d.Product).WithMany(p => p.StoreReviews)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StoreReview_Store");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
