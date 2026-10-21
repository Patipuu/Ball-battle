# Đưa server PvP Ball Battle lên Oracle Cloud Always Free

Server chạy 24/7 trên máy ảo miễn phí của Oracle, có HTTPS. Bản web trên itch.io và bản APK Android đều kết nối tới nó.
Các bước đánh dấu **[BẠN]** cần tài khoản, thẻ hoặc xác nhận của bạn. Các bước còn lại do script làm.

Thời gian: khoảng 30–45 phút cho lần đầu.

---

## 1. [BẠN] Tạo tài khoản Oracle Cloud

1. Vào https://signup.cloud.oracle.com và đăng ký tài khoản **Free Tier**.
2. Oracle cần thẻ Visa/Mastercard để xác minh danh tính. Họ có thể giữ tạm khoảng 1 USD rồi hoàn lại. Gói Always Free không trừ tiền.
3. **Home Region** chỉ chọn được một lần, không đổi được sau này. Nên chọn **Singapore** (gần Việt Nam nhất, ping thấp) hoặc **Japan East (Tokyo)**.

## 2. [BẠN] Nâng lên Pay As You Go (nên làm, vẫn miễn phí)

Với tài khoản Free Tier thường, Oracle có thể **thu hồi máy ảo nếu máy rảnh suốt 7 ngày** (CPU, mạng, RAM đều dưới 20%). Server game nhỏ thường rảnh như vậy. Tài khoản **Pay As You Go** không bị áp quy tắc này, và các tài nguyên Always Free vẫn không tính tiền.

1. Menu ☰ → **Billing & Cost Management** → **Upgrade and Manage Payment** → **Upgrade to Pay As You Go**.
2. Đặt cảnh báo chi phí để yên tâm: **Billing** → **Budgets** → **Create Budget**, số tiền `1` USD, cảnh báo về email của bạn.

Chỉ dùng tài nguyên có nhãn **Always Free-eligible** ở bước 3 thì hóa đơn là 0 đồng.

## 3. [BẠN] Tạo máy ảo

Menu ☰ → **Compute** → **Instances** → **Create instance**:

| Mục | Chọn |
|---|---|
| Name | `ballbattle-pvp` |
| Image | **Canonical Ubuntu 24.04** (bấm *Change image*) |
| Shape | **Ampere** → `VM.Standard.A1.Flex`, **1 OCPU, 6 GB RAM** (dư sức; hạn mức miễn phí là 2 OCPU / 12 GB) |
| Networking | *Create new virtual cloud network* + *public subnet*, bật **Assign a public IPv4 address** |
| SSH keys | **Generate a key pair for me** → bấm **Save private key** |
| Boot volume | Để mặc định (khoảng 47 GB, nằm trong 200 GB miễn phí) |

- Lưu file khóa tải về vào `C:\Users\<tên bạn>\.ssh\oracle-ballbattle.key`. **Không chia sẻ file này.**
- Nếu báo **"Out of capacity"** cho Ampere: thử lại sau vài giờ, hoặc chọn shape **`VM.Standard.E2.1.Micro`** (AMD, 1 GB RAM, cũng miễn phí). Server PvP chỉ dùng khoảng 100 MB RAM nên vẫn chạy được, và script tự nhận ra loại máy.

Bấm **Create**, chờ trạng thái **Running**, rồi chép **Public IP address** (ví dụ `152.69.x.x`).

## 4. [BẠN] Mở cổng 80 và 443

Trên trang máy ảo, bấm vào **Subnet** → **Security** (hoặc *Security Lists*) → **Default Security List** → **Add Ingress Rules**. Thêm hai dòng:

| Source CIDR | IP Protocol | Destination Port |
|---|---|---|
| `0.0.0.0/0` | TCP | `80` |
| `0.0.0.0/0` | TCP | `443` |

Cổng 80 chỉ dùng để xin chứng chỉ HTTPS. Mọi kết nối của game đi qua 443. Tường lửa bên trong máy ảo do script tự mở.

## 5. Cài server (một lệnh)

Mở PowerShell trên máy bạn:

```powershell
cd E:\ball-battle
powershell -ExecutionPolicy Bypass -File .\PvpServer\deploy\deploy.ps1 -Ip <PUBLIC_IP> -Key $HOME\.ssh\oracle-ballbattle.key -WriteClientUrl
```

Lần đầu SSH, bạn có thể được hỏi xác nhận máy chủ: gõ `yes`.

Script làm các việc sau:

1. Build server cho Linux. Máy ảo không cần cài .NET.
2. Gửi lên máy ảo và cài:
   - **Caddy**: tự xin và tự gia hạn chứng chỉ HTTPS.
   - **Dịch vụ chạy nền**: tự khởi động lại nếu lỗi hoặc khi máy ảo khởi động lại.
   - **Sao lưu dữ liệu**: mỗi ngày lúc 3h17, giữ 14 bản.
   - **Mở tường lửa bên trong máy ảo** cho cổng 80 và 443.
3. Địa chỉ server sẽ là `https://<ip-nối-bằng-gạch>.sslip.io`. Dịch vụ sslip.io miễn phí, không cần đăng ký.
4. Kiểm tra `https://…/health` từ máy bạn.
5. Ghi địa chỉ đó vào `BallBattleUnity/Assets/Resources/pvp-server.txt` để các bản build sau dùng.

Khi script in `PASS: https://… is live.` là xong.

> Nếu có tên miền riêng, trỏ bản ghi A về IP máy ảo rồi thêm `-Domain pvp.tenmien.com` vào lệnh trên.

## 6. Build game (web cho itch.io + APK Android)

```powershell
powershell -ExecutionPolicy Bypass -File .\PvpServer\deploy\build-clients.ps1
```

Kết quả:
- `Builds\BallBattle-webgl-itch.zip`: bản chơi trên trình duyệt.
- `Builds\Android\BallBattle.apk`: bản cài trên điện thoại Android.

Lưu ý:
- APK được ký bằng `E:\ball-battle\keys\ballbattle.keystore`, mật khẩu nằm trong `keystore-pass.txt` cùng thư mục. **Hãy sao lưu thư mục `keys` ra chỗ khác** (USB, Drive cá nhân). Mất khóa thì các bản APK sau không cập nhật đè được bản cũ, người chơi phải gỡ ra cài lại.
- Unity 6000.5.3f1 hiện chưa có module Android, nên script build APK bằng Unity 6000.5.1f1 trên một bản sao của project ở `D:\bb-android`. Muốn dùng đúng 6000.5.3f1: **[BẠN]** mở Unity Hub → Installs → 6000.5.3f1 → ⚙ → **Add modules** → tích **Android Build Support** (gồm SDK & NDK, OpenJDK), bấm **Install** rồi **đồng ý quyền Admin của Windows**. Sau đó script tự dùng bản này.

## 7. [BẠN] Đăng lên itch.io

1. https://itch.io/game/new
   - **Kind of project:** HTML.
   - **Uploads:**
     - Tải lên `BallBattle-webgl-itch.zip`, tích **This file will be played in the browser**.
     - Tải lên `BallBattle.apk`, đánh dấu **Android**. Người chơi tải về cài thủ công; điện thoại sẽ hỏi cho phép cài từ nguồn ngoài.
   - **Embed options:**
     - Kích thước khung **540 × 960** (game dọc 270×480, phóng ×2).
     - Tích **Mobile friendly** (Orientation: *Portrait*).
     - Tích **Fullscreen button**.
   - Không cần bật *SharedArrayBuffer support*. Server đã cho phép cả hai địa chỉ mà itch dùng để chạy game.
2. Lưu ở chế độ **Draft**, mở thử, vào PvP: phải thấy **ĐANG KẾT NỐI…** rồi tới màn chọn đội phòng thủ.
3. Cần ít nhất **4 người chơi** (4 tài khoản) đã đăng đội phòng thủ thì mới có 3 đối thủ để chọn. Có thể thử bằng trình duyệt thường, trình duyệt ẩn danh, APK và một máy khác.

---

## Vận hành

**Cập nhật server** (sau khi sửa code): chạy lại lệnh ở bước 5, có thể bỏ `-WriteClientUrl`. Dữ liệu người chơi được giữ nguyên.

**Tải bản sao lưu về máy:**
```powershell
scp -i $HOME\.ssh\oracle-ballbattle.key ubuntu@<PUBLIC_IP>:/var/lib/ballbattle-pvp/pvp-data.json .\Builds\pvp-data-backup.json
```

**Xem log / khởi động lại** (SSH vào máy ảo bằng `ssh -i $HOME\.ssh\oracle-ballbattle.key ubuntu@<PUBLIC_IP>`):
```bash
sudo journalctl -u ballbattle-pvp -n 50     # log server
sudo systemctl restart ballbattle-pvp       # khởi động lại server
sudo journalctl -u caddy -n 50              # log HTTPS
```

**Khắc phục sự cố:**

| Hiện tượng | Nguyên nhân thường gặp |
|---|---|
| `deploy.ps1` báo *No HTTPS answer yet* | Chưa mở cổng 80/443 ở bước 4, hoặc chứng chỉ chưa cấp xong (chạy lại sau 1 phút) |
| SSH báo *UNPROTECTED PRIVATE KEY FILE* | Script tự sửa quyền file khóa; nếu vẫn lỗi, chuyển file khóa vào `C:\Users\<bạn>\.ssh\` |
| Trong game báo **KHÔNG KẾT NỐI MÁY CHỦ** | `pvp-server.txt` chưa đúng địa chỉ HTTPS lúc build; chạy lại bước 5 (`-WriteClientUrl`) rồi bước 6 |
| Báo **TOO MANY REQUESTS** | Mỗi IP chỉ tạo được 20 tài khoản/giờ và 120 yêu cầu/phút (chống spam). Chờ một lúc |
| Máy ảo bị dừng | Kiểm tra email từ Oracle; nếu bị thu hồi vì rảnh, làm bước 2 rồi tạo lại máy ảo và chạy bước 5 |

## Giới hạn hiện tại

- Mỗi người chơi là một tài khoản ẩn danh lưu trong trình duyệt hoặc trong app. Xóa dữ liệu trình duyệt, đổi máy hoặc gỡ app là mất tài khoản PvP, và chưa có cách đăng nhập lại.
- Server tin cấp bóng và cây kỹ năng gửi từ file save trên máy người chơi (chỉ kiểm giới hạn hợp lệ). Người cố ý sửa file save vẫn có thể gian lận trong giới hạn đó.
- Chỉ chạy một server, dữ liệu lưu trong một file JSON. Hợp với giai đoạn thử nghiệm vài trăm người chơi.
