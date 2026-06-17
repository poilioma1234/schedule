# Schedule Manager

Schedule Manager là ứng dụng web ASP.NET Core MVC dùng để quản lý lịch học, deadline, công việc cá nhân và các sự kiện quan trọng. Người dùng có thể tạo lịch, xem theo danh sách hoặc calendar, nhận nhắc lịch qua email và xuất lịch ra PDF.

## Bài Toán Giải Quyết

Trong học tập và công việc, người dùng thường có nhiều lịch học, deadline, lịch họp, lịch cá nhân khác nhau. Nếu chỉ ghi nhớ thủ công hoặc ghi rải rác ở nhiều nơi thì rất dễ quên, trùng lịch hoặc bỏ sót việc quan trọng.

Dự án này giúp:

- Quản lý toàn bộ lịch trình ở một nơi.
- Xem nhanh lịch hôm nay, lịch đang diễn ra và lịch sắp tới.
- Đánh dấu lịch quan trọng.
- Nhắc lịch qua email.
- Xuất lịch trình ra PDF.
- Phân quyền Admin/User để quản lý tài khoản.

## Công Nghệ Sử Dụng

- ASP.NET Core MVC .NET 8
- Entity Framework Core
- SQL Server LocalDB
- ASP.NET Core Identity
- Bootstrap
- FullCalendar
- QuestPDF
- Gmail SMTP

## Chức Năng Chính

### Người dùng

- Đăng ký, đăng nhập, đăng xuất.
- Thêm lịch mới.
- Sửa lịch.
- Xóa lịch.
- Xem danh sách lịch.
- Tìm kiếm lịch theo tiêu đề.
- Lọc lịch theo ngày bắt đầu.
- Xem lịch theo calendar.
- Đánh dấu lịch quan trọng.
- Nhập email nhận nhắc lịch.
- Xuất lịch ra PDF.

### Admin

- Xem danh sách người dùng.
- Gán quyền Admin.
- Khóa/mở khóa tài khoản.
- Xóa tài khoản.
- Xem lịch của người dùng khác.

### Service chạy nền

Ứng dụng có `ReminderService` chạy nền mỗi 1 phút để kiểm tra lịch cần nhắc. Nếu lịch chưa gửi nhắc, có email người nhận, chưa kết thúc và đã đến thời điểm cần nhắc, hệ thống sẽ gửi email.

## Cấu Trúc Thư Mục

```text
schedule/
├── Controllers/
│   ├── AdminController.cs
│   ├── HomeController.cs
│   └── ScheduleController.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   ├── ApplicationDbContextFactory.cs
│   └── IdentitySeedData.cs
├── Helpers/
│   └── SchedulePdfGenerator.cs
├── Migrations/
│   └── ...
├── Models/
│   ├── EmailSettings.cs
│   ├── ErrorViewModel.cs
│   └── ScheduleItem.cs
├── Services/
│   ├── EmailService.cs
│   ├── IEmailService.cs
│   └── ReminderService.cs
├── ViewModels/
│   ├── AdminUserViewModel.cs
│   └── HomeDashboardViewModel.cs
├── Views/
│   ├── Admin/
│   ├── Home/
│   ├── Schedule/
│   └── Shared/
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── lib/
├── appsettings.json
├── Program.cs
├── schedule.csproj
└── schedule.sln
```

## Database

Dự án dùng SQL Server LocalDB với connection string mặc định:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ScheduleManagerRebuildDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

Database chính là `ScheduleManagerRebuildDb`.

Các bảng chính:

- `AspNetUsers`: tài khoản người dùng.
- `AspNetRoles`: vai trò.
- `AspNetUserRoles`: liên kết user và role.
- `ScheduleItems`: dữ liệu lịch trình.

## Tài Khoản Admin Mặc Định

Khi chạy app lần đầu, hệ thống tự tạo tài khoản admin:

```text
Email: admin@example.com
Password: Admin@123
```

Code tạo tài khoản nằm trong:

```text
Data/IdentitySeedData.cs
```

## Setup Khi Clone Từ GitHub

### 1. Cài công cụ cần thiết

Cần có:

- Visual Studio 2022 hoặc mới hơn
- .NET SDK 8
- SQL Server LocalDB
- Git

Kiểm tra .NET:

```powershell
dotnet --version
```

Cài Entity Framework CLI nếu chưa có:

```powershell
dotnet tool install --global dotnet-ef --version 8.*
```

Nếu đã cài rồi:

```powershell
dotnet tool update --global dotnet-ef --version 8.*
```

### 2. Clone project

```powershell
git clone <github-url>
cd schedule
```

Nếu project nằm trong solution khác, vào đúng thư mục có file:

```text
schedule.csproj
schedule.sln
```

### 3. Restore package

```powershell
dotnet restore
```

### 4. Kiểm tra connection string

Mở `appsettings.json`, kiểm tra:

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ScheduleManagerRebuildDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Nếu muốn đổi tên database, đổi phần:

```text
Database=ScheduleManagerRebuildDb
```

Ví dụ:

```text
Database=MyScheduleDb
```

### 5. Tạo database

Nếu project đã có thư mục `Migrations/`, chạy:

```powershell
dotnet ef database update
```

Nếu clone về chưa có migration, tạo migration trước:

```powershell
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 6. Chạy project

```powershell
dotnet run --launch-profile http
```

Mở trình duyệt:

```text
http://localhost:5299
```

## Cấu Hình AI Chatbot

AI Chatbot dùng Gemini API để gợi ý lịch, task, deadline và mức độ ưu tiên. API key chỉ cần cấu hình một lần ở backend; tất cả user đăng nhập sẽ dùng chung qua server. User không cần tự nhập API key.

Nếu chưa cấu hình API key, chatbot sẽ báo cần cấu hình key và không gọi AI thật.

### 1. AI đang dùng LLM gì, điều kiện và cách gắn vào project

- LLM đang dùng: Google Gemini qua Gemini API.
- Model mặc định trong code: `gemini-3.1-flash-lite`, cấu hình tại `Models/GeminiAiSettings.cs`.
- Lý do chọn Gemini: dễ lấy API key từ Google AI Studio, có free tier để test, model flash-lite nhẹ và phù hợp chatbot gợi ý lịch/task.
- Quota/token được tính theo tài khoản Google/API key đã tạo trong Google AI Studio. Nếu backend dùng một API key chung thì tất cả user trong app dùng chung quota của key đó.
- Điều kiện chạy: project có Internet, API key hợp lệ, model hợp lệ, database đã update migration chat, user đã đăng nhập.
- API key chỉ được lưu ở backend bằng User Secrets khi chạy local, hoặc biến môi trường khi deploy. Không đưa key vào GitHub, view, JavaScript hoặc HTML.
- Code gọi AI nằm trong `Services/GeminiAiChatService.cs`, được đăng ký ở `Program.cs` bằng `IAiChatService`.
- Màn hình chat nằm ở `Controllers/AiChatController.cs` và `Views/AiChat/Index.cshtml`.

Luồng hoạt động chính:

1. User nhập yêu cầu trong AI Chat hoặc popup chat.
2. Backend lấy prompt, user hiện tại, một phần task quá hạn và lịch sắp tới.
3. Backend gọi Gemini API bằng API key đã cấu hình.
4. Gemini trả JSON gồm câu trả lời và danh sách lịch/task đề xuất.
5. App hiển thị kết quả để user chỉnh sửa trước khi bấm `Áp dụng vào calendar`.

### 2. Lấy Gemini API key

Mở Google AI Studio:

```text
https://aistudio.google.com/app/apikey
```

Các bước:

1. Đăng nhập tài khoản Google.
2. Bấm `Create API key`.
3. Chọn project có sẵn hoặc tạo project mới.
4. Copy API key vừa tạo.

Không commit API key lên GitHub, không dán key vào JavaScript hoặc HTML.

### 3. Lưu API key bằng User Secrets

Chạy trong thư mục có file `schedule.csproj`:

```powershell
cd C:\Users\ADMIN\Documents\Learn\Web\schedule
dotnet user-secrets set "Gemini:ApiKey" "PASTE_API_KEY_CUA_BAN" --project schedule.csproj
```

Nếu muốn cấu hình rõ model đang dùng:

```powershell
dotnet user-secrets set "Gemini:Model" "gemini-3.1-flash-lite" --project schedule.csproj
dotnet user-secrets set "Gemini:MaxOutputTokens" "1400" --project schedule.csproj
dotnet user-secrets set "Gemini:Temperature" "0.35" --project schedule.csproj
```

Kiểm tra secret đã lưu:

```powershell
dotnet user-secrets list --project schedule.csproj
```

Kết quả nên có dạng:

```text
Gemini:ApiKey = ...
Gemini:Model = gemini-3.1-flash-lite
```

### 4. Cập nhật database cho lịch sử chat

AI Chatbot có bảng `AiChatConversations` và `AiChatMessages` để lưu nhiều đoạn chat riêng theo từng user. Sau khi pull branch có chatbot, chạy:

```powershell
dotnet ef database update
```

### 5. Chạy và test chatbot

Chạy app:

```powershell
dotnet run --project schedule.csproj --launch-profile http
```

Đăng nhập, sau đó:

1. Bấm nút tròn `AI` ở góc phải dưới màn hình, hoặc vào menu `AI Chat`.
2. Nhập thử:

   ```text
   Tạo lịch ôn thi trong 7 ngày, mỗi ngày 2 tiếng
   ```

3. AI sẽ gợi ý lịch và task.
4. Chỉnh lại tiêu đề, giờ bắt đầu/kết thúc, deadline, ưu tiên nếu cần.
5. Bấm `Áp dụng vào calendar`.

AI Chat có thể tạo nhiều đoạn chat riêng. Mỗi đoạn chat lưu lịch sử riêng theo user và danh sách đoạn chat được sắp xếp theo tin nhắn mới nhất.

### 6. Tính năng AI Chatbot đã làm

- Có giao diện AI Chat riêng và nút popup `AI` ở góc phải dưới để chat nhanh.
- Mỗi user đăng nhập có lịch sử chat riêng, không lẫn với user khác.
- Hỗ trợ nhiều đoạn chat/conversation, đoạn nào có tin nhắn mới nhất sẽ hiện lên đầu danh sách.
- User có thể tạo đoạn chat mới, mở lại đoạn chat cũ hoặc xóa lịch sử chat.
- AI nhận yêu cầu tự nhiên, ví dụ `Tạo lịch ôn thi trong 7 ngày`.
- AI gợi ý danh sách lịch và task kèm thời gian bắt đầu, kết thúc, deadline và mức độ ưu tiên.
- User có thể chỉnh sửa lịch/task AI đề xuất trước khi lưu.
- Có nút `Áp dụng vào calendar` để tạo `ScheduleItems` và `TaskItems` thật trong hệ thống.
- AI có thể phân tích task quá hạn và gợi ý thứ tự xử lý theo deadline/mức ưu tiên.
- Prompt được giới hạn context để tiết kiệm token: chỉ gửi yêu cầu hiện tại, một phần task quá hạn và lịch sắp tới, không gửi toàn bộ lịch sử chat lên Gemini.
- Nếu API key thiếu, sai, hết quota hoặc model không hợp lệ, chatbot hiển thị thông báo lỗi thay vì làm hỏng app.

### 7. Lưu ý khi dùng AI key

- Một API key backend dùng chung cho toàn bộ user.
- API key không nằm trong source code và không được push lên GitHub.
- Nếu deploy lên server/IIS/Azure, dùng biến môi trường thay cho User Secrets:

  ```text
  Gemini__ApiKey=API_KEY_CUA_BAN
  ```

- Nếu API key sai, hết quota hoặc model không hợp lệ, chatbot sẽ báo lỗi từ Gemini API.

## Cấu Hình SMTP Gmail

Ứng dụng gửi email bằng Gmail SMTP. Không nên commit mật khẩu Gmail hoặc App Password lên GitHub.

### 1. Bật xác minh 2 bước cho Gmail

Vào tài khoản Google:

```text
Google Account > Security > 2-Step Verification
```

Bật xác minh 2 bước.

### 2. Tạo App Password

Sau khi bật xác minh 2 bước:

```text
Google Account > Security > App passwords
```

Chọn app là `Mail`, device có thể chọn `Windows Computer`, sau đó Google sẽ tạo mật khẩu 16 ký tự.

Ví dụ dạng:

```text
abcd efgh ijkl mnop
```

Khi đưa vào cấu hình có thể giữ hoặc bỏ khoảng trắng.

### 3. Cấu hình bằng User Secrets

Khuyến nghị dùng User Secrets khi chạy local:

```powershell
dotnet user-secrets init
dotnet user-secrets set "EmailSettings:EnableEmail" "true"
dotnet user-secrets set "EmailSettings:SmtpServer" "smtp.gmail.com"
dotnet user-secrets set "EmailSettings:SmtpPort" "587"
dotnet user-secrets set "EmailSettings:SenderEmail" "your-email@gmail.com"
dotnet user-secrets set "EmailSettings:SenderName" "Schedule Manager"
dotnet user-secrets set "EmailSettings:SenderPassword" "your-app-password"
```

Sau đó chạy lại app:

```powershell
dotnet run --launch-profile http
```

### 4. Cấu hình trực tiếp trong appsettings.json

Chỉ dùng cách này khi test local, không nên push lên GitHub:

```json
"EmailSettings": {
  "EnableEmail": true,
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": 587,
  "SenderEmail": "your-email@gmail.com",
  "SenderName": "Schedule Manager",
  "SenderPassword": "your-app-password"
}
```

## Cách Reminder Email Hoạt Động

Mỗi lịch có các trường:

- `StartTime`: thời gian bắt đầu.
- `EndTime`: thời gian kết thúc.
- `ReceiverEmail`: email nhận nhắc.
- `ReminderMinutes`: nhắc trước bao nhiêu phút.
- `ReminderSentAt`: thời điểm đã gửi email.

`ReminderService` chạy mỗi 1 phút. Một lịch sẽ được gửi email nếu:

- Có `ReceiverEmail`.
- Chưa từng gửi email nhắc (`ReminderSentAt == null`).
- Lịch chưa kết thúc (`EndTime >= DateTime.Now`).
- Đã đến thời điểm nhắc (`StartTime <= DateTime.Now.AddMinutes(ReminderMinutes)`).

Ví dụ lúc `01:06`, nếu có lịch:

```text
00:00 - 01:07
01:05 - 01:15
```

Hai lịch này vẫn đang diễn ra nên dashboard sẽ hiện trong phần lịch cần chú ý. Nếu email chưa từng gửi và SMTP cấu hình đúng, service có thể gửi nhắc.

Lịch:

```text
00:58 - 01:00
```

Tại `01:06` đã kết thúc nên sẽ không còn hiện trong dashboard và không gửi email nữa.

## Vì Sao Email Có Thể Không Gửi?

Một số nguyên nhân thường gặp:

- `EmailSettings:EnableEmail` đang là `false`.
- Chưa cấu hình đúng Gmail App Password.
- Gmail chưa bật xác minh 2 bước.
- App không chạy tại thời điểm cần nhắc.
- Lịch đã kết thúc.
- `ReminderSentAt` đã có giá trị, nghĩa là lịch đó đã từng được xử lý gửi nhắc.
- Email người nhận bị nhập sai.
- SMTP bị Google chặn vì cấu hình bảo mật.
- Máy không có internet.

## Cách Test Email

1. Bật SMTP bằng User Secrets hoặc `appsettings.json`.
2. Tạo lịch mới có:
   - `StartTime` cách hiện tại khoảng 3-5 phút.
   - `EndTime` sau `StartTime`.
   - `ReceiverEmail` là email thật.
   - `ReminderMinutes` là `5`.
3. Giữ app đang chạy.
4. Chờ service chạy trong vòng 1 phút.
5. Kiểm tra inbox/spam.

## Lưu Ý Về Dashboard

Dashboard hiện hiển thị:

- `Tổng lịch`: toàn bộ lịch của user.
- `Lịch hôm nay`: lịch có ngày bắt đầu là hôm nay.
- `Đang/sắp diễn ra`: lịch chưa kết thúc.
- `Đang diễn ra`: lịch có `StartTime <= hiện tại <= EndTime`.
- `Lịch cần chú ý`: tối đa 5 lịch đang diễn ra hoặc sắp diễn ra, sắp xếp theo thời gian bắt đầu.

## Hạn Chế Hiện Tại

- Chưa có thông báo realtime trên trình duyệt, mới có nhắc qua email.
- Email reminder phụ thuộc vào app đang chạy.
- Nếu app tắt trong thời điểm nhắc và lịch đã kết thúc, email sẽ không gửi bù.
- Chưa có chọn múi giờ riêng cho từng người dùng.
- Chưa có lặp lịch theo ngày/tuần/tháng.
- Chưa có phân loại lịch theo màu hoặc tag.
- Chưa có API riêng cho mobile app.
- Chưa có unit test/integration test.
- Trang đăng nhập/đăng ký mặc định của ASP.NET Identity vẫn còn một số chữ tiếng Anh nếu chưa scaffold/custom Identity UI.
- SMTP đang dùng Gmail nên có thể bị giới hạn số lượng email/ngày.

## Lỗi Thường Gặp

### dotnet ef không chạy

Lỗi:

```text
dotnet-ef does not exist
```

Cách sửa:

```powershell
dotnet tool install --global dotnet-ef --version 8.*
```

Đóng terminal rồi mở lại.

### Không connect được database

Kiểm tra LocalDB:

```powershell
sqllocaldb info
```

Nếu có `MSSQLLocalDB`, chạy:

```powershell
sqllocaldb start MSSQLLocalDB
```

Sau đó:

```powershell
dotnet ef database update
```

### Build lỗi do file đang bị khóa

Nếu app đang chạy, build có thể báo file `.exe` bị khóa.

Cách sửa:

```powershell
Get-Process schedule -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build
```

### Đổi database nhưng dữ liệu cũ vẫn còn

Nếu muốn tạo database mới, đổi tên database trong connection string rồi chạy:

```powershell
dotnet ef database update
```

## Gợi Ý Nâng Cấp

- Thêm thông báo realtime bằng SignalR.
- Thêm toast notification trong dashboard.
- Thêm recurring schedule.
- Thêm màu/tag cho từng loại lịch.
- Thêm trang thống kê theo tuần/tháng.
- Thêm export Excel.
- Thêm tìm kiếm nâng cao.
- Thêm xác nhận email khi đăng ký.
- Thêm reset password.
- Deploy lên Azure App Service hoặc IIS.

