# Camera obstruction transparency

Configured scenes: `mapgame`, `m`, `combattest`.

## Chỉnh trong Inspector

Chọn camera thật có **Camera** + **Cinemachine Brain** (Main Camera; trong `m` là Main Camera (1)), rồi mở **Camera Occlusion Fade**.

- **Occluded Alpha**: độ đậm còn lại của vật che. Mặc định `0.16`; `0` là hoàn toàn trong suốt, `1` là không mờ.
- **Fade Out Seconds**: thời gian làm mờ (`0.16` giây).
- **Fade In Seconds**: thời gian hiện lại (`0.25` giây).
- **Clear Delay**: chờ ngắn trước khi hiện lại, tránh chớp khi mép vật thể vừa hết che.
- **Obstruction Layers**: các layer môi trường cần làm mờ. Không chọn layer Player/UI.
- **Cast Radius**, **Vertical Sample Offset**: vùng kiểm tra quanh đường nhìn đến nhân vật.
- **Check Interval**: chu kỳ kiểm tra vật cản; mặc định `0.06` giây.
- **Target**: để trống trong các scene đã cấu hình. Camera khám phá/combat tự cập nhật nhân vật và mục tiêu đang nhìn.

Vật có collider được phát hiện tự động. Vật không có collider: thêm **Camera Occlusion Object** vào model; để trống Renderers để lấy các mesh con. Component này cũng cho phép gom nhiều mesh cùng mờ hoặc bật **Ignore Occlusion** để loại trừ một vật.

Terrain, nhân vật, trigger và particle VFX không bị làm mờ. Collider vật cản vẫn hoạt động. Hệ thống dùng material tạm cho từng renderer và trả lại material/property block gốc khi hết che, tắt camera hoặc rời scene. Shader môi trường đặc biệt dùng bản URP Lit tạm có texture gốc; các hiệu ứng shader riêng như gió được khôi phục cùng material gốc.

## Kiểm tra

`Tools > Adventure > Camera > Setup and Validate Obstruction Fade` chạy kiểm tra trong scene thử tạm và gắn component vào các scene campaign. Không vào Play Mode và không đọc/ghi bản lưu game.

Kết quả: `Library/CombatValidation/CameraOcclusion.result.txt`.

Ảnh đối chiếu: `CameraOcclusion-before.png` và `CameraOcclusion-after.png` trong cùng thư mục. Đã kiểm tra vật che/ngoài đường nhìn/sau mục tiêu, nhiều vật che, collider còn hoạt động, đổi mục tiêu, camera nằm trong vật, marker không có collider, loại trừ trigger, shader TTFE và khôi phục material/property block.

Để thử trong game: đặt một khối Cube có collider giữa camera và nhân vật, di chuyển camera qua khối rồi ra ngoài. Trong combat, thử cả chọn mục tiêu và lúc hai bên chạy đến tấn công.
