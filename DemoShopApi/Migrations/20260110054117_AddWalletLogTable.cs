using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DemoShopApi.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    uid = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    balance = table.Column<decimal>(type: "decimal(15,2)", nullable: true, defaultValue: 0.00m),
                    escrow_balance = table.Column<decimal>(type: "decimal(15,2)", nullable: true, defaultValue: 0.00m),
                    Avatar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Users__DD701264BFF137ED", x => x.uid);
                });

            migrationBuilder.CreateTable(
                name: "WalletLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Uid = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EscrowBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    product_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    creator_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    product_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    image_url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    price = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: true, defaultValue: 1),
                    category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    location = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    shipping_address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    deadline = table.Column<DateTime>(type: "datetime", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Products__47027DF5CBCC3192", x => x.product_id);
                    table.ForeignKey(
                        name: "FK_Product_Creator",
                        column: x => x.creator_id,
                        principalTable: "Users",
                        principalColumn: "uid");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    order_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    product_id = table.Column<int>(type: "int", nullable: true),
                    buyer_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    seller_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    order_status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValue: "pending"),
                    total_amount = table.Column<decimal>(type: "decimal(15,2)", nullable: true),
                    system_fee = table.Column<decimal>(type: "decimal(15,2)", nullable: true),
                    shipping_address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    accepted_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Orders__46596229FFA5568D", x => x.order_id);
                    table.ForeignKey(
                        name: "FK_Order_Buyer",
                        column: x => x.buyer_id,
                        principalTable: "Users",
                        principalColumn: "uid");
                    table.ForeignKey(
                        name: "FK_Order_Product",
                        column: x => x.product_id,
                        principalTable: "Products",
                        principalColumn: "product_id");
                    table.ForeignKey(
                        name: "FK_Order_Seller",
                        column: x => x.seller_id,
                        principalTable: "Users",
                        principalColumn: "uid");
                });

            migrationBuilder.CreateTable(
                name: "Chat_Messages",
                columns: table => new
                {
                    message_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_id = table.Column<int>(type: "int", nullable: true),
                    sender_id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    message_text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    sent_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Chat_Mes__0BBF6EE6BAE9625D", x => x.message_id);
                    table.ForeignKey(
                        name: "FK_Chat_Order",
                        column: x => x.order_id,
                        principalTable: "Orders",
                        principalColumn: "order_id");
                    table.ForeignKey(
                        name: "FK_Chat_Sender",
                        column: x => x.sender_id,
                        principalTable: "Users",
                        principalColumn: "uid");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chat_Messages_order_id",
                table: "Chat_Messages",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_Chat_Messages_sender_id",
                table: "Chat_Messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_buyer_id",
                table: "Orders",
                column: "buyer_id");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_product_id",
                table: "Orders",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_seller_id",
                table: "Orders",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "IX_Products_creator_id",
                table: "Products",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "UQ__Users__AB6E6164C939893A",
                table: "Users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Chat_Messages");

            migrationBuilder.DropTable(
                name: "WalletLogs");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
