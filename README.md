# HUTECH Canteen Management

Hệ thống quản lý căn tin HUTECH được xây dựng bằng ASP.NET Core MVC. Ứng dụng hỗ trợ quản lý thực đơn, giỏ hàng, đơn hàng, thanh toán QR SePay, đăng nhập Google OAuth 2.0, phân quyền và chat realtime giữa khách hàng với nhân viên.

## Công nghệ sử dụng

### Backend

- .NET 10 và ASP.NET Core MVC/Razor Pages
- ASP.NET Core Identity: đăng ký, đăng nhập, xác nhận email, quên mật khẩu và phân quyền
- Entity Framework Core 10
- Microsoft SQL Server
- SignalR cho chat realtime
- Background Service kiểm tra đơn thanh toán hết hạn
- SMTP Brevo gửi email xác nhận và đặt lại mật khẩu
- Google OAuth 2.0
- SePay QR và webhook thanh toán

### Frontend

- Razor/CSHTML
- HTML5, CSS3 và JavaScript
- Bootstrap 5
- Font Awesome
- Giao diện responsive cho desktop và mobile
- AJAX/Fetch API và SignalR JavaScript Client

## Chức năng chính

### Khách hàng

- Xem món ăn theo danh mục và URL thân thiện bằng slug.
- Tìm kiếm món ăn và gợi ý autocomplete.
- Xem chi tiết, hình ảnh và video sản phẩm.
- Thêm, cập nhật hoặc xóa sản phẩm trong giỏ hàng.
- Tạo đơn hàng với thông tin người nhận, vị trí nhận và ghi chú.
- Theo dõi lịch sử và trạng thái đơn hàng.
- Thanh toán bằng QR SePay, kiểm tra trạng thái và đối soát giao dịch.
- Giao diện tiếng Việt và tiếng Anh.

### Tài khoản và xác thực

- Đăng ký bằng email/mật khẩu và xác nhận email qua SMTP.
- Đăng nhập Google OAuth 2.0.
- Tài khoản OAuth mới được gán vai trò `Customer`.
- Sau khi đăng ký Google, người dùng phải tạo mật khẩu cục bộ trước khi được cấp phiên đăng nhập.
- Một tài khoản có thể đăng nhập bằng Google hoặc email/mật khẩu sau khi hoàn tất thiết lập.
- Quên mật khẩu và đặt lại mật khẩu bằng token một lần.
- Các vai trò: `Admin`, `Employee`, `Customer`.

### Realtime chat

- Phòng chat chung.
- Chat riêng sau khi người nhận chấp nhận yêu cầu kết nối.
- Chat hỗ trợ giữa khách hàng và Admin/Employee.
- Gửi ảnh với kiểm tra định dạng, kích thước và URL phía server.
- Thông báo realtime cho tin nhắn và yêu cầu chat mới.
- Lưu lịch sử hội thoại, archive, đóng hoặc mở lại hỗ trợ.
- Admin có thể khóa chat chung, xóa tin nhắn, mute hoặc ban người dùng.
- Rate limit gửi tin nhắn và giới hạn kích thước SignalR payload.

### Trang quản trị

- Dashboard tổng quan.
- CRUD và soft-delete danh mục, sản phẩm.
- Quản lý thư viện ảnh/video sản phẩm.
- Xem, cập nhật trạng thái và quản lý đơn hàng.
- Giao diện đơn hàng responsive dạng card trên mobile.
- Phân quyền tài khoản.
- Khóa/mở khóa, soft-delete và khôi phục tài khoản.
- Soft-delete giữ nguyên lịch sử đơn hàng và chat.
- Thu hồi phiên đăng nhập và quyền SignalR ngay khi tài khoản bị khóa.
- Quản trị toàn bộ chức năng realtime chat.

### Thanh toán SePay

1. Khi checkout, hệ thống tạo đơn hàng và mã thanh toán dạng `DH000001`.
2. Trang thanh toán hiển thị QR chứa số tiền và nội dung chuyển khoản.
3. SePay gọi webhook `POST /api/sepay/webhook` sau khi nhận giao dịch.
4. Webhook được xác thực bằng HMAC-SHA256 hoặc API key và có cơ chế chống replay.
5. Giao dịch hợp lệ được lưu vào `SepayTransactions`.
6. Đơn hàng được cập nhật thành `Paid` khi mã đơn và số tiền khớp.
7. Background service đánh dấu các yêu cầu thanh toán quá hạn thành `Expired`.

## Bảo mật cốt lõi

- Identity cookie, security stamp và role-based authorization.
- Anti-forgery token cho các thao tác thay đổi dữ liệu.
- Khóa hoặc soft-delete sẽ đổi security stamp để vô hiệu hóa cookie hiện tại.
- SignalR Hub Filter kiểm tra trạng thái khóa trên mỗi Hub invocation.
- Kết nối đang mở nhận sự kiện thu hồi quyền và bị chuyển tới trang lockout.
- Ngăn người dùng gửi yêu cầu chat hoặc tin nhắn cho chính mình.
- Kiểm tra Origin cho quá trình bắt tay SignalR.
- File chat chỉ phục vụ cho người dùng đã xác thực và sử dụng `private, no-store`.
- Webhook SePay xác thực chữ ký, timestamp và chống xử lý giao dịch trùng lặp.
- Return URL được giới hạn ở URL nội bộ để ngăn open redirect.

## Kiến trúc dự án

```text
Areas/
  Admin/                 Controllers và Views dành cho quản trị
  Identity/              Razor Pages cho đăng ký, đăng nhập và quản lý tài khoản
Controllers/             MVC controllers phía khách hàng và SePay webhook
Data/                    DbContext, seeder và EF Core migrations
Hubs/                    SignalR ChatHub và bộ lọc quyền truy cập
Models/                  Entity và view model
Resources/               Tài nguyên bản địa hóa
Services/                Chat, SePay, SMTP và background service
Views/                   Razor views phía khách hàng
wwwroot/                 CSS, JavaScript, thư viện và file upload
Program.cs               Đăng ký dịch vụ và HTTP pipeline
```

Các bảng dữ liệu chính gồm `AspNetUsers`, `AspNetRoles`, `Categories`, `Products`, `ProductGalleries`, `Orders`, `OrderDetails`, `SepayTransactions`, `ChatMessages`, `ChatPrivateRequests`, `ChatSupportConversations`, `ChatPrivateArchives` và `ChatUserRestrictions`.

## Yêu cầu môi trường

- .NET SDK 10
- SQL Server 2019 trở lên hoặc SQL Server Express/LocalDB tương thích
- EF Core CLI nếu cần quản lý migration
- Tài khoản Google Cloud OAuth
- Tài khoản SMTP Brevo
- Tài khoản và webhook SePay

Cài EF Core CLI nếu máy chưa có:

```powershell
dotnet tool install --global dotnet-ef
```

## Cấu hình bảo mật

Không commit Client Secret, SMTP password, SePay token hoặc khóa HMAC vào Git. Trong môi trường Development, nên dùng .NET User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=QuanLyCanTeenHutech;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_GOOGLE_CLIENT_ID"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_GOOGLE_CLIENT_SECRET"
dotnet user-secrets set "Smtp:Username" "YOUR_BREVO_USERNAME"
dotnet user-secrets set "Smtp:Password" "YOUR_BREVO_SMTP_KEY"
dotnet user-secrets set "Sepay:HmacSecret" "YOUR_RANDOM_HMAC_SECRET"
dotnet user-secrets set "Sepay:ApiToken" "YOUR_SEPAY_API_TOKEN"
dotnet user-secrets set "Sepay:WebhookApiKey" "YOUR_WEBHOOK_API_KEY"
```

Cấu hình không nhạy cảm có thể giữ trong `appsettings.json`:

```json
{
  "Smtp": {
    "Host": "smtp-relay.brevo.com",
    "Port": 587,
    "FromEmail": "no-reply@example.com",
    "FromName": "HutechCanteen"
  },
  "Sepay": {
    "BankCode": "YOUR_BANK_CODE",
    "AccountNumber": "YOUR_ACCOUNT_NUMBER",
    "QrTemplate": "compact",
    "ReplayWindowMinutes": 10
  }
}
```

> Nếu secret thật từng được commit hoặc chia sẻ, hãy thu hồi và tạo lại secret trước khi triển khai.

## Cấu hình Google OAuth

Tạo OAuth Client loại **Web application** trong Google Cloud Console.

Authorized JavaScript origins cho môi trường local:

```text
https://localhost:7054
```

Authorized redirect URIs:

```text
https://localhost:7054/signin-google
https://your-domain.example/signin-google
```

Scheme, host, port và đường dẫn phải khớp tuyệt đối để tránh lỗi `redirect_uri_mismatch`.

## Cấu hình SePay webhook

URL webhook production:

```text
https://your-domain.example/api/sepay/webhook
```

Ưu tiên chế độ HMAC-SHA256 và cấu hình cùng một secret ở SePay và `Sepay:HmacSecret`. Chỉ sử dụng HTTPS trong môi trường production.

## Cài đặt và chạy

```powershell
git clone <repository-url>
cd HutechCanteenManagement
dotnet restore
dotnet ef database update
dotnet run --launch-profile https
```

Mở trình duyệt tại:

```text
https://localhost:7054
```

Khi ứng dụng khởi động, `DbSeeder` tự chạy migration còn thiếu, tạo ba vai trò và các tài khoản mẫu nếu chúng chưa tồn tại. Hãy thay đổi hoặc loại bỏ thông tin đăng nhập mẫu trong [Data/DbSeeder.cs](Data/DbSeeder.cs) trước khi triển khai production.

## Build và kiểm tra

```powershell
dotnet build .\QuanLyCanTeenHutech.csproj
```

Dự án hiện chưa có test project tự động. Khi thay đổi các luồng quan trọng, cần kiểm tra tối thiểu:

- Đăng ký, xác nhận email và đăng nhập mật khẩu.
- Đăng ký Google, tạo mật khẩu và đăng nhập lại bằng cả hai phương thức.
- Khóa/soft-delete tài khoản đang hoạt động.
- Checkout, QR SePay, webhook, thanh toán thiếu/thừa và giao dịch trùng.
- Chat chung, chat riêng, hỗ trợ, upload ảnh và thu hồi quyền realtime.
- Bảng đơn hàng và tài khoản trên màn hình mobile.

## Triển khai production

- Đặt `ASPNETCORE_ENVIRONMENT=Production`.
- Dùng HTTPS và reverse proxy IIS/Nginx phù hợp.
- Lưu secret bằng biến môi trường hoặc secret manager.
- Cấp quyền ghi cho thư mục `wwwroot/uploads`.
- Sao lưu SQL Server và file upload định kỳ.
- Không bật Development Exception Page trên production.
- Thay hoặc xóa toàn bộ tài khoản seed mặc định.
- Cấu hình đúng domain OAuth và webhook SePay production.

## License

Dự án phục vụ mục đích học tập và quản lý căn tin HUTECH.

## Project Credits - Group 7
- Trần Anh Kiệt - Main Developer/Tester/System Development
- Lê Quang Huy - Main Developer/Tester
- Phạm Gia Huy - Main Developer/Tester
- Phạm Anh Khoa - Tester/Project Report