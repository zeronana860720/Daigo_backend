using DemoShopApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DemoShopApi.Hubs;
using DemoShopApi.services;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.OpenApi.Models;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
var builder = WebApplication.CreateBuilder(args);

// 1. 註冊資料庫 (DbContext)
builder.Services.AddDbContext<DaigoContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // 定義安全設定
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "請輸入 'Bearer' [空格] 加上你的 JWT Token。例如：'Bearer abc123def'"
    });

    // 讓所有 API 預設都要套用這個安全需求
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// 3. 註冊 JWT 驗證服務
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
                .GetBytes(builder.Configuration.GetSection("Jwt:Key").Value!)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration.GetSection("Jwt:Issuer").Value,
            ValidateAudience = true,
            ValidAudience = builder.Configuration.GetSection("Jwt:Audience").Value,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
        
        // ← 加上這個!SignalR 需要這個設定
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// 註冊CORS服務
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp",
        policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5174",
                    "http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // SignalR需要這個
        });
});

// 註冊SignalR功能
builder.Services.AddSignalR();
builder.Services.AddScoped<CommissionService>();
builder.Services.AddScoped<CreateCommissionCode>();
builder.Services.AddScoped<ReviewService>();

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
app.UseAuthorization();  // 辨認你能做什麼
app.UseStaticFiles();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.Run();
