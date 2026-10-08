# Roadmap — từ skill tới SDK của team

> Mục đích: skill hôm nay là tài liệu + code mẫu copy vào dự án; đích dài hạn là package có version mà dự án chỉ việc cài.
> Baseline: KT commit `7d96acd5` (2026-10-05). Thiết kế package chi tiết: `KT:docs/multiplayer-modules/architecture.md` (đề xuất, chưa triển khai).
> Đọc khi: lên kế hoạch tách package, hoặc khi dự án thứ hai/thứ ba bắt đầu dùng skill.

## Nguyên tắc

- **Không tách package trước khi có consumer thứ hai chạy thật.** Một consumer = chưa biết ranh giới nào là chung, ranh giới nào là của KT (YAGNI).
- Mỗi bước có **gate bằng thiết bị**, không phải bằng "compile được".
- Giữ thuật toán đã kiểm chứng (lease/fence, ledger, barrier); chỉ đổi kiểu và ranh giới phụ thuộc.
- Tách version package khỏi version giao thức phòng (wire) — xem `02-room-protocol.md` và `KT:docs/multiplayer-modules/architecture.md` §Compatibility.

## Các bước

| Bước | Nội dung | Gate để sang bước sau |
|---|---|---|
| 0 (hiện tại) | Skill: references + `assets/unity` copy-in; Core có test chạy thật | — |
| 1 | Dự án Unity mới đầu tiên dùng `assets/unity` | 2 thiết bị: tạo/vào phòng, trọn trận, rematch, rejoin, đổi vai host; ghi kết quả + lỗi mới vào `04-bug-catalog.md` |
| 2 | Tách 4 UPM package trong repo này (`packages/`): `com.teamnet.multiplayer.foundation` (Core + Unity bridge), `.eos` (Eos), `.fishnet-eos` (FishNetEos + FishyEOS đã vá có patch manifest), `.voice-eos` (tùy chọn) | Project Unity sạch (không có code game) cài package qua git URL, chạy được 2 người — tương đương "PR 7 clean consumer" trong architecture.md |
| 3 | KT chuyển sang dùng package (thay `Assets/Scripts/Net` phần chung) | Regression KT xanh + thiết bị; theo thứ tự batch PR 1–7 trong architecture.md |
| 4 | `cocos-eos` native: C++ module EOS (identity/lobby/P2P) + lớp netcode tối thiểu, cùng giao thức phòng | 5 gate trong `cocos/native-eos-design.md` §7 trên Android + iOS |
| 5 (tùy chọn) | Lobby trên server team (kiến trúc lai B) dùng chung cho Unity + Cocos + Web | Giao thức `02` chạy trên backend team; EOS chỉ còn P2P/RTC; xem `00` §5 |

## Ứng viên package — ánh xạ từ skill

| Package đề xuất | Lấy từ skill | Phụ thuộc |
|---|---|---|
| foundation | `assets/unity/Core/` + `Tests/Core/` | không (noEngineReferences) + Unity bridge mỏng |
| eos | `assets/unity/Eos/` | PlayEveryWare EOS (pin version), foundation |
| fishnet-eos | `assets/unity/FishNetEos/` + `FishyEOS-patches/patched/` + `Editor/` | FishNet (pin), eos, foundation |
| voice-eos | chưa có trong skill — lấy từ `KT:Assets/Scripts/Net/Voice/` khi cần | eos |

## Việc phải làm trước bước 2 (đã biết)

- Session owner duy nhất (KT đang có 2 stack phiên song song) — xem `unity/templates-guide.md` §session owner.
- Port test cho lease/fence của `Eos/` (KT test nằm trong bộ `ProductLobby*`, chưa port).
- Quyết định license/phân phối vendor: FishyEOS là MIT (giữ notice); FishNet và PlayEveryWare kiểm license trước khi đóng gói lại.
- Đo băng thông relay thật (`00` §2) trước khi cân nhắc bước 5.
