# Collider Environment / Chapel trong scene m

Đã áp dụng trên scene `Assets/Scenes/m.unity`:

- Tắt 414 collider của các mảnh trang trí nhỏ: gạch vụn sát nền, đá vụn, xương và chén. Model vẫn hiển thị. Giữ collider của tường, cột, nền và các vật thể lớn. Không thay các prefab nguồn của package.
- 8 collider cửa/trần hầm dùng mesh LOD2 của model thay cho hình va chạm gần dạng hộp, giữ đúng khoảng trống của các vòm.
- Thêm mặt va chạm dốc ở cửa Chapel để nối nền ngoài với nền hầm, tránh mắc vào cạnh/bậc khi quay ra.

Trong Hierarchy: **Environment → Collision Geometry → Chapel Entrance Ramp**. Đây là collider, không có Renderer. Mesh nằm tại `Assets/Adventure/Generated/Collision/Chapel_Entrance_SmoothCollision.asset`.

Độ cao/kích thước capsule player, Terrain, hình ảnh model và cơ chế di chuyển không được sửa bởi tác vụ này.

## Kiểm tra

Phép thử tạo scene tạm chứa bản sao collider thật và CharacterController cao 2 m, bán kính 0.3 m, Step Offset=0.25, Skin Width=0.03. Chạy vào hầm rồi quay ra theo ba làn, tốc độ đi 2 m/s và chạy 4 m/s. Không vào Play Mode và không truy cập campaign/save game.

`SavedValidation.txt`: kiểm tra scene đã lưu bằng cách mở lại trong Preview Scene, đối chiếu các collider tắt, mesh của vòm và asset mặt dốc. `ValidationAfter.txt`: kết quả di chuyển. `Repair.txt`: danh sách đầy đủ các thay đổi và vị trí backup.

Tools → Adventure → Repair có các lệnh Inspect m Environment Collision, Probe Chapel Passage và Verify Saved m Collision. Không cần chạy lại Apply sau khi sửa.

## Backup

`Before-20261114-111027/m.current.unity` giữ cả các chỉnh sửa chưa lưu của scene tại thời điểm bắt đầu áp dụng. `m.disk.unity` là scene trên ổ đĩa trước khi sửa. `BeforeProbe.txt` ghi các vị trí kẹt trước khi sửa.
