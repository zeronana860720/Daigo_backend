# Proxy Shopper Platform (Backend API)

> ASP.NET Core RESTful API 為跨境代購平台提供強大的後端服務

![.NET Core](https://img.shields.io/badge/.NET%20Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)
![Entity Framework](https://img.shields.io/badge/EF%20Core-512BD4?style=for-the-badge&logo=.net&logoColor=white)

## 專案簡介

Proxy Buy Platform Backend 是一個基於 **ASP.NET Core** 的 RESTful API 服務，為跨境代購平台提供完整的後端支援，包含使用者認證、訂單處理、資料持久化及 AI 整合功能。

> **前端倉庫：** 此為後端 API。Vue.js 前端請訪問：[travel-shopper-frontend](https://github.com/zeronana860720/travel-shopper-frontend)

## 核心功能

- **RESTful API 架構**  
  設計簡潔且可擴展的 API 端點，涵蓋使用者、訂單及商品管理

- **安全認證機制**  
  實作 **JWT (JSON Web Token)** 基礎的身份驗證與授權邏輯

- **資料庫管理**  
  使用 **MS SQL Server** 設計正規化資料庫結構，處理複雜關聯（訂單、使用者、交易手續費）

- **CORS 支援**  
  配置跨來源資源共享，確保與 Vue.js 前端的安全通訊

## 技術棧

| 技術 | 用途 |
|------|------|
| **ASP.NET Core Web API** (.NET 6/7/8) | API 框架 |
| **C#** | 開發語言 |
| **SQL Server** | 關聯式資料庫 |
| **Entity Framework Core** | ORM 框架 |
| **JWT Bearer** | 身份驗證 |
| **Swagger / OpenAPI** | API 文件 |

## 快速開始

依照以下步驟在本地建置後端環境。

### 前置需求

- [.NET SDK 6.0+](https://dotnet.microsoft.com/download) 已安裝
- SQL Server（LocalDB 或 Docker 實例）已運行
- （選用）Postman 或其他 API 測試工具

### 安裝步驟

#### 1. Clone 專案
```sh
git clone https://github.com/zeronana860720/travel-shopper-server.git
cd travel-shopper-server
```

#### 2. 還原相依套件
```sh
dotnet restore
```

#### 3. 設定資料庫連線字串
編輯 `appsettings.json`，設定你的 SQL Server 連線：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ProxyBuyDB;Trusted_Connection=True;"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-here",
    "Issuer": "ProxyBuyAPI",
    "Audience": "ProxyBuyClient"
  }
}
```

#### 4. 執行 Entity Framework 遷移
```sh
# 建立資料庫結構
dotnet ef database update

# 若需要新增遷移
dotnet ef migrations add InitialCreate
```

#### 5. 啟動開發伺服器
```sh
dotnet run
```

API 將在 `https://localhost:5001` 或 `http://localhost:5000` 運行。

#### 6. 查看 API 文件
啟動後訪問 Swagger UI：
```
https://localhost:5001/swagger
```

## 專案結構
```
travel-shopper-server/
├── Controllers/        # API 控制器
├── Models/            # 資料模型與實體
├── DTOs/              # 資料傳輸物件
├── Services/          # 業務邏輯服務
├── Data/              # DbContext 與資料庫設定
├── Middleware/        # 自訂中介軟體
├── Migrations/        # EF Core 遷移檔
└── appsettings.json   # 應用程式設定
```

## 主要 API 端點

### 認證
- `POST /api/auth/register` - 使用者註冊
- `POST /api/auth/login` - 使用者登入
- `POST /api/auth/refresh` - 刷新 Token

### 使用者
- `GET /api/users/{id}` - 取得使用者資訊
- `PUT /api/users/{id}` - 更新使用者資料

### 訂單
- `GET /api/orders` - 取得訂單列表
- `POST /api/orders` - 建立新訂單
- `PUT /api/orders/{id}` - 更新訂單狀態
- `DELETE /api/orders/{id}` - 刪除訂單

### 商品
- `GET /api/products` - 取得商品列表
- `POST /api/products` - 新增商品
- `GET /api/products/{id}` - 取得商品詳情

> 完整 API 文件請參考 Swagger UI

## 🔧 環境變數設定

建議在 `appsettings.Development.json` 中設定敏感資訊：
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "你的資料庫連線字串"
  },
  "JwtSettings": {
    "SecretKey": "至少32字元的安全金鑰",
    "Issuer": "ProxyBuyAPI",
    "Audience": "ProxyBuyClient",
    "ExpiryMinutes": 60
  },
  "GoogleAI": {
    "ApiKey": "你的 Gemini API Key"
  }
}
```

## 測試
```sh
# 執行單元測試
dotnet test

# 執行特定測試專案
dotnet test ./Tests/ProxyBuy.Tests.csproj
```

## Docker 部署（選用）
```dockerfile
# Dockerfile 範例
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ProxyBuyAPI.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProxyBuyAPI.dll"]
```
```sh
# 建置與執行
docker build -t proxy-buy-api .
docker run -p 5000:80 proxy-buy-api
```

## 🤝 貢獻

歡迎提交 Issue 或 Pull Request！請確保：
- 遵循現有的程式碼風格
- 新增適當的單元測試
- 更新相關文件

## 作者

**Cliff**  
- GitHub: [@zeronana860720](https://github.com/zeronana860720)
- Email: [zeronana860720@gmail.com]

##  致謝

- [ASP.NET Core](https://docs.microsoft.com/aspnet/core)
- [Entity Framework Core](https://docs.microsoft.com/ef/core/)
- [JWT Authentication](https://jwt.io/)

---

如果這個專案對你有幫助，請給個星星支持！

**相關專案**  
- [Frontend Repository](https://github.com/zeronana860720/travel-shopper-frontend) - Vue.js 前端應用
```
