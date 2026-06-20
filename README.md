
# Quản lý canteen Hutech 
Hệ thống quản lý Canteen trường đại học

**Công nghệ sử dụng: Java 17+, Spring Boot, Spring MVC hoặc REST API, Spring Security, JPA/Hibernate, MySQL và giao diện Bootstrap/Thymeleaf**
#
# Các chức năng chính
- Đăng ký tài khoản bằng số điện thoại và xác thực bằng SMS, SMTP (Brevo)
- Friendly URL
- Tích hợp ví điện tử Momo để thanh toán, hoặc SEPAY
- Phân quyền người dùng
- AutoComplete
- Tích hợp Imgur API để lưu và quản lý ảnh
-  Chức năng quên mật khẩu và gửi đường link xác thực qua email đã đăng ký (SMTP Brevo)
- Chat RealTime
- OAth v2



## Tích hợp thanh toán SePay

Bản này đã thêm thanh toán QR SePay cho đơn hàng.

### URL webhook cấu hình trên SePay

Sau khi publish web lên VPS/domain, nhập URL này trong SePay:

```text
https://ten-mien-cua-ban.com/api/sepay/webhook
```

Chọn bảo mật `HMAC-SHA256` và nhập Secret Key đúng với cấu hình:

```json
"Sepay": {
  "HmacSecret": "canteen@123"
}
```

### Luồng hoạt động

1. Khách đặt hàng trong giỏ hàng.
2. Web tạo đơn hàng, sinh `PaymentCode` dạng `DH000001`.
3. Web hiển thị QR động SePay có `amount` và `des=DH000001`.
4. Khi khách chuyển khoản thành công, SePay gọi webhook `/api/sepay/webhook`.
5. Web xác thực chữ ký HMAC-SHA256, lưu giao dịch vào bảng `SepayTransactions`, sau đó cập nhật `Orders.PaymentStatus = Paid` nếu khớp mã đơn và số tiền.

### Database

Chạy migration:

```bash
dotnet ef database update
```

Nếu không chạy được migration, có thể chạy file SQL thủ công:

```text
Data/SQL/AddSepayPayment.sql
```
