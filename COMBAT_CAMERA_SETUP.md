# Cinematic Combat Camera trong combattest

Scene `Assets/Scenes/combattest.unity` đã gắn **Combat Camera Director** trên object **CameraManager**, bật **Use Dynamic Combat Camera** và gán asset `Assets/Adventure/CombatCameraSettings.asset`. Vào trận từ map hoặc chạy scene combat trực tiếp đều dùng cùng đường camera. **Main Camera** trong scene tiếp tục là Unity Camera xuất hình và có **Cinemachine Brain**.

```text
Main Camera                     Unity Camera + Cinemachine Brain
CameraManager                   CameraManager + CombatCameraDirector
├─ CM_CombatDynamic             CinemachineCamera, tạo khi vào trận
├─ Skill Timeline Staging       Tạo khi dùng skill có Timeline
└─ Skill Cinematic Rig          Tạo trong thời gian phát Timeline
Battlefield_Slots               Player Slots / Enemy Slots từ CombatManager
CombatManager                   CombatManager + CombatTargetSelection
```

Ở Play Mode, Brain lọc **Channel 08**. Camera động dùng priority 100. Các shot trong prefab Timeline dùng priority thấp và chỉ điều khiển Brain qua Cinemachine Track. Khi dừng Timeline, Director lấy lại pose đang chiếu làm điểm xuất phát để blend về camera gameplay. Tắt **Use Dynamic Combat Camera** trong Inspector nếu cần quay lại hệ thống camera cũ để so sánh.

## 1. Battle Idle

Chọn asset **CombatCameraSettings → Battle Idle**. Camera bám nhân vật đến lượt, còn **Primary Screen Position** đặt nhân vật về phía trái để dành chỗ cho đối thủ và menu. **Player Menu** là góc sát vai khi mở Skills hoặc Items. **Orbit Angle** đổi góc 3/4, **Follow Offset** đổi vị trí theo trục của trận, **Look At Offset** chỉnh điểm ngắm. Local X là ngang, Y là cao, Z hướng từ phía Player sang Enemy. **Camera Distance** là khoảng lùi cơ sở. **Maximum Distance** giới hạn khoảng lùi khi tự căn khung. **Field Of View** và **Viewport Fit** quyết định độ rộng và khoảng trống quanh nhân vật.

Combat Manager chuyển camera theo nhân vật đang đến lượt. Vị trí tính từ **Player Slots / Enemy Slots** sau khi áp dụng encounter preset. Nếu một unit thiếu slot, camera dùng vị trí unit được ghi lúc đăng ký. Kiểm tra bằng cách đổi slot hoặc thứ tự party rồi nhìn lượt của ít nhất hai Player. Nếu Player bị che khuất, đổi **Orbit Angle** hoặc tăng **Follow Offset Y** trước khi chỉnh FOV.

## 2. Target Selection và Outline

Bấm **Attack** hoặc chọn một Skill/Item cần mục tiêu. Dùng **A/D** hoặc **←/→** để thử nhiều Enemy, sau đó dùng chiêu hồi máu/buff để thử đổi giữa đồng đội. **Enter/Space** xác nhận. **Esc/chuột phải** quay về góc chờ ở trận thường. Bài hướng dẫn bắt buộc dùng quy tắc khóa riêng của Guided Tutorial. Nếu mục tiêu mất hiệu lực, Combat Manager tìm mục tiêu hợp lệ tiếp theo hoặc đóng phần chọn.

Các hàm cho UI hoặc Input Actions nằm trên **CombatManager**. Dùng `EnterTargetSelection()` sau khi đã chuẩn bị hành động, `SelectNextTarget()`, `SelectPreviousTarget()`, `SetSelectedTarget(Transform)`, `ConfirmTarget()` và `CancelTargetSelection()`. **CombatTargetSelection** đọc cùng mục tiêu đó để bật/tắt Outline đỏ/xanh và marker của Linework Lite. Click vào nhân vật trong thế giới vẫn xác nhận theo luồng hiện có.

**Target Selection** đặt camera trước mặt mục tiêu theo hướng của nhân vật. Giữ **Orbit Angle = 0** để nhìn chính diện. Chỉnh **Camera Distance** để đổi khoảng cách cơ sở. Nếu cần đặt điểm ngắm riêng, dùng **Combat Camera Framing → Selection Anchor** và **Selection Bounds Size**. MountainDragon dùng xương đầu để căn riêng phần đầu. **Target Blend Seconds Per Meter** tăng nhẹ thời gian blend khi hai mục tiêu cách xa nhau, đến giới hạn **Target Blend Extra Limit**. Nếu Outline không sáng nhưng camera vẫn theo đúng người, kiểm tra component **CombatTargetSelection** và tính năng Linework trên URP Renderer đang dùng.

## 3. Enemy Attack

**Enemy Anticipation** dùng khi AI đã chọn chiêu và mục tiêu. **Enemy Execution** dùng từ lúc bắt đầu animation hoặc áp sát. **Enemy Recovery** dùng khi quay về hoặc kết thúc action. Góc phòng thủ bám Player bị nhắm, rồi căn thêm Enemy khi áp sát. **Target Weight** điều chỉnh mức camera hướng thêm về nhân vật thứ hai khi hai bên ở gần. Mỗi lần va chạm thêm phản hồi vào góc đang chạy.

Kiểm tra bằng đòn hai hit có hai cửa sổ parry và một đòn projectile. Camera phải theo cùng cặp nhân vật qua cả combo, phản hồi riêng từng lần chạm. Nếu khó đọc động tác, chỉnh **Enemy Execution → Orbit Angle/Follow Offset** và giảm mức rung của action.

## 4. Transition

Mỗi shot có **Blend Duration**, **Damping** và **Easing**. **Smooth** đi chậm ở hai đầu, **Ease In** tăng tốc dần, **Ease Out** giảm tốc dần, **Custom** đọc đường cong 0–1, **Cut** chuyển ngay. Khi liên tục đổi mục tiêu, blend mới bắt đầu từ pose đang chiếu. Để xem tâm ngắm và các điểm bao khung, chọn object **CameraManager** trong Play Mode, bật **Draw Framing Gizmos** và bật Gizmos trong Scene View.

Nếu thấy một góc camera cũ tranh quyền, xem **Cinemachine Brain → Show Debug Text**. Gameplay dùng **CM_CombatDynamic**. Prefab cinematic đang phát có thể chiếm quyền qua Timeline. Giữ **Channel 08** dành cho hệ thống này. Trong scene combat mới, gắn Brain vào Camera có tag **MainCamera**, gắn **CameraManager + CombatCameraDirector**, và gán asset Settings. Director tự tạo camera động.

## 5. Impact

Tạo profile bằng **Assets → Create → Combat → Camera → Impact Profile** và kéo vào **Action Data → Camera Impact Profile**. Điều chỉnh **Shake Strength**, **Shake Duration**, **Push In Degrees** và **Push In Duration**. Giá trị shake/push-in bằng 0 tắt hiệu ứng tương ứng. Profile trống dùng mức mặc định nhẹ. Có thể gắn **Impact Camera Controller** lên **CameraManager** để chỉnh hệ số riêng của địch và parry hoặc gán một profile chung cho trận.

**Player Hit Stop Duration** mặc định bằng 0. Ví dụ bắt đầu với 0.04 giây và **Player Hit Stop Speed** 0.08 cho một đòn nặng của Player. Hiệu ứng này tác động cục bộ lên Animator của Player khi hit xác nhận tại animation event. Projectile, Timeline, enemy animation và kết quả Parry tiếp tục chạy theo timing hiện có. Nếu parry khó nhìn, giảm rung cho enemy hoặc parry trước khi chỉnh phần timing.

## 6. Skill Timeline

Xem [hướng dẫn dựng prefab, gán Cinemachine Shot, Animation, VFX, Audio và Signal](SKILL_CINEMATIC_SETUP.md). Có thể triển khai từng skill riêng bằng cách gán asset vào **Action Data → Skill Cinematic**. Các skill chưa có asset dùng góc Player Action hoặc Enemy Execution được tính từ Battle Position. Trước khi dùng trên nhiều trận, thử cùng một asset với hai slot và hai mục tiêu có kích thước khác nhau.

Kỹ năng bật **Is Ultimate** dùng góc **CombatCameraSettings → Ultimate**. Góc này tự căn người tung chiêu và mục tiêu trong vùng còn thấy giữa hai dải đen. **Target Weight** dời điểm ngắm giữa hai nhân vật, từ `0` ở người tung chiêu đến `1` ở mục tiêu. **Camera Distance** là khoảng cách cơ sở. Đặt **Maximum Distance = 0** cho phép camera lùi đủ để thấy cả hai nếu đứng xa nhau. Thời gian chuyển góc lúc vào Ultimate chỉnh ở **CameraManager → Ultimate Transition Duration**. Nếu gán Timeline riêng, các Cinemachine Shot trên Timeline điều khiển máy trong thời gian phát.

## 7. Boss và vật cản

Khi chọn Boss, camera ưu tiên vùng thân. **Boss Wide** xét bounds đầy đủ và thêm các thành viên còn sống vào khung khi Boss chuyển phase hoặc lấy đà cho đòn AoE. Góc ra đòn sau đó bám Player bị nhắm. Có thể gắn **Combat Camera Framing** vào GameObject chứa **BattleUnit** của Boss. Gán **Focus Anchor** cho chọn mục tiêu, **Attack Anchor** cho lúc hành động và thêm các điểm cần thấy vào **Frame Anchors**. Dùng **Bounds Size/Center Offset** để đặt vùng khung thủ công.

**Obstruction Layers** mặc định nhận layer **Obstacle**. Gán collider của tường/cột vào layer được chọn và loại collider nhân vật khỏi mask. Kiểm tra một vị trí có tường giữa điểm ngắm và camera. Khi bị cản, camera thử góc cao hơn nếu vật cản nằm quá gần mục tiêu, rồi giới hạn vị trí trước collider. Nếu combat có sàn trên tầng hoặc môi trường chật, chỉnh **Obstruction Radius** và góc máy theo map.
