# DemonLord: collider parry theo vũ khí

Đã sửa prefab `Assets/Adventure/Prefabs/Enemy/DemonLord/DemonLord.prefab`.

`SK_FireSword` và `SK_LavaWhip` là SkinnedMeshRenderer. Animation di chuyển/biến dạng các bone, không di chuyển Transform của object chứa renderer. Vì vậy BoxCollider trên hai object đó không bám theo hình vũ khí.

- Kiếm: một BoxCollider con của `DEMON_LORD_SWORD`, khớp mesh trong hệ tọa độ bone.
- Roi: 12 BoxCollider con của các bone `DEMON_LORD_WHIP_01` đến `DEMON_LORD_WHIP_011` và `DEMON_LORD_WHIP_HANDLE`. Mỗi đoạn theo bone riêng, không dùng một box thẳng dài cho cả roi cong.
- Hai collider cũ trên object renderer được giữ nhưng tắt.
- Hai EnemyMeleeParryHitbox cũ giữ nguyên vị trí component, tham chiếu collider mới qua `Hitbox` và `Additional Hitboxes`.
- `Weapon Renderer` ngăn vũ khí đang ẩn nhận parry.
- ParrySystem gọi Physics.SyncTransforms trước truy vấn để đọc đúng tư thế animation hiện tại, kể cả khi autoSyncTransforms tắt.
- Không BakeMesh hoặc tái tạo MeshCollider mỗi frame. BakeMesh chỉ dùng trong công cụ kiểm tra Editor.

## Chỉnh bằng tay

Mở prefab DemonLord. Trong Hierarchy tìm `Parry Hitbox -` hoặc mở chuỗi bone của vũ khí. Chọn object con tương ứng, chỉnh **Box Collider → Center / Size**, giữ **Is Trigger** bật. Không chỉnh hai box cũ đã tắt trên SK_FireSword/SK_LavaWhip.

Phía player vẫn dùng PlayerMeleeParryZone cùng guardCollider. Parry Zone Layers của EnemyMeleeParryHitbox phải bao gồm layer của guardCollider. Hệ thống hiện kiểm tra overlap tại mỗi AnimEvent_DealDamage, khi cửa sổ parry đang bật; không cần thêm Rigidbody để chạy OnTriggerEnter.

## Kiểm tra

Tools → Adventure → Combat → Validate DemonLord Bone Parry Colliders.

Công cụ dùng bản sao prefab trong preview scene cô lập, không vào Play Mode và không gọi API campaign/save. So sánh collider với mesh đã biến dạng trong Idle và năm đòn đánh, thử tiếp xúc, khoảng cách xa, vũ khí ẩn, layer, guard tắt và actor đổi vị trí/hướng/scale. Kết quả: `Tools/DemonLordParry/Validation.txt`.

Lệnh Set Up DemonLord Bone Parry Colliders dùng để dựng lại theo mesh gốc; nó sẽ tính lại Center/Size đã chỉnh tay và tạo bản sao prefab trước khi lưu. Không cần chạy lại sau mỗi lần mở Unity.

Bản sao trước sửa nằm trong thư mục `Before-*` cạnh file này. Chỉ prefab DemonLord và hỗ trợ compound collider/parry pose sync được thay đổi; Animator, skill và thông số BattleUnit được giữ nguyên.
