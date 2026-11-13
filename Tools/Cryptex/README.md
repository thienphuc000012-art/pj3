# Cryptex Lock

Prefab: `Assets/asset/model item/cryptex-engine/Prefabs/Cryptex Lock.prefab`.

## Đưa vào map

- Chọn **GameObject > Adventure > Cryptex Lock**, hoặc kéo prefab trên vào Hierarchy của map khám phá.
- Di chuyển root tới nơi đặt khóa. Root nằm ở đáy model; dài **0.55 m**, đường kính khoảng **0.21 m**, so với capsule player cao 2 m. Scale root nếu muốn thay đổi kích thước.
- Không kéo trực tiếp file OBJ: file đó còn chứa các bản tháo rời của model.
- **World Interaction**: chỉnh Range, Rewards và Experience. Giữ persistent ID ổn định sau khi phát hành map.
- **Cryptex Lock > Password**: đúng **7 chữ A–Z**, không dấu hoặc khoảng trắng, ví dụ `ANCIENT`. Model có 7 vòng vật lý. Initial Word là tổ hợp ban đầu, mặc định AAAAAAA.
- Mỗi bản khóa độc lập theo ID của điểm tương tác; menu tạo khóa cấp một ID mới.

## Chơi

- Đến gần, bấm **E**. Chọn vòng và bấm **▲/▼** để xoay từng chữ.
- **A/D** hoặc **←/→** đổi vòng; **W/S**, **↑/↓** hoặc cuộn chuột xoay vòng đang chọn.
- Bấm **Mở khóa**. Enter dùng khi không có nút UI giữ focus; nếu có focus, Enter kích hoạt nút đang chọn theo UGUI.
- Sai mật khẩu: báo chưa đúng, không nhận thưởng và không mở. Chỉ kiểm tra khi vòng đã xoay xong.
- Đúng: lõi trượt ra, phát On Unlocked, nhận Rewards/XP một lần và lưu trạng thái theo CampaignSession. Bảng tự đóng sau animation.
- **Esc**, chuột phải hoặc **Trở lại** đóng bảng, giữ chữ đang xoay và khôi phục input/cursor/thời gian. Đóng UI không tự mở khóa.

## Nối cửa

Trong **On Unlocked**, thêm đối tượng cần mở và chọn hàm tương ứng, ví dụ `GameObject.SetActive(false)` để tắt vật cản. Sự kiện này cũng chạy khi tải bản lưu đã mở khóa; dùng hành động đặt trạng thái, tránh cộng thưởng tại sự kiện vì Rewards đã quản lý một lần.

**On Locked** khôi phục cửa khi tải bản lưu cũ chưa giải khóa, ví dụ `GameObject.SetActive(true)`. Đừng tắt chính root Cryptex nếu muốn thấy model đã mở.

## Chỉnh giao diện

Mở `Assets/Resources/Adventure/UI/CryptexCanvas.prefab`:

- **Backdrop/Title**: tiêu đề.
- **Backdrop/Model Preview**: khung xem model.
- **Backdrop/Rings/Ring Template**: ô chữ mẫu, Up và Down; runtime tạo bản sao theo số vòng.
- **Backdrop/Unlock** và **Back**: các nút.

Canvas dùng Canvas Scaler 1920×1080, dùng EventSystem hiện có. Model preview dùng camera riêng và render texture, không đổi camera exploration. Preview chỉ tồn tại trong lúc tương tác và được giải phóng khi đóng.

## Material và model

6 material URP Lit remap vào OBJ: albedo, normal OpenGL, roughness chuyển thành smoothness trong alpha của metallic map (linear). Bản lắp giữ 7 vòng gốc và phần vỏ/lõi; bỏ các bản tháo rời ở hàng trên. Dùng một BoxCollider với scale dương.

Bảng chữ gốc dùng LL và thiếu W. Prefab dùng A–Z đã bake thành mesh riêng cho từng vòng; albedo/normal/metallic của dải chữ được làm sạch từ chất liệu đồng gốc. Texture gốc không bị ghi đè.

## Kiểm tra

**Tools > Adventure > Cryptex > Validate Lock and Materials** kiểm tra từ khóa, xoay, wrap 26 chữ, khôi phục trạng thái, sự kiện, material, kích thước, collider, nút và ảnh render trong preview scene. Không chạy Play Mode và không ghi bản lưu thật.

Ảnh: `Tools/Cryptex/Preview.png`, `Tools/Cryptex/Interface.png`.

**Build URP Lock Prefab** tái tạo các prefab/material sinh ra về bản mặc định. Công cụ sao lưu trước vào `Tools/Cryptex/BeforeSetup-*`. Tránh chạy lại sau khi tự chỉnh prefab; nên tạo Prefab Variant để tùy chỉnh.
