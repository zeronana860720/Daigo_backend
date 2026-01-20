using System;
using System.Collections.Generic;
using DemoShopApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoShopApi.Data;

public partial class DaigoContext : DbContext
{
    public DaigoContext(DbContextOptions<DaigoContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<ChatRoom> ChatRooms { get; set; }

    public virtual DbSet<Commission> Commissions { get; set; }

    public virtual DbSet<CommissionHistory> CommissionHistories { get; set; }

    public virtual DbSet<CommissionOrder> CommissionOrders { get; set; }

    public virtual DbSet<CommissionPlace> CommissionPlaces { get; set; }

    public virtual DbSet<CommissionReceipt> CommissionReceipts { get; set; }

    public virtual DbSet<CommissionSequence> CommissionSequences { get; set; }

    public virtual DbSet<CommissionShipping> CommissionShippings { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<WalletLog> WalletLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ChatMess__3214EC07ABE5A5EC");

            entity.Property(e => e.ChatRoomId).HasMaxLength(100);
            entity.Property(e => e.SenderUserId).HasMaxLength(50);
            // 讓 EF 知道 SenderUserId 是連向 User 表的 uid
            entity.HasOne(d => d.Sender)
                .WithMany() // 如果 User 那邊沒設 Collection，這裡留空
                .HasForeignKey(d => d.SenderUserId)
                .HasPrincipalKey(u => u.Uid); // 明確指出對應到 User 的哪一欄
        });

        modelBuilder.Entity<ChatRoom>(entity =>
        {
            entity.Property(e => e.ChatRoomId).HasMaxLength(100);
            entity.Property(e => e.CreatedByUserId).HasMaxLength(50);
            entity.Property(e => e.UserAid)
                .HasMaxLength(50)
                .HasColumnName("UserAId");
            entity.Property(e => e.UserBid)
                .HasMaxLength(50)
                .HasColumnName("UserBId");
        });

        modelBuilder.Entity<Commission>(entity =>
        {
            entity.HasKey(e => e.CommissionId).HasName("PK__Commissi__D19D7CC921851D53");

            entity.ToTable("Commission");

            entity.HasIndex(e => e.ServiceCode, "UX_Commission_service_code").IsUnique();

            entity.Property(e => e.CommissionId).HasColumnName("commission_id");
            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .HasColumnName("category");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatorId)
                .HasMaxLength(10)
                .HasColumnName("creator_id");
            entity.Property(e => e.Deadline)
                .HasColumnType("datetime")
                .HasColumnName("deadline");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EscrowAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("escrowAmount");
            entity.Property(e => e.FailCount)
                .HasDefaultValue(0)
                .HasColumnName("fail_count");
            entity.Property(e => e.Fee)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("fee");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.PlaceId).HasColumnName("place_id");
            entity.Property(e => e.Price)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("price");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.ServiceCode)
                .HasMaxLength(30)
                .HasColumnName("service_code");
            entity.Property(e => e.Status)
                .HasMaxLength(15)
                .HasDefaultValue("未審核")
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Place).WithMany(p => p.Commissions)
                .HasForeignKey(d => d.PlaceId)
                .HasConstraintName("FK_Commission_Place");
        });

        modelBuilder.Entity<CommissionHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__Commissi__096AA2E99D988686");

            entity.ToTable("CommissionHistory");

            entity.Property(e => e.HistoryId).HasColumnName("history_id");
            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .HasColumnName("action");
            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("changed_at");
            entity.Property(e => e.ChangedBy)
                .HasMaxLength(50)
                .HasColumnName("changed_by");
            entity.Property(e => e.CommissionId).HasColumnName("commission_id");
            entity.Property(e => e.NewData).HasColumnName("new_data");
            entity.Property(e => e.OldData).HasColumnName("old_data");

            entity.HasOne(d => d.Commission).WithMany(p => p.CommissionHistories)
                .HasForeignKey(d => d.CommissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CommissionHistory_Commission");
        });

        modelBuilder.Entity<CommissionOrder>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Commissi__4659622912E0446B");

            entity.ToTable("CommissionOrder");

            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.BuyerId)
                .HasMaxLength(10)
                .HasColumnName("buyer_id");
            entity.Property(e => e.CommissionId).HasColumnName("commission_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.FinishedAt)
                .HasColumnType("datetime")
                .HasColumnName("finished_at");
            entity.Property(e => e.SellerId)
                .HasMaxLength(10)
                .HasColumnName("seller_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");

            entity.HasOne(d => d.Commission).WithMany(p => p.CommissionOrders)
                .HasForeignKey(d => d.CommissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_Commission");
        });

        modelBuilder.Entity<CommissionPlace>(entity =>
        {
            entity.HasKey(e => e.PlaceId).HasName("PK__Commissi__BF2B684A47C081CF");

            entity.ToTable("Commission_Place");

            entity.Property(e => e.PlaceId).HasColumnName("place_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.FormattedAddress)
                .HasMaxLength(500)
                .HasColumnName("formatted_address");
            entity.Property(e => e.GooglePlaceId)
                .HasMaxLength(255)
                .HasColumnName("google_place_id");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(10, 8)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(11, 8)")
                .HasColumnName("longitude");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
        });

        modelBuilder.Entity<CommissionReceipt>(entity =>
        {
            entity.HasKey(e => e.ReceiptId).HasName("PK__Commissi__91F52C1FE2AD85AC");

            entity.ToTable("CommissionReceipt");

            entity.Property(e => e.ReceiptId).HasColumnName("receipt_id");
            entity.Property(e => e.CommissionId).HasColumnName("commission_id");
            entity.Property(e => e.ReceiptAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("receipt_amount");
            entity.Property(e => e.ReceiptDate)
                .HasColumnType("datetime")
                .HasColumnName("receipt_date");
            entity.Property(e => e.ReceiptImageUrl).HasColumnName("receipt_image_url");
            entity.Property(e => e.Remark)
                .HasMaxLength(500)
                .HasColumnName("remark");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("uploaded_at");
            entity.Property(e => e.UploadedBy)
                .HasMaxLength(50)
                .HasColumnName("uploaded_by");

            entity.HasOne(d => d.Commission).WithMany(p => p.CommissionReceipts)
                .HasForeignKey(d => d.CommissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Receipt_Commission");
        });

        modelBuilder.Entity<CommissionSequence>(entity =>
        {
            entity.HasKey(e => e.Ym);

            entity.ToTable("commission_sequence");

            entity.Property(e => e.Ym)
                .HasMaxLength(6)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("ym");
            entity.Property(e => e.Seq).HasColumnName("seq");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<CommissionShipping>(entity =>
        {
            entity.HasKey(e => e.ShippingId).HasName("PK__Commissi__059B15A906770AD2");

            entity.ToTable("CommissionShipping");

            entity.Property(e => e.ShippingId).HasColumnName("shipping_id");
            entity.Property(e => e.CommissionId).HasColumnName("commission_id");
            entity.Property(e => e.LogisticsName)
                .HasMaxLength(50)
                .HasColumnName("logistics_name");
            entity.Property(e => e.Remark)
                .HasMaxLength(255)
                .HasColumnName("remark");
            entity.Property(e => e.ShippedAt)
                .HasColumnType("datetime")
                .HasColumnName("shipped_at");
            entity.Property(e => e.ShippedBy)
                .HasMaxLength(50)
                .HasColumnName("shipped_by");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasColumnName("status");
            entity.Property(e => e.TrackingNumber)
                .HasMaxLength(100)
                .HasColumnName("tracking_number");

            entity.HasOne(d => d.Commission).WithMany(p => p.CommissionShippings)
                .HasForeignKey(d => d.CommissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CommissionShipping_Commission");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Uid).HasName("PK__Users__DD701264BFF137ED");

            entity.HasIndex(e => e.Email, "UQ__Users__AB6E6164C939893A").IsUnique();

            entity.Property(e => e.Uid)
                .HasMaxLength(50)
                .HasColumnName("uid");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.Avatar).HasColumnName("avatar");
            entity.Property(e => e.Balance)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("balance");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .HasColumnName("email");
            entity.Property(e => e.EscrowBalance)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("escrow_balance");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
        });

        modelBuilder.Entity<WalletLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__WalletLo__3214EC0726B774A7");

            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Balance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.EscrowBalance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Uid).HasMaxLength(450);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
