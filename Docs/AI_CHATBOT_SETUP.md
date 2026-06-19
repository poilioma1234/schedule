# Setup AI Chatbot cho Schedule Manager

Ngày cập nhật: 31/05/2026.

## 1. Chọn AI API

Khuyến nghị dùng Gemini API của Google vì:

- Có thể tạo API key từ Google AI Studio.
- Model `gemini-3.1-flash-lite` có free tier và là dòng model nhỏ, tối ưu chi phí.
- Nếu chuyển sang paid tier, giá text standard hiện tại của `gemini-3.1-flash-lite` là khoảng `$0.25 / 1M input tokens` và `$1.50 / 1M output tokens`. Batch/Flex rẻ hơn nhưng không cần cho bản chatbot MVC này.
- App này chỉ gửi context ngắn: yêu cầu của user, tối đa 8 task quá hạn và 8 lịch sắp tới. Không gửi toàn bộ lịch sử chat lên AI để đỡ tốn token.

Nguồn chính thức:

- Gemini pricing: https://ai.google.dev/gemini-api/docs/pricing
- Gemini API key: https://ai.google.dev/gemini-api/docs/api-key
- Gemini rate limits: https://ai.google.dev/gemini-api/docs/rate-limits
- Generate content API: https://ai.google.dev/api/generate-content

## 2. Lấy Gemini API Key

1. Mở Google AI Studio:

   https://aistudio.google.com

2. Đăng nhập bằng tài khoản Google.

3. Vào `Get API key` hoặc `Dashboard > API keys`.

4. Nếu chưa có project:

   - Tạo project mới trong AI Studio, hoặc
   - Import Google Cloud project sẵn có.

5. Bấm `Create API key`.

6. Copy API key và lưu tạm ở nơi riêng. Không commit key vào GitHub.

## 3. Cấu hình API key bằng User Secrets

Chạy trong thư mục project có file `schedule.csproj`:

```powershell
cd C:\Users\ADMIN\Documents\Learn\Web\schedule
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "PASTE_API_KEY_CUA_BAN"
dotnet user-secrets set "Gemini:Model" "gemini-3.1-flash-lite"
dotnet user-secrets set "Gemini:MaxOutputTokens" "1400"
dotnet user-secrets set "Gemini:Temperature" "0.35"
```

Kiểm tra secrets:

```powershell
dotnet user-secrets list
```

## 4. Cài database cho bảng chat

Nếu vừa pull code mới về, chạy:

```powershell
dotnet restore
dotnet ef database update
```

Migration mới sẽ tạo bảng:

```text
AiChatMessages
```

Bảng này lưu lịch sử chat riêng theo `UserId`.

## 5. Chạy project

```powershell
dotnet run --launch-profile http
```

Mở:

```text
http://localhost:5299
```

Đăng nhập user hoặc admin. Tài khoản seed mặc định:

```text
Email: admin@example.com
Password: Admin@123
```

## 6. Test chatbot

1. Vào menu `AI Chat`.

   Hoặc bấm nút tròn `AI` ở góc phải dưới màn hình để mở popup chat nhanh.

2. Nhập thử:

   ```text
   Tạo lịch ôn thi trong 7 ngày, mỗi ngày 2 tiếng, có task ôn lý thuyết và làm đề.
   ```

3. AI sẽ trả lời và hiển thị danh sách lịch/task đề xuất.

4. Chỉnh tiêu đề, giờ bắt đầu, giờ kết thúc, deadline, ưu tiên task nếu cần.

5. Bấm `Áp dụng vào calendar`.

6. App sẽ tạo `ScheduleItems` và `TaskItems`, sau đó chuyển về trang `Lịch trình`.

## 7. Test phân tích task quá hạn

Nhập:

```text
Phân tích các task quá hạn của tôi và gợi ý thứ tự xử lý theo deadline.
```

App chỉ gửi tối đa 8 task quá hạn gần nhất để tiết kiệm token. AI sẽ gợi ý cách sắp xếp lại theo deadline và mức ưu tiên.

## 8. Cách tiết kiệm token

- Dùng model `gemini-3.1-flash-lite`.
- Giữ `Gemini:MaxOutputTokens` khoảng `1000-1600`.
- Hỏi ngắn, rõ số ngày, số giờ, mục tiêu.
- Không gửi đoạn văn dài nếu chỉ cần chia lịch.
- Không gửi toàn bộ lịch sử chat lên AI. Code hiện tại chỉ lưu lịch sử trong database để user xem lại, không đưa toàn bộ history vào prompt.

## 9. Bảo mật key

- Không đặt `Gemini:ApiKey` trong `appsettings.json`.
- Không paste key vào view, JavaScript hoặc HTML.
- Nếu key lỡ bị push lên GitHub, revoke key ngay trong Google AI Studio rồi tạo key mới.
- Nên đặt quota/billing alert trong Google Cloud nếu bật paid tier.

## 10. File code liên quan

- `Models/AiChatMessage.cs`
- `Models/GeminiAiSettings.cs`
- `Services/IAiChatService.cs`
- `Services/GeminiAiChatService.cs`
- `Controllers/AiChatController.cs`
- `Views/AiChat/Index.cshtml`
- `ViewModels/AiChatViewModels.cs`
- `Migrations/*_AddAiChatMessages.cs`
