using DemoShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. 註冊資料庫 (DbContext)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. 註冊 CORS 服務 (允許 Vue 前端存取)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173") // 這是 Vue 預設網址，若有改動請修改此處
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. 註冊 JWT 驗證服務
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    // 開啟驗證功能:格式-> JWT(bearer)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
                .GetBytes(builder.Configuration.GetSection("Jwt:Key").Value!)),
            // 驗證發卡人
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration.GetSection("Jwt:Issuer").Value,
            // 驗證收件人
            ValidateAudience = true,
            ValidAudience = builder.Configuration.GetSection("Jwt:Audience").Value,
            // 驗證期限
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            // 時間偏移:0
            // 預設會有五分鐘容忍度
            
        };
    });
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5174",
                    "http://localhost:5173") // 這裡填妳的前端 Port,有時候會因為port變動抓不到
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

// 配置 HTTP 請求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 4. 啟用 CORS (必須放在 Authentication 之前)
app.UseCors("AllowVueApp");

// 5. 啟用驗證與授權 (順序絕對不能錯)
app.UseAuthentication(); // 認證 -> 確認身份
app.UseAuthorization();  // 辨認妳能做什麼
app.UseStaticFiles();
app.MapControllers();


app.Run();