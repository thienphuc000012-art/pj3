# Main Quest và portal — scene m

Đã gán hai enemy ở nhóm Quest Marker ID `12` vào quest `Main Quest`.

- Sau khi nhận quest, mỗi trận thắng sẽ chờ quay lại map m rồi phát Die + dissolve của đúng enemy, tiếp đến là cảnh sét đỏ đánh vào portal.
- Bỏ chạy/thua không kích hoạt cảnh này. Mỗi enemy chỉ chạy một lần; tải bản lưu giữa trận thắng và cutscene sẽ tiếp tục cảnh còn thiếu.
- Sau cả hai cảnh, bật điểm chuyển map và VFX Dark; NPC giao quest chuyển đến điểm NPC Arrival.
- Portal quay về vị trí/chiều nhìn gần nhất đã ghi của mapgame. Bản lưu cũ chưa có lịch sử từng map sẽ tìm bản lưu thủ công mapgame, tiếp đến checkpoint mapgame, cuối cùng dùng Target Map Position dự phòng.

Trong Hierarchy của scene m, chọn **Main Quest Portal Sequence**:

- **Enemies**: model encounter, Die clip, material dissolve và camera từng enemy.
- **Portal Camera**: chỉnh Transform của điểm máy quay nhìn portal.
- **Lightning Impact / Dark Area**: vị trí VFX. Lightning Impact hiện là con của Discovery Area dưới portal, Local Position = 0 nên luôn đi theo tâm điểm đó.
- **NPC Arrival**: vị trí và góc đứng NPC sau khi mở portal.
- **Portal Strike Seconds / Camera Cut Fade Seconds / Return Blend Seconds**: nhịp chuyển cảnh.
- **Zap Scale / Dark Scale**: kích thước hiệu ứng. Sét đỏ hiện là (3, 3, 3), lớn gấp 3 prefab gốc.

Quest/nhóm enemy: `Assets/Resources/Adventure/MainQuestPortal.asset`. Chỉ cấu hình này áp dụng cho m.

## Thoại lần đầu gặp save point

Chọn từng save point/campfire → World Interaction → **Thoại lần đầu gặp / First Visit Voice** → **Clips** rồi gán audio vào Element 0. Có thể chỉnh Volume, Pitch và Spatial Blend riêng. Mặc định thoại 2D, pitch 1, chịu thanh Giọng nói trong Options.

Thoại tự phát khi vào phạm vi Range của điểm nghỉ. Mỗi save point ghi nhớ một lần riêng trong dữ liệu chơi, kể cả khi quay lại từ combat hoặc tải lại. Ô chưa gán clip không tiêu hao lượt phát, nên thêm audio sau vẫn hoạt động. Nếu gán nhiều clip, chọn ngẫu nhiên một clip cho lần gặp đầu.

Validation dùng preview scene và dữ liệu giả trong Edit Mode, không chạy Play Mode hoặc ghi dữ liệu chơi.

Trạng thái kiểm tra và ảnh xem trước nằm trong thư mục này. Các thư mục Before-* chứa bản sao scene/quest trước khi cấu hình.
