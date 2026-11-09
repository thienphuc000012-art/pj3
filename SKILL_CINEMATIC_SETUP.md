# Timeline camera cho từng kỹ năng

`Action Data → Skill Cinematic` nhận một asset tạo từ **Assets → Create → Combat → Skill Cinematic**. Một asset có thể gán cho nhiều kỹ năng. Bộ điều khiển tạo rig khi hành động bắt đầu và liên kết Cinemachine Track với Brain đang dùng trong combat. Khi Timeline hoặc hành động kết thúc, Brain tiếp tục dùng camera combat.

## Dựng rig

1. Tạo một GameObject tên `HeavySlashRig` ở gốc scene, Scale `1, 1, 1`. Gắn **Skill Cinematic Rig**. Component này yêu cầu **Playable Director**.
2. Tạo ba object con trống `AttackerAnchor`, `TargetAnchor` và `CombatCenter`. Gán chúng vào ba ô tương ứng trên **Skill Cinematic Rig**. Offset mặc định là `0, 1.25, 0` theo trục rig. Tăng **Target Anchor Offset Y** nếu muốn ngắm cao hơn vào Boss.
3. Để dựng thử trong Editor, đặt `AttackerAnchor` tại `(0, 1.25, -3)`, `TargetAnchor` tại `(0, 1.25, 3)`, `CombatCenter` tại `(0, 1.25, 0)`. Khi chạy thật, gốc rig nằm giữa hai Battle Position. Trục local Z hướng từ Battle Position của người ra đòn tới Battle Position của mục tiêu. Ba anchor dịch theo vị trí thật của nhân vật khi animation di chuyển.
4. Tạo các **Cinemachine Camera** dưới rig. Dùng **Position Control → None** cho góc đặt bằng Transform. Dùng **Tracking Target** cùng **Rotation Control → Rotation Composer** để máy hướng theo anchor. Nếu muốn máy bám người đang lao tới, dùng **Position Control → Follow** với `AttackerAnchor` và chọn **Binding Mode → Lock To Target With World Up** để offset xoay cùng trục của rig. Các camera trong rig dùng chung Main Camera của trận.

```text
HeavySlashRig                     SkillCinematicRig, PlayableDirector
├─ AttackerAnchor
├─ TargetAnchor
│  └─ ImpactVFX                    VisualEffect hoặc ParticleSystem
├─ CombatCenter
├─ CM_Windup                      CinemachineCamera
├─ CM_Dash                        CinemachineCamera
├─ CM_Impact                      CinemachineCamera
└─ Cinematic Audio                AudioSource, SignalReceiver
```

## Gắn Timeline

1. Chọn `HeavySlashRig` và mở **Window → Sequencing → Timeline → Create**. Lưu `HeavySlash.playable`. Trong Playable Director tắt **Play On Awake**, chọn **Update Method → Game Time**, **Wrap Mode → None**.
2. Thêm **Cinemachine Track** và kéo từng camera con lên track để tạo **Cinemachine Shot**. Khi xem trước trong scene, gán Brain của Main Camera vào ô binding. Lúc vào trận, binding này được gán lại tự động cho Brain của trận. Mỗi Shot phải trỏ đến camera nằm trong prefab rig.
3. Với Heavy Slash, có thể dùng `Windup 0.00–0.30s`, `Dash 0.24–0.65s` và `Impact 0.60–0.90s` làm mốc đầu tiên. Timeline bắt đầu trước bước áp sát của đòn cận chiến. Dịch các Shot theo thời gian áp sát và frame lấy đà, lao và va chạm của Animator hiện tại. Hai Shot chồng thời gian sẽ blend. Hai Shot nối sát nhau sẽ cut. Đặt **Ease In Duration** cho Shot đầu và **Ease Out Duration** cho Shot cuối để blend với góc gameplay. [Cinemachine và Timeline](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/concept-timeline.html)
4. Thêm **Animation Track** và gán Animator của một object con như đạo cụ hoặc pivot của camera. Record local position, local rotation hoặc thông số hình ảnh. Để Gameplay Animator tiếp tục phát animation đánh và Animation Event gây damage, hãy gán Animation Track vào object trong rig.
5. Thêm **Visual Effect Control Track** nếu dùng VFX Graph hoặc **Control Track** nếu dùng Particle System. Gán VFX trong prefab, chẳng hạn object con của `TargetAnchor`. Thêm **Audio Track** và gán AudioSource trong prefab. Thêm **Signal Track**, gán **Signal Receiver** của prefab và tạo các Signal như `ImpactFlash` hoặc `AudioCue` để phát hiệu ứng phụ tại frame va chạm. [VFX Graph và Timeline](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.3/manual/Timeline.html)
6. Kéo root vào Project để lưu thành prefab. Gán component **Skill Cinematic Rig** của prefab vào ô **Rig Prefab** trên asset **Skill Cinematic**, sau đó gán asset vào **Action Data → Skill Cinematic**. Timeline, clip và các binding nội bộ được lưu trên Playable Director của prefab. Xóa bản preview rig khỏi scene trước khi chơi.

## Kiểm tra trong Game View

- Dùng cùng một Action Data khi đổi người đánh hoặc đổi mục tiêu. Góc máy phải đổi vị trí cùng Battle Position, còn camera có Rotation Composer phải tiếp tục hướng theo anchor trong lúc nhân vật di chuyển.
- Nếu camera gameplay vẫn chạy xuyên suốt, mở Console và kiểm tra **Rig Prefab**, **Playable Asset**, từng **Cinemachine Shot → Virtual Camera** và Cinemachine Track có đang bị mute không. Nhớ lưu binding vào prefab.
- Nếu góc máy trở lại quá sớm, kiểm tra chiều dài Shot cuối và thời gian thực hiện hành động. Combat có thể kết thúc Timeline trước khi hết clip nếu lượt hoặc trận đã chuyển trạng thái.
- Nếu cùng một cú đánh phát hai VFX hoặc hai âm thanh, kiểm tra cả Action Data/Animation Event và track tương ứng. Chọn một nơi phát hiệu ứng đó để tránh trùng.
