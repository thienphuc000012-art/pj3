# Camera riêng cho BattleEncounterSetup

Trong scene combat, chọn object có **Battle Encounter Setup** (ví dụ `defaultSetUp`, `BossSetUp`). Mục **Cinematic Camera Settings cho Encounter** có ô **Camera Settings**.

- Để **None**: giữ `CombatCameraSettings` chung trên `CombatCameraDirector`.
- Bấm **Tạo Camera Settings riêng cho encounter...**: chọn nơi lưu một asset mới. Công cụ sao chép cấu hình hiện tại của scene, gồm các góc máy bạn đã chỉnh.
- Hoặc kéo một asset `CombatCameraSettings` đã có vào ô này.
- Mở **Camera Settings — tên asset** ngay bên dưới để chỉnh trực tiếp trong Inspector của encounter.

Các nhóm và thông số giống `CombatCameraSettings`: Battle Idle, Player Menu, Target Selection, Enemy Anticipation/Execution/Recovery, Player Action, Ultimate, Boss Wide, Battle Intro, Status Focus, Framing và Obstacles.

Ví dụ muốn encounter boss chọn mục tiêu gần hơn: mở **Target Selection**, giảm **Camera Distance**, điều chỉnh **Field Of View** và **Maximum Distance**. Dùng **Follow Offset Y**, **Look At Offset Y** và **Primary Screen Position Y** để chỉnh độ cao/bố cục.

Các thay đổi nằm trong **asset camera** đang gán. Hai encounter dùng chung một asset sẽ dùng chung thông số; nút **Tạo bản camera riêng từ cấu hình này...** tạo một bản sao độc lập.

Cấu hình này áp dụng khi `CameraManager` bật **Use Dynamic Combat Camera**. Inspector sẽ ẩn mục **Optional Cameras cho Encounter (Legacy)** trong chế độ này. Các ô cũ chỉ hiện khi CameraManager trong cùng scene tắt Dynamic Camera; tham chiếu đã lưu vẫn được giữ.

Khi encounter từ map chuyển sang combat, hệ thống lấy preset có `setupId` trùng **Combat Setup ID** trên WorldInteraction và áp dụng profile trước camera mở đầu. Encounter không gán profile hoặc không có preset sẽ dùng lại cấu hình chung.

Kiểm tra: **Tools → Combat → Validate Encounter Camera Settings** trong Edit Mode. Kết quả ghi ở `Tools/EncounterCamera/Validation.txt`; kiểm tra độc lập, không Play Mode và không ghi save/scene.
