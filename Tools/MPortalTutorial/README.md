# Mở đầu và hướng dẫn khám phá trong scene m

Đã thiết lập trực tiếp trong `Assets/Scenes/m.unity`, dùng Player, camera gameplay, scanner, menu, parkour và Save Point hiện có. Không thêm một Player, EventSystem hay campfire thứ hai.

## Luồng chơi

Lần đầu tới m trong game mới, Player xuất hiện **bên trong cổng** tại `(-87.2855682, -91.1699982, -7.56377697)`, yaw `124.340195`. Player đi thẳng 3,4 m bằng CharacterController trong 2,6 giây; ánh sáng cổng bắt đầu tắt khi đi được 0,8 m, giảm dần trong 0,65 giây. Khung đá và collider vẫn giữ lại.

Camera quay Player đi ra từ phía trước cổng, sau đó lia qua hai góc map (3,5 giây mỗi góc), trở lại camera gameplay trong 1,8 giây. Thời gian, vị trí và góc máy đều có thể chỉnh. Chỉ khóa điều khiển trong đoạn phim; luôn nhả khóa khi hoàn tất hoặc hủy đoạn phim.

Tiếp theo là 7 bài thực hành, dùng spotlight của hệ thống Guided Combat Tutorial:

1. **Di chuyển và nhảy** trong cùng một hint: WASD đủ 2 m và Space nhảy một lần; spotlight cả WASD/SPACE. Hai thao tác làm theo thứ tự nào cũng được; phần đã làm được lưu lại.
2. Scan bằng phím trong AdventureScanSettings (mặc định Q); chờ sự kiện scan thật.
3. Mở menu bằng P và đóng lại bằng P/Esc.
4. Đi tới **Cube (3)** gần cổng, hướng vào vật cản và W + Space để thực hiện parkour; chờ động tác kết thúc.
5. Đi tới **Save Point** hiện có, bấm E khi lời nhắc xuất hiện.
6. Giới thiệu chức năng lửa trại, đóng menu nghỉ bằng Esc để tiếp tục.
7. **Chạm trán kẻ địch**: tới Enemy Encounter tại `(-36.8455811, -85.8805084, -29.9340305)`, dùng E/chuột trái trong phạm vi hoặc để Enemy phát hiện và tấn công. Marker theo đúng Enemy đang di chuyển; hoàn tất khi encounter của Enemy đó được chốt.

Khung hướng dẫn cho phép chuột bấm xuyên xuống menu/rest. Enter hoặc click không tự bỏ qua bài thực hành. Đổi ngôn ngữ sẽ cập nhật bài đang mở.

## Chỉnh trong Hierarchy / Inspector

```text
m
└─ M Arrival Tutorial                      [MExplorationTutorial]
   ├─ Arrival Portal                       [PortalArrivalVisual]
   │  ├─ Portal Model                      (model low.fbx đã có trong scene)
   │  └─ Portal Glow                       (Point Light, không tạo shadow map)
   ├─ Arrival Poses
   │  ├─ Player Start                      điểm bắt đầu bên trong cổng
   │  ├─ Player Exit                       điểm đứng sau khi đi ra
   │  ├─ Walk Camera Start / End           camera nhìn Player đi ra
   │  └─ Map Pan 1 / Map Pan 2              vị trí + hướng nhìn khi lia map
   ├─ Arrival Camera                       CinemachineCamera dùng riêng cho đoạn phim
   └─ Exploration Tutorial Canvas
      ├─ Guided Exploration Tutorial
      │  ├─ Lesson                         nền, Step, Title, Instruction, Input Hint
      │  ├─ Spotlight
      │  └─ Shade 0…3
      ├─ Controls                          các ô phím WASD, SPACE, Q, P, E
      ├─ World Focus                       nhãn parkour/lửa trại + khoảng cách
      └─ Cinematic Bars
```

Chọn **M Arrival Tutorial** để chỉnh các thời lượng trong Arrival Poses, khoảng cách tắt cổng, FOV và khoảng thực hành di chuyển. Di chuyển/xoay các Transform dưới **Arrival Poses** để thay đổi góc máy. Sửa màu, ảnh nền, font, viền tại **Exploration Tutorial Canvas**. Nội dung bài học được cập nhật theo bước và ngôn ngữ trong `MExplorationTutorial.ShowLesson()`; không lấy nội dung preview trong Inspector làm nội dung cố định.

Material mới nằm trong `Assets/Adventure/Generated/MPortalTutorial`. Chỉ sửa import normal map của hai texture nguồn tương ứng; không thay material của asset khác. Mặt năng lượng không có collider; các phần đá dùng MeshCollider tĩnh, scale dương.

**Lưu ý:** không chạy lại lệnh Setup sau khi tự chỉnh camera/canvas, vì Setup là công cụ dựng mặc định và sẽ đặt lại các giá trị. Lệnh Validate chỉ kiểm tra, không dựng lại bố cục.

## Lưu tiến trình

Tiến trình nằm trong `CampaignSave.explorationTutorials`, id ổn định `m.portal-and-exploration.v1`, `curriculumVersion = 2`. Hoàn tất mỗi bài sẽ dùng cơ chế tự lưu sẵn có. Quay lại từ combat hoặc tải bản lưu đang học sẽ tiếp tục bài còn lại; đoạn đi ra cổng không phát lại. Tiến trình cũ được chuyển đúng bước mới; người đã xong bảy bài cũ chỉ nhận thêm hint encounter. Enemy đã được hoàn tất trước đó sẽ không bắt Player đánh lại.

Chọn **M Arrival Tutorial → Encounter Enemy** để đổi Enemy của hint cuối. Tham chiếu đã được gán trực tiếp tới Enemy sẵn có tại tọa độ yêu cầu; di chuyển Enemy sau đó vẫn giữ đúng mục tiêu. Dấu mốc hướng dẫn đi theo Enemy, còn tâm quét đấu trường vẫn do EnemyCombatOrigin quyết định như trước.

Bản lưu cũ đã ở m sẽ giữ nguyên vị trí và bỏ qua đoạn mở đầu mới. Để xem từ đầu, dùng một lượt **game mới** đi tới m (hoặc chế độ Start New Game hiện có trên Campaign Map Setup khi chủ động kiểm thử). Không cần xóa file save thủ công.

## Kiểm tra

Unity menu: **Tools → Adventure → Tutorial → Validate m Arrival Tutorial** trong Edit Mode, mở scene m.

Kết quả đầy đủ: `Validation.txt`; lượt cập nhật hint: `LessonsValidation.txt`; ảnh `Lesson-*` và `Portal-Materials-*`. Kiểm tra hint gồm gộp di chuyển/nhảy, lưu phần đã thực hiện, chuyển tám trạng thái tiến trình cũ, đủ 7 hint, chỉ đúng Enemy mới hoàn tất, hoàn tất ngay cả khi transition Busy, marker Enemy và spotlight WASD/SPACE, VI/EN, chữ không tràn và click xuyên spotlight. Lượt cập nhật hint không sửa lại camera/canvas/portal. Công cụ kiểm tra đầy đủ vẫn có các kiểm tra CharacterController qua cổng, góc máy và parkour trước đó.

Các kiểm tra dùng fixture tách biệt trong Edit Mode, không bắt đầu Play Mode hoặc ghi file save của người chơi. Cần chơi lượt mới để xem trọn chuyển động camera và animation theo thời gian thực trong Game View.

Trước khi sửa scene đã sao lưu cả file trên đĩa và trạng thái chưa lưu trong `Before-Scene-20261115-194048388`. Bản sao script trước thay đổi nằm trong `Before-20261115-191947907`. Những thư mục Before-Scene sau đó là các mốc thiết lập tiếp theo.
