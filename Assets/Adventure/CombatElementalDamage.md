# Tương khắc nguyên tố trong chiến đấu

## Hệ số sát thương

- Đòn đánh khắc hệ mục tiêu: `x2`.
- Đòn đánh cùng hệ với mục tiêu: `x1.5`.
- Hệ của mục tiêu khắc hệ đòn đánh: `x0.5`.
- Cặp nguyên tố chưa có trong bảng khắc hệ, hoặc mục tiêu/đòn đánh là `None`: sát thương bình thường `x1`.

Chỉ có ba hệ: `Lửa`, `Nước`, `Tự nhiên`. Vòng tương khắc mặc định là `Lửa > Tự nhiên > Nước > Lửa`. Quy tắc trong Inspector chỉ cần ghi hệ mạnh hơn và hệ yếu hơn; chiều ngược lại tự áp dụng hệ số bất lợi.

## Gán hệ cho nhân vật

Mở prefab nhân vật có `BattleUnit`:

- Party: prefab thành viên đang được dùng trong `CampaignConfig` → `Starting Party`.
- Enemy: prefab enemy được gán vào encounter.
- Trong `BattleUnit` → `Elemental Type`, chọn hệ của nhân vật.

BattleUnit được tạo cho trận chiến sẽ giữ hệ đã đặt trong prefab.

## Gán hệ riêng cho kỹ năng

Mở asset `Action Data` của từng kỹ năng rồi chọn trường `Element`. Hệ của kỹ năng độc lập với hệ của người dùng; cần gán riêng cho từng kỹ năng muốn có tương khắc. `None` là kỹ năng trung tính, sát thương không bị nhân theo hệ.

## Chỉnh vòng và hệ số

Trong scene `combattest`, chọn `CombatManager` ở Hierarchy rồi mở **Elemental Damage / Tương khắc nguyên tố**:

- `Elemental Advantage Multiplier`: mặc định `2`.
- `Elemental Same Type Multiplier`: mặc định `1.5`.
- `Elemental Disadvantage Multiplier`: mặc định `0.5`.
- `Elemental Counter Rules`: ba cặp mặc định tạo vòng Lửa > Tự nhiên > Nước > Lửa. Mỗi cặp ghi hệ mạnh hơn/yếu hơn; chiều ngược lại tự áp dụng hệ số bất lợi.
- `Use Elemental Damage`: tắt để vô hiệu hóa tương khắc mà không xóa cấu hình.

Tương khắc áp dụng cho từng hit của combo và được tính lúc projectile/beam chạm mục tiêu. Hồi máu và hành động thân thiện không bị nhân sát thương nguyên tố.
