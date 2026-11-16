# VFX hồi máu và nhận đồ

Đã gán hai prefab từ thư mục người dùng chỉ định:

- Heal: `Assets/prefab/VFX_Heal_Cast.prefab`.
- Pickup và Chest: `Assets/prefab/VFX_Pickup_Cast.prefab`.

Thiết lập chung ở **Assets/Resources/Adventure/CampaignConfig → Exploration Interaction VFX → Interaction Vfx**. Có thể chỉnh **Heal Offset / Heal Scale**, **Pickup Offset / Pickup Scale** và **Maximum Lifetime**. Xóa tham chiếu prefab để tắt hiệu ứng tương ứng; thao tác hồi máu/nhận đồ vẫn hoạt động.

Save Point dùng cơ chế hồi máu cả đội đang có: chỉ phát một Heal Cast khi ít nhất một thành viên thực sự được tăng HP. Đứng tiếp ở vùng hồi máu với cả đội đầy máu sẽ không phát lại. Quy tắc bảo vệ HP khi vừa bỏ chạy khỏi combat và trở về gần Save Point được giữ nguyên.

Pickup Cast phát tại vị trí model item ở thời điểm nhận item thành công; nếu không có model instance riêng thì dùng vị trí WorldInteraction. Với rương, hiệu ứng phát tại rương khi mở xong và trao thưởng. Pickup Offset dịch vị trí phát theo hướng của item/rương. Animation Event hoặc lệnh hoàn tất lặp lại sẽ không cấp thêm đồ hay phát thêm VFX. Discovery không dùng hiệu ứng nhặt đồ.

VFX hồi máu đi theo Player. VFX nhận đồ đứng độc lập tại vị trí item/rương, không đi theo Player và không mất khi vật thể bị ẩn/hủy. Nếu item thuộc chunk streaming, VFX được giữ ở scene Player để phát hết rồi tự dọn. Particle chạy bằng thời gian thực khi menu nghỉ tạm dừng game, chỉ chạy một lượt và tự dọn sau khi kết thúc hoặc đạt giới hạn thời gian. Các prefab nguồn giữ nguyên.

Kiểm tra trong Edit Mode: **Tools → Adventure → VFX → Validate Heal and Pickup Casts**. Kết quả ở `Validation.txt`, ảnh ở `Heal-*` và `Pickup-*`. Kiểm tra đã đạt: HP đầy/thiếu/nhiều thành viên, không phát lặp, nhận thưởng item/rương, VFX sống sau khi vật thể biến mất, thời gian thực khi pause, dọn hiệu ứng, prefab phát particle đúng material. Đã chạy lại kiểm tra giữ HP sau bỏ chạy và Player về idle khi rương còn mở. Không chạy Play Mode hoặc ghi file save của người chơi.
