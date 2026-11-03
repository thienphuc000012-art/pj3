# Hiệu ứng chọn mục tiêu trong chiến đấu

Mở `combattest`, chọn object **CombatManager** trong Hierarchy rồi chỉnh component
**Combat Target Selection**. Hiệu ứng chỉ xuất hiện ở bước chọn mục tiêu cho Attack,
Skill hoặc Item; tự tắt khi chấp thuận, hủy chọn, kết thúc lượt hoặc mục tiêu chết.

- **Enemy Style / Player Style**: màu viền, độ dày `Width`, độ sáng lúc lóe
  `Enter Brightness`, độ sáng khi giữ chọn `Selected Brightness` và nhịp sáng
  `Pulse Amount / Pulse Cycles Per Second`. Mặc định enemy đỏ, player xanh dương.
- **Enter Duration**: thời gian lóe lúc bắt đầu chọn, mặc định `0.1` giây.
  **Exit Duration**: thời gian mờ đi khi bỏ chọn, mặc định `0.16` giây.
  Khi đổi mục tiêu, viền cũ mờ đi cùng lúc viền mới xuất hiện.
- **Outline Details**: cách che khuất, loại renderer, cách tạo viền, và tùy chọn
  giữ độ dày theo độ phân giải. `When Not Occluded` không cho viền xuyên tường.
- **Enemy Target Sprite / Player Target Sprite**: hai ảnh `target enemy` và
  `target player` trong `Assets/asset/Ui`.
- **Marker Size**: kích thước icon theo đơn vị Canvas. **Enter Scale Peak** và
  **Marker Enter Duration** điều khiển icon to ra rồi thu về. **Rotation Speed**
  là tốc độ xoay theo độ/giây; đặt `0` để dừng xoay. **Marker Opacity** chỉnh độ mờ.
- **Center Offset**: dịch tâm icon chung cho mọi nhân vật.

Muốn tâm hoặc kích thước riêng cho một enemy/player, thêm **Combat Target Anchor**
vào cùng object có **BattleUnit**. Gán `Center` vào bone/Transform ở ngực hoặc tạo
một object con làm tâm; chỉnh `Local Offset` và `Marker Scale`. `Outline Renderers`
để trống sẽ lấy các MeshRenderer/SkinnedMeshRenderer của nhân vật. Có thể gán danh
sách cụ thể để bỏ vũ khí hoặc phụ kiện khỏi viền. Particle System không bị viền.

Icon được tạo khi chạy tại `Canvas/Target Selection/Target Reticle` và không chặn
chuột. Chỉnh các tham số trên CombatManager ở Edit Mode để giữ thay đổi sau khi
thoát Play Mode. Không đổi GameObject Layer của nhân vật để bật viền.

Hệ thống dùng renderer feature **Free Outline** có sẵn trong `PC_Renderer`.
Cấu hình lúc chạy được tạo riêng và khôi phục khi thoát; không ghi đè các outline
đã tự thiết lập trong asset **Free Outline Settings**. Bốn Rendering Layer liên
tiếp mặc định `24–27` dành riêng cho các mục tiêu đang vào/ra; chỉ đổi
**First Rendering Layer** trước khi chạy nếu project đã dùng các bit đó.

**Tools → Adventure → UI → Setup Target Selection** gán các tham chiếu vào scene,
có bản sao dự phòng và giữ nguyên các thông số đã chỉnh. Công cụ từ chối ghi đè
`combattest` nếu scene đang có thay đổi chưa lưu.

**Tools → Adventure → UI → Validate Target Selection** kiểm tra chọn enemy/player,
đổi nhanh, mờ viền, icon xoay/phóng to rồi thu nhỏ, mục tiêu chết và khôi phục layer
trong scene kiểm tra riêng. Không chạy trận đấu hoặc truy cập save của người chơi.
Kết quả tại `Library/CombatValidation/CombatTargetSelectionSetup.result.txt`.
Ảnh kiểm tra được render bằng Free Outline thật tại
`Tools/CanvasUI/TargetSelectionPreview.png`; phép kiểm tra màu viền được thực hiện
khi tắt icon để icon không che giấu lỗi shader hoặc Rendering Layer.
