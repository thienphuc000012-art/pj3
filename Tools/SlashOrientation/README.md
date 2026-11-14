# Căn Slash VFX theo kiếm

Slash được căn theo hai điểm `pointBase`/`pointTip` của `SwordBladeTrail` đang hoạt động. Hướng vung dùng pose của bone tại thời điểm animation phát đòn, so với mẫu chuyển động gần nhất. Kiếm cất/ẩn không được chọn làm nguồn pose.

Code đọc mesh slash chính và tính cả `Start Rotation` của particle (Free Slash VFX thường X = 90°, Stylized Slash thường X = 90°, Y = 45°). Mesh distortion, smoke, spark và decal không được ưu tiên làm chuẩn. Prefab gốc của hai package không bị sửa.

Với slash bay thông thường, vị trí tiếp tục đi tới mục tiêu nhưng giữ mặt phẳng/góc kiếm lúc phát đòn. Free Slash Projectile tiếp tục dùng hướng bay +Z, góc lăn theo kiếm và hệ thống ground VFX đã có.

Trong asset skill, `Sword Slash Vfx Local Euler` là góc **bổ sung sau khi tự căn**; giá trị (0, 0, 0) dùng góc tự động. `Sword Slash Vfx Local Offset`, `Scale`, `Lifetime` vẫn hoạt động như trước. Không cần đặt thêm X = 90° để bù góc của particle.

Kiểm tra: `Tools > Adventure > Combat > Validate Sword Slash Orientation`. `Validation.txt` và `Cases.txt` ghi kết quả 104 lần bake mesh particle thật cho 26 prefab, ở các mặt phẳng chém ngang/dọc/chéo và nhiều hướng nhân vật, cùng kiểm tra pose lúc hit và lệnh spawn của CombatManager. Các bài projectile và ground VFX có sẵn cũng đã được chạy lại. Toàn bộ chạy trong Edit Mode, không sửa scene đang mở, ActionData, prefab package hoặc save thật.
