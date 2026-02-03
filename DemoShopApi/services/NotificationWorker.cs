using DemoShopApi.Hubs;
using Microsoft.AspNetCore.SignalR;
// 確認 NuGet 有安裝 Microsoft.Data.SqlClient
// 使用 Microsoft.Data.SqlClient 以支援 .NET Core/8 的 SQL 客戶端功能
using Microsoft.Data.SqlClient;
//// 安裝指令：Install-Package SqlTableDependency
//// 安裝指令：Install-Package Microsoft.Data.SqlClient
// 監控資料庫異動，並透過 SignalR 即時推播通知給前端
// 繼承 BackgroundService 使其在應用程式啟動時自動於後台執行

public class NotificationWorker : BackgroundService
{
    private readonly string _connectionString = string.Empty;
    // SignalR 的 Hub 上下文，用於推播訊息
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationWorker(IConfiguration configuration, IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
        // 確認這裡的名稱與 appsettings.json 中的一致
        var conn = configuration.GetConnectionString("DefaultConnection");
        if (conn != null)
        {
            _connectionString = conn;
        }
    }

    // 從資料庫取得最新的一筆通知內容
    private async Task<dynamic?> GetLatestNotificationAsync()
    {
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            // 打開資料庫連線
            await conn.OpenAsync();
            // 根據 SentAt (發送時間) 降冪排序，取得最新的一筆資料
            // 必須選取 uid 欄位，否則 Worker 不知道要發給誰
            string sql = "SELECT TOP 1 uid, Title, Content FROM dbo.Notifications ORDER BY SentAt DESC";
            await using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                // 將 SQL 指令發送給資料庫執行，並取得一個 SqlDataReader 物件
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    // 移動到下一筆資料並讀取 → 確認是否真的有抓到最新那筆通知
                    if (await reader.ReadAsync())
                    {
                        return new
                        {
                            Uid = reader["uid"].ToString(), // 取得接收者的 uid
                            Title = reader["Title"].ToString(),
                            Content = reader["Content"].ToString()
                        };
                    }
                }
            }
        }
        return null;
    }

    // 背景服務(監聽邏輯)
    // stoppingToken 在網頁關閉時通知服務停止
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 如果連線字串為空，直接結束，防止崩潰
        if (string.IsNullOrEmpty(_connectionString)) return;

        // 啟動 SQL 依賴監聽(需要資料庫開啟 Service Broker 功能)
        // USE master; GO
        // ALTER DATABASE [資料庫名稱] SET ENABLE_BROKER WITH ROLLBACK IMMEDIATE; GO
        SqlDependency.Start(_connectionString);

        // 啟動時只註冊「一次」
        await RegisterDependency();

        // 保持此背景服務持續運行，直到網頁停止 (stoppingToken 被觸發)
        // 使用 Timeout.Infinite 讓此循環不消耗 CPU 資源地等待
        await Task.Delay(Timeout.Infinite, stoppingToken);
        // 應用程式關閉時，停止 SQL 監聽
        SqlDependency.Stop(_connectionString);
    }

    // 註冊 SQL 資料表監聽器
    private async Task RegisterDependency()
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 建立 SQL 指令，監控 Notifications 表中的標題與內容
                // SqlDependency 要求 SQL 語句必須包含 Schema (如 dbo.) 且欄位需明確
                await using (SqlCommand cmd = new SqlCommand("SELECT Title, Content FROM dbo.Notifications", conn))
                {
                    SqlDependency dependency = new SqlDependency(cmd);

                    // 定義資料變動時的事件處理 (OnChange)
                    dependency.OnChange += async (sender, e) =>
                    {
                        // SqlDependency 是一次性的，一旦觸發後該監聽就會失效
                        // 移除舊的事件訂閱，防止重複觸發造成訊息重複
                        //SqlDependency dep = (SqlDependency)sender;
                        //dep.OnChange -= null;

                        // 檢查資料是否變更
                        if (e.Type == SqlNotificationType.Change)
                        {
                            Console.WriteLine("偵測到資料庫變動！");
                            // 偵測變更後，去資料庫抓取最新產生的那一筆通知
                            var latest = await GetLatestNotificationAsync();
                            if (latest != null)
                            {
                                // 透過 SignalR 將資料發送給所有連線的前端客戶端(這邊再改成根據token去抓UID)
                                await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
                                {
                                    title = latest.Title,
                                    content = latest.Content,
                                    time = DateTime.Now.ToString("HH:mm")
                                });
                            }

                            // 重要：監聽是一次性的，必須在觸發後重新註冊新的監聽
                            await Task.Delay(1000);
                            await RegisterDependency();
                        }
                    };
                    // 執行指令以啟動監聽器 (SqlDependency 依賴於此執行來建立訂閱)
                    await cmd.ExecuteReaderAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"監聽發生錯誤: {ex.Message}");
            // 發生錯誤時，過 5 秒再試一次，避免瘋狂重試
            await Task.Delay(5000);
            await RegisterDependency();
        }
    }

}
