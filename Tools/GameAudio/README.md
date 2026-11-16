# Hệ thống âm thanh

Mở **Tools → Adventure → Audio → Select Audio Settings**, hoặc chọn:

`Assets/Resources/Adventure/Audio/GameAudioSettings.asset`

Các clip mới để trống theo yêu cầu. Kéo clip vào **Clips**, tăng Size nếu muốn nhiều biến thể; mỗi lần phát chọn một clip. Mỗi tiếng có Volume, khoảng Pitch ngẫu nhiên, Spatial Blend (0 = 2D, 1 = 3D), Max Distance.

| Âm thanh | Nơi gán/chỉnh |
| --- | --- |
| Title, Intro, mapgame, m, Battle mặc định, Campfire | GameAudioSettings → Nhạc nền mặc định: Clip / Volume. Music Fade Seconds chỉnh thời gian chuyển bài. |
| Từng Discovery Area | Chọn WorldInteraction loại Discovery → Discovery Area Music. Nhạc áp dụng trong bán kính Range, độc lập với XP và hồi chiêu banner. Vùng giao nhau dùng vùng có tâm gần player nhất. |
| Từng encounter/quái | BattleEncounterSetup → Encounter Music → Battle Music. Để trống dùng Battle chung. |
| Campfire riêng | WorldInteraction loại Save Point → Campfire Music Override. Nhạc bật khi mở menu campfire, đóng menu trở lại nhạc vùng/map. |
| Walk, Run, Jump, Skill, Parry, Hurt, Die của player | PlayerAudio.asset. Có thể gán profile riêng ở PlayerScript → Audio Profile và BattleUnit → Audio Profile trên prefab combat. |
| Hurt, Die, Skill của enemy | EnemyAudio.asset hoặc BattleUnit → Audio Profile riêng trên prefab quái. |
| Growl khi tuần tra | EnemyAudio.asset → Growl và Growl Interval. EnemyEncounterBehaviour → Audio Profile ghi đè riêng từng quái trên map. |
| Riêng từng kỹ năng | ActionData → Audio → Use Sound. Phát khi bắt đầu animation hành động, sau khi áp sát. Để trống dùng Skill trong profile nhân vật. |
| Nhặt đồ, mở rương, scan, chế tạo | GameAudioSettings → Tương tác và UI → Pickup / Chest Open / Scan / Craft. Pickup/chest phát ở vật tương tác, đúng mốc animation; Craft chỉ phát khi chế tạo thành công. |
| Nút ActionMenu | GameAudioSettings → Action Menu Button. Các nút UI khác dùng Common Button. |

**Bước chân:** mặc định phát theo quãng đường player thực sự di chuyển trên đất; chạy dùng Run, đứng yên/nhảy không lặp bước chân. Battle entry walk cũng có bước chân. Nếu clip là một đoạn bước chân dài, bật **Loop Movement Audio** trong profile để lặp khi đang di chuyển và dừng khi đứng lại. Nếu dùng từng clip ngắn theo animation, bật **Footsteps By Animation Event** và thêm event `Footstep` / `PlayFootstepSound` vào Animator cùng GameObject với ActorAudio. Không bật đồng thời hai chế độ. Walk Step Distance / Run Step Distance chỉnh nhịp theo quãng đường.

**Options:** Settings ở Title và Options trong Menu dùng chung prefab:

`Assets/Resources/Adventure/UI/GameOptions.prefab`

Có 5 thanh Master / Music / Voice / User Interface / Special effects, kéo thanh hoặc bấm mũi tên để đổi 5%; đổi Tiếng Việt/English; Back/Esc/P đóng (P chỉ trong Menu). Âm lượng và ngôn ngữ được lưu vào PlayerPrefs, không phụ thuộc file save. Các giá trị mặc định trong Settings chỉ dùng khi chưa có lựa chọn đã lưu; Volume riêng từng clip luôn là hệ số bổ sung.

Hurt/Die/Growl thuộc Voice; bước chân/skill/parry/tương tác thuộc SFX. Âm thanh trong prefab VFX hiện có cũng theo SFX. Nếu muốn một AudioSource có sẵn thuộc nhạc/giọng nói, thêm **GameAudioSourceBus** lên chính GameObject AudioSource và chọn Bus. Hệ thống giữ một AudioListener theo camera đang dùng, kể cả Intro.

Scan/parry/nút bấm giữ âm thanh cũ khi clip mới chưa gán. Các ô âm thanh khác không phát cho đến khi bạn thêm clip.

Kiểm tra: **Tools → Adventure → Audio → Validate Audio and Options**. Báo cáo tại `Tools/GameAudio/Validation.txt`; các ảnh Options Việt/Anh có dấu thời gian. Kiểm tra ở Edit Mode, không chạy campaign và không thay đổi save game hoặc tùy chọn âm lượng đã lưu.

Ảnh giao diện đã kiểm tra trực quan: [Tiếng Việt](Options-Vietnamese-Verified.png), [English](Options-English-Verified.png).
