# Schedule Manager

Schedule Manager là ứng dụng web quản lý lịch trình và năng suất cá nhân được xây dựng bằng ASP.NET Core MVC. Hệ thống tập trung lịch, công việc, deadline, nhắc việc, báo cáo và trợ lý AI vào một không gian làm việc thống nhất.

Ứng dụng hỗ trợ hai nhóm người dùng chính:

- **Người dùng:** quản lý lịch và task, theo dõi tiến độ, xem báo cáo, sử dụng AI và tùy chỉnh hồ sơ cá nhân.
- **Quản trị viên:** quản lý tài khoản, dữ liệu hệ thống, báo cáo vi phạm và các chỉ số tổng quan.

## Chức năng nổi bật

### Quản lý lịch trình

- Tạo, xem, chỉnh sửa và xóa lịch trình.
- Hiển thị lịch theo danh sách hoặc calendar.
- Tìm kiếm và lọc lịch theo thời gian.
- Đánh dấu lịch quan trọng, địa điểm và người nhận email nhắc lịch.
- Xem trang chi tiết cùng các task thuộc lịch.
- Xuất lịch trình sang PDF.

### Quản lý công việc

- Tạo task độc lập hoặc gắn task vào một lịch trình.
- Theo dõi bốn trạng thái: chưa bắt đầu, đang thực hiện, hoàn thành và quá hạn.
- Phân loại bốn mức ưu tiên: thấp, trung bình, cao và khẩn cấp.
- Quản lý deadline, màu hiển thị, mô tả và liên kết đính kèm.
- Lọc task theo trạng thái, thời gian và mức độ quá hạn.
- Tự động gửi email cảnh báo task quá hạn chưa hoàn thành.

### Dashboard và báo cáo

- Tổng quan lịch hôm nay, lịch sắp tới và task cần chú ý.
- Thống kê năng suất theo ngày, tuần, tháng hoặc khoảng thời gian tùy chọn.
- Biểu đồ tỷ lệ hoàn thành, xu hướng hoạt động và phân bố trạng thái.
- Báo cáo chi tiết lịch, task và hiệu suất cá nhân.
- Xuất báo cáo người dùng và báo cáo quản trị sang PDF.

### Trợ lý AI

- Chat với AI bằng ngữ cảnh lịch và task hiện tại của người dùng.
- Phân tích task quá hạn và đề xuất kế hoạch xử lý.
- Sinh gợi ý lịch trình, công việc và deadline có cấu trúc.
- Cho phép xem trước và áp dụng kế hoạch AI vào hệ thống.
- Lưu cuộc hội thoại và lịch sử tin nhắn.
- Hỗ trợ Gemini API hoặc OpenRouter thông qua cấu hình endpoint.

### Hồ sơ và cộng đồng

- Hồ sơ cá nhân gồm tên hiển thị, tiểu sử, avatar và ảnh bìa.
- Liên kết Facebook, YouTube, TikTok, website và nhạc cá nhân.
- Đường dẫn hồ sơ công khai bằng slug.
- Bật hoặc tắt chế độ hồ sơ công khai.
- Tìm kiếm và xem hồ sơ người dùng khác.
- Gửi báo cáo vi phạm tới quản trị viên.

### Bảng xếp hạng

- Xếp hạng người dùng dựa trên số task hoàn thành và đúng hạn.
- Xem kết quả theo khoảng thời gian.
- Lưu giải thưởng và thứ hạng theo tháng.

### Xác thực và quản trị

- Đăng ký, đăng nhập và đăng xuất bằng ASP.NET Core Identity.
- Đăng nhập bằng Google OAuth.
- Phân quyền `Admin` và `User`.
- Khóa, mở khóa, xóa tài khoản và thay đổi vai trò.
- Quản lý lịch, task và hoạt động của người dùng.
- Tiếp nhận, cảnh báo, khóa tài khoản hoặc bỏ qua báo cáo vi phạm.
- Gửi email thông báo cho các hành động quản trị.

### REST API

- API cho lịch trình, task, hồ sơ, bảng xếp hạng, AI và quản trị.
- DTO riêng cho dữ liệu đầu vào và đầu ra.
- Phân quyền bằng ASP.NET Core Identity.
- Tài liệu tương tác bằng Swagger UI.

## Công nghệ sử dụng

| Thành phần | Công nghệ |
| --- | --- |
| Nền tảng | .NET 8, ASP.NET Core MVC, Razor Pages |
| Xác thực | ASP.NET Core Identity, Google OAuth |
| Cơ sở dữ liệu | SQL Server, Entity Framework Core 8 |
| Giao diện | Razor, Bootstrap, JavaScript, jQuery, FullCalendar |
| Trợ lý AI | Gemini API hoặc OpenRouter |
| Email | SMTP, Gmail App Password |
| PDF | QuestPDF, SkiaSharp |
| API | ASP.NET Core Web API, Swagger/OpenAPI |

## Kiến trúc tổng quan

```text
Browser / API client
        |
        v
MVC Controllers / API Controllers / Identity Razor Pages
        |
        v
Services + Helpers + ViewModels/DTOs
        |
        v
Entity Framework Core + ASP.NET Core Identity
        |
        v
SQL Server
```

Các tích hợp ngoài hệ thống gồm Google OAuth, Gemini/OpenRouter và máy chủ SMTP.

## Tài liệu flow chức năng

Luồng xử lý chi tiết từ giao diện, controller, service đến database được mô tả tại [`Docs/FUNCTION_FLOWS.md`](Docs/FUNCTION_FLOWS.md).

## Yêu cầu môi trường

- .NET SDK 8.x
- SQL Server hoặc SQL Server LocalDB
- Visual Studio 2022, Visual Studio Code hoặc IDE hỗ trợ .NET
- Git
- `dotnet-ef` 8.x nếu muốn thao tác migration bằng dòng lệnh

Cài Entity Framework CLI nếu máy chưa có:

```powershell
dotnet tool install --global dotnet-ef --version 8.*
```

## Cài đặt và chạy dự án

### 1. Clone repository

```powershell
git clone <repository-url>
cd schedule
```

### 2. Khôi phục package

```powershell
dotnet restore
```

### 3. Cấu hình cơ sở dữ liệu

Khuyến nghị lưu connection string bằng User Secrets khi chạy local:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=ScheduleManagerDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Áp dụng migration:

```powershell
dotnet ef database update
```

Ứng dụng cũng tự động chạy migration khi khởi động.

### 4. Chạy ứng dụng

```powershell
dotnet run --launch-profile http
```

Truy cập:

```text
http://localhost:5299
```

Swagger UI:

```text
http://localhost:5299/swagger
```

## Cấu hình dịch vụ ngoài

### Google OAuth

Lưu Client ID và Client Secret bằng User Secrets:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_GOOGLE_CLIENT_ID"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_GOOGLE_CLIENT_SECRET"
```

Thêm redirect URI sau trong Google Cloud Console:

```text
http://localhost:5299/signin-google
```

Nếu chạy bằng IIS Express, thêm đúng callback tương ứng với cổng được cấu hình trong `Properties/launchSettings.json`.

Google OAuth không hỗ trợ callback trực tiếp tới địa chỉ IP riêng như `10.x.x.x` hoặc `192.168.x.x`. Khi thử nghiệm qua LAN, hãy đăng nhập Google bằng `localhost` trên máy chạy ứng dụng hoặc dùng một domain HTTPS/tunnel công khai.

### Email SMTP

Với Gmail, cần bật xác minh hai bước và tạo App Password. Sau đó cấu hình:

```powershell
dotnet user-secrets set "EmailSettings:EnableEmail" "true"
dotnet user-secrets set "EmailSettings:SmtpServer" "smtp.gmail.com"
dotnet user-secrets set "EmailSettings:SmtpPort" "587"
dotnet user-secrets set "EmailSettings:SenderEmail" "YOUR_EMAIL"
dotnet user-secrets set "EmailSettings:SenderName" "Schedule Manager"
dotnet user-secrets set "EmailSettings:SenderPassword" "YOUR_APP_PASSWORD"
```

`ReminderService` chạy nền mỗi phút để:

- Gửi email khi lịch đã đến thời điểm cần nhắc.
- Cảnh báo task đã quá hạn trong vòng bảy ngày gần nhất.
- Đánh dấu thời điểm đã xử lý để tránh gửi trùng lặp.

### Gemini AI

Cấu hình trực tiếp với Gemini API:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY"
dotnet user-secrets set "Gemini:Model" "gemini-3.1-flash-lite"
dotnet user-secrets set "Gemini:MaxOutputTokens" "1400"
dotnet user-secrets set "Gemini:Temperature" "0.35"
```

Khi `Gemini:Endpoint` để trống, ứng dụng sử dụng endpoint Google Generative Language mặc định.

### OpenRouter

Để sử dụng OpenRouter thay cho Gemini trực tiếp:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_OPENROUTER_API_KEY"
dotnet user-secrets set "Gemini:Endpoint" "https://openrouter.ai/api/v1/chat/completions"
dotnet user-secrets set "Gemini:Model" "YOUR_OPENROUTER_MODEL"
```

Xem hướng dẫn AI chi tiết tại [`Docs/AI_CHATBOT_SETUP.md`](Docs/AI_CHATBOT_SETUP.md).

## REST API

Các nhóm endpoint chính:

| Endpoint | Chức năng | Quyền |
| --- | --- | --- |
| `/api/schedules` | CRUD lịch trình và dữ liệu calendar | User |
| `/api/tasks` | CRUD task và cập nhật trạng thái | User |
| `/api/profile` | Hồ sơ cá nhân, hồ sơ công khai và báo cáo | User/Public |
| `/api/leaderboard` | Dữ liệu bảng xếp hạng | User |
| `/api/ai` | Hội thoại và phản hồi AI | User |
| `/api/admin` | Người dùng và xử lý báo cáo | Admin |

Phần lớn endpoint yêu cầu người dùng đăng nhập. Endpoint quản trị yêu cầu role `Admin`.

## Cơ sở dữ liệu

Các nhóm bảng chính:

- ASP.NET Identity: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` và các bảng xác thực liên quan.
- `ScheduleItems`: lịch trình của người dùng.
- `TaskItems`: công việc và deadline thuộc lịch trình.
- `UserProfiles`: dữ liệu hồ sơ và quyền riêng tư.
- `LeaderboardAwards`: giải thưởng xếp hạng theo kỳ.
- `UserReports`: báo cáo vi phạm và trạng thái xử lý.
- `AiChatConversations`, `AiChatMessages`: lịch sử hội thoại AI.

Project hiện có migration cho Identity, hồ sơ, task, quyền riêng tư, bảng xếp hạng, báo cáo người dùng, AI Chat và email task quá hạn.

## Cấu trúc dự án

```text
schedule/
|-- Areas/Identity/       # Trang đăng nhập, đăng ký và Google OAuth
|-- Controllers/         # MVC controllers
|   `-- Api/             # REST API controllers
|-- Data/                # DbContext, factory và dữ liệu khởi tạo
|-- Docs/                # Tài liệu cấu hình bổ sung
|-- DTOs/                # Request/response models cho API
|-- Helpers/             # Thống kê, hiển thị và tạo PDF
|-- Migrations/          # Entity Framework Core migrations
|-- Models/              # Các entity và cấu hình hệ thống
|-- Services/            # Email, AI, leaderboard và background service
|-- ViewModels/          # Dữ liệu dành cho giao diện MVC
|-- Views/               # Razor views
|-- wwwroot/             # CSS, JavaScript, hình ảnh và thư viện frontend
|-- Program.cs           # Cấu hình và pipeline ứng dụng
|-- schedule.csproj      # Package và cấu hình project
`-- schedule.sln         # Visual Studio solution
```

## Build và publish

Kiểm tra project:

```powershell
dotnet build
```

Publish bản Release:

```powershell
dotnet publish -c Release -o publish
```

Thư mục `publish/` là sản phẩm build cục bộ và không nên commit vào repository.

## Bảo mật

Không lưu các giá trị sau trực tiếp trong Git:

- Connection string của môi trường thật.
- SMTP email và App Password.
- Gemini hoặc OpenRouter API Key.
- Google OAuth Client Secret.

Khi phát triển local, sử dụng .NET User Secrets. Khi triển khai, sử dụng biến môi trường hoặc secret manager của nền tảng hosting.

Ví dụ ánh xạ cấu hình sang biến môi trường:

```text
ConnectionStrings__DefaultConnection
EmailSettings__SenderPassword
Gemini__ApiKey
Authentication__Google__ClientSecret
```

> **Lưu ý:** `Data/IdentitySeedData.cs` chứa dữ liệu demo phục vụ quá trình phát triển. Hãy thay đổi hoặc vô hiệu hóa tài khoản, mật khẩu và dữ liệu mẫu trước khi triển khai production.

Nếu một khóa đã từng được commit, cần thu hồi khóa đó và xóa khỏi lịch sử Git; chỉ tạo thêm một commit xóa khóa là chưa đủ.

## Hạn chế hiện tại

- Email nhắc việc phụ thuộc vào tiến trình ứng dụng đang chạy.
- Chưa có thông báo realtime trên trình duyệt.
- Chưa hỗ trợ lịch lặp định kỳ.
- Chưa có múi giờ riêng cho từng tài khoản.
- Chưa có test project tự động.
- Swagger và API hiện dùng cùng cơ chế Identity của ứng dụng; chưa có luồng JWT riêng cho client bên ngoài.

## Hướng phát triển

- Thêm thông báo realtime bằng SignalR.
- Thêm lịch lặp theo ngày, tuần hoặc tháng.
- Thêm tag và màu tùy chỉnh cho lịch trình.
- Thêm xác nhận email và khôi phục mật khẩu hoàn chỉnh.
- Bổ sung unit test, integration test và CI/CD.
- Tách nghiệp vụ dùng chung thành service để MVC và API tái sử dụng.
- Triển khai trên Azure App Service, IIS hoặc Linux reverse proxy.

---

Đây là dự án học tập nhằm thực hành ASP.NET Core MVC, Entity Framework Core, Identity, tích hợp dịch vụ ngoài và xây dựng hệ thống quản lý năng suất hoàn chỉnh.
