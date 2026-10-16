# Bản tiếng Việt trong game

Game mở bằng tiếng Việt. Trong **Cài đặt → Ngôn ngữ**, chọn `VI` hoặc `EN`; lựa chọn được lưu trên máy và áp dụng sau khi tải lại màn chơi. Tên game và tên riêng của các bóng (Ember, Mantis, Angler, Wrecker, Sparkler, Spore, Mirror, Hive, BGLGXLQ, Hunter) được giữ nguyên để người chơi nhận ra nhân vật ở mọi chế độ.

## Nguồn chữ

- `BallBattleUnity/Assets/Resources/Strings/ui-vi.txt`: mỗi dòng là `câu tiếng Anh<TAB>câu tiếng Việt`. Mẫu `{0}`, `{1}` giữ số hoặc tên động; có thể đảo thứ tự mẫu trong câu tiếng Việt để đúng ngữ pháp. Mẫu cụ thể được ưu tiên trước mẫu chung. Dùng `\s` cho dấu cách ở cuối câu. Không đặt dấu tab trong câu.
- `strings-en.csv` và `strings-vi.csv`: các câu đã có khóa riêng (đặc biệt tên và lời nhắc của trùm). Hai bảng phải có cùng khóa.
- `UiVietnamese.Translate` dịch tại lớp hiển thị; mã định danh, lưu game và công thức chiến đấu không phụ thuộc ngôn ngữ. Khi thêm câu mới lên màn hình, thêm bản dịch vào bảng và kiểm tra độ dài ở cỡ chữ thực tế.

## Giọng văn và thuật ngữ

Ưu tiên cụm từ người Việt dùng tự nhiên, ngắn đủ cho màn hình pixel: **đấu lại**, **nội tại**, **đỡ đòn**, **tầng độc**, **lên cấp**, **hoàn điểm**. Gọi một lần chơi từ đầu đến lúc nhận rương là **ván**, một lần giao chiến là **trận**, và một vòng trong chế độ so tài là **hiệp**. Dùng **trùm** cho boss và **mảnh** cho shard; dùng **xu** trong ván, **vàng** để nâng bóng. Tên node của tám bóng theo [`bb12-persona-web-v2.md`](bb12-persona-web-v2.md). Tên chiêu nên mang tính cách từng bóng, còn dòng hiệu ứng ghi rõ điều kiện và con số. Với câu chứa nhiều vế, dịch cả ý theo trật tự tiếng Việt; không ghép các mảnh dịch theo thứ tự tiếng Anh.

Kiểm tra tự động: `BbViTests` xác nhận tên node, trật tự câu ghép, glyph và thẻ node của mười bóng; `BbFont1Tests.EveryScreenFitsWithoutOverlapInVietnamese` rà hơn 30 màn ở độ phân giải gốc 270×480.
