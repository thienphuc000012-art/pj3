# Mở Kỹ năng và Vật phẩm bằng Ink Wipe

Hệ thống dùng UI UGUI, SoftMaskForUGUI **3.6.6** và DOTween đã có trong project. Nút Skills giữ listener `CombatManager.OnSkillsClicked`. Camera, lượt đánh, tài nguyên và bước chọn mục tiêu vẫn do CombatManager quản lý.

## Bắt đầu trong project này

1. Mở `Assets/Scenes/combattest.unity`, ở Edit Mode.
2. Chọn **Tools → Adventure → UI → Setup Skill Ink Transition** nếu Canvas chưa có component mới. Lệnh chỉ thêm component và nối reference, không sắp xếp lại các button hay thay hình nền của bạn. Lưu scene.
3. Chọn **Canvas → Skill Panel Transition Controller** để chỉnh chuyển động. Chọn **Brush Reveal Controller** ngay trên Canvas để chỉnh nét mực.
4. Play, tới lượt player rồi bấm **Kỹ năng** hoặc **Vật phẩm**. Nhấn chuột phải hoặc Esc để đóng ngay. EventSystem Cancel trên gamepad cũng được chuyển từ từng hàng về controller.
5. Sửa thiết kế tại `Canvas/SubMenuPanel/Skill Presentation` như trước, khi ở Edit Mode. Không chỉnh bản sao sinh ra lúc Play nếu muốn giữ thay đổi.

Không thêm listener OpenSkillPanel bên cạnh listener OnSkillsClicked hiện có. Gọi hai luồng cùng lúc sẽ bỏ qua hoặc lặp phần chuẩn bị dữ liệu chiến đấu.

## Các file

| File | Nhiệm vụ |
|---|---|
| [SkillPanelTransitionController.cs](../Scripts/combat/SkillPanelTransitionController.cs) | Trạng thái Closed/Opening/Open/Closing, timeline chung, khóa input, stagger, mở/đóng/hủy |
| [BrushRevealController.cs](../Scripts/combat/BrushRevealController.cs) | Tạo wrapper SoftMask, quét tích lũy, vệt mực, tùy chọn Sprite |
| [SkillItemAnimator.cs](../Scripts/combat/SkillItemAnimator.cs) | Offset và alpha từng hàng, khôi phục đúng vị trí, chuyển Cancel từ EventSystem |
| [InkRevealGraphic.cs](../Scripts/combat/InkRevealGraphic.cs) | Sinh mesh mép mực xước và vùng mask tích lũy bằng UGUI |
| [SkillInkTransitionSetup.cs](../Editor/SkillInkTransitionSetup.cs) | Nối reference vào Canvas hiện có |
| [SkillInkTransitionValidation.cs](../Editor/SkillInkTransitionValidation.cs) | Kiểm tra UI riêng biệt, không chạy combat hoặc sửa save game |

Code đầy đủ nằm trong các file trên. Các thay đổi tích hợp nhỏ nằm ở AdvancedUIManager, CombatManager, CombatSkillRowUI, BattleMenuButtonSlide và BattleMenuStagger.

## Cấu trúc Canvas

Trong Edit Mode giữ hierarchy bạn đang dùng:

```text
Canvas
  AdvancedUIManager
  SkillPanelTransitionController
  BrushRevealController
  ActionMenu
    Attack
    Skill               Button → CombatManager.OnSkillsClicked
      Motion            hình nền + chữ, đang dùng BattleMenuButtonSlide
    Items
  SubMenuPanel
    Viewport            nội dung Items hiện có
    Skill Presentation
      Rows
        Skill Row Template (tắt, dùng để tạo 4 Skills + 1 Ultimate)
      Description Panel
```

Lúc mở Skills, wrapper được tạo một lần:

```text
SubMenuPanel
  Viewport
  BrushMask             CanvasRenderer + InkRevealGraphic + Coffee.UISoftMask.SoftMask
    Skill Presentation  CanvasGroup
      Rows
        Skill Slot 1    SkillItemAnimator + CombatSkillRowUI + Button
        Skill Slot 2
        Skill Slot 3
        Skill Slot 4
        Ultimate Slot
      Description Panel
  InkBrushOverlay       cùng Canvas, vẽ sau BrushMask
    Full Height Ink Front
```

Wrapper nhận anchors, pivot, kích thước, rotation và scale ban đầu của panel. Panel bên trong stretch theo wrapper, giữ nguyên vị trí và hình dạng hiển thị. Khi mở Vật phẩm, hệ thống tạo một BrushMask riêng theo Viewport, giữ nguyên tham chiếu cuộn và khung cắt của danh sách.

`InkBrushOverlay` lấy đúng kích thước và góc xoay từ BrushMask. Mép mực phủ từ trên xuống dưới của panel và dùng chung tiến độ với mask. Phần mực đậm chạy trước mép reveal để các hàng đã hiện vẫn dễ đọc. Overlay không nhận raycast và được vẽ sau BrushMask trong cùng Canvas.

## Reference và thông số Inspector

**Skill Panel Transition Controller** đặt trên Canvas, bên ngoài panel được ẩn:

| Reference | Gán |
|---|---|
| Skills Button | Button `ActionMenu/Skill` |
| Skills Panel | RectTransform `SubMenuPanel/Skill Presentation` |
| Skill Items Root | `Skill Presentation/Rows` |
| Command Menu | RectTransform `ActionMenu` |
| Brush Reveal | BrushRevealController trên Canvas |
| Items Button | Button `ActionMenu/Item`, tìm từ listener `CombatManager.OnItemsClicked` |
| Items Viewport | Viewport của ScrollRect trong `SubMenuPanel` |
| Skills Direction / Items Direction | Hướng quét riêng của từng bảng, mặc định Right To Left |

Timeline mặc định:

| Khoảng thời gian | Chuyển động |
|---|---|
| 0–0,12 giây | Button trượt thêm 24 đơn vị Canvas, OutBack, overshoot 1,4 |
| 0,10–0,40 giây | Mép mực và mask quét đồng bộ theo hướng đặt cho bảng, Linear |
| 0,13–0,42 giây | 5 hàng lần lượt trượt 24 đơn vị về vị trí gốc, mỗi hàng 0,17 giây, lệch nhau 0,03 giây |
| 0,48 giây | Mở input, highlight hàng có thể sử dụng đầu tiên |
| Đóng | Hủy wipe đang chạy và trả về command menu ngay |

Tăng `Stagger Delay` lên 0,04–0,07 nếu muốn các hàng tách nhịp rõ hơn. Controller tự kéo dài thời gian tổng để hàng cuối chạy xong rồi mới cho tương tác. `Minimum Open Duration` là thời gian tối thiểu, không cắt ngắn các track khác.

`Button Slide Distance` là offset cộng thêm vào chuyển động hover hiện tại. `BattleMenuButtonSlide` có kênh offset riêng, không có hai tween cùng giành quyền ghi một RectTransform.

**Brush Reveal Controller**:

- `Prefer Soft Mask`: dùng mask mềm nếu có shader tương thích cho toàn bộ nội dung.
- `Edge Roughness`: độ gồ ghề mép mở, mặc định 28. Giảm nếu nét cắt quá răng cưa.
- `Feather Width`: vùng mềm ở mép, mặc định 5. Giữ nhỏ để mực sắc nét.
- `Edge Segments`: số đoạn chia theo chiều cao, mặc định 64.
- `Ink Front Width`: độ dày ngang của mép mực, mặc định 126 đơn vị UI. Chiều cao tự khớp với Skill Presentation.
- `Ink Head Angle`: độ nghiêng của đầu mực, từ −30° đến 30°. Số dương nghiêng đầu trên về phải.
- `Flip Ink Head`: lật ngang hình vệt mực. Cả hai tùy chọn chỉ đổi lớp hình, giữ nguyên hướng và tiến độ mở bảng. Vật phẩm dùng chung thiết lập hình mực.
- `Visual Brush Sprite`: ảnh cọ tùy chọn được đặt theo chiều cao panel. Bỏ trống để dùng mép mực xước sinh bằng mesh.
- `Brush Tint`: màu và độ đậm của mực. Với ảnh cọ có sẵn màu, dùng trắng để giữ màu gốc.
- `Standalone Duration`: chỉ áp dụng khi gọi PlayBrushReveal/PlayReverseBrushReveal trực tiếp, không ghi đè timeline trong controller chính.

Hàng kỹ năng hiện tại dùng anchors cố định nên có thể dịch chuyển RectTransform của chính hàng. Nếu tái sử dụng `SkillItemAnimator` dưới LayoutGroup, gán `Motion Target` là một object con chứa phần hình, tránh tween RectTransform đang bị LayoutGroup điều khiển.

## SoftMask và reveal tích lũy

Vùng mask là toàn bộ phần **từ cạnh trái đến mép quét hiện tại**. Mép này có biên dạng cố định theo chiều cao và chỉ tiến ngang. Vì vậy một điểm đã được mở sẽ tiếp tục hiển thị khi tiến độ tăng.

SoftMask 3.6.6 đọc mesh, UV và alpha của texture bằng shader nội bộ. Không nên chỉ thêm `_Progress` vào material của ảnh mask rồi mong SoftMask dùng shader đó. Implementation này cập nhật geometry của `InkRevealGraphic`, gọi SetVerticesDirty/SetSoftMaskDirty, và dùng một texture alpha ramp nhỏ để làm mềm mép. Vùng đã mở có alpha 1. Ở 0% mask rỗng, ở 100% mask được tắt để tránh clipping phần hover và không tiếp tục tốn buffer render.

Thiết lập SoftMask được thực hiện bởi BrushRevealController:

```csharp
softMask.maskingMode = SoftMask.MaskingMode.SoftMasking;
softMask.showMaskGraphic = false;
softMask.alphaHitTest = false;
softMask.downSamplingRate = SoftMask.DownSamplingRate.x1;
softMask.SetSoftMaskDirty();
```

Namespace đúng của bản đang cài là `Coffee.UISoftMask`. `Maskable` trên Image/TMP phải bật. Không đặt Canvas con với `Override Sorting` dưới mask vì có thể tách khỏi vùng mask cha. Canvas Scaler của project vẫn quản lý tỉ lệ UI.

Các shader mẫu chính thức cho TMP Unity 6 được đặt tại:

`Assets/Samples/SoftMaskForUGUI/3.6.6/TextMeshPro Support (Unity 6)`

Nếu chuyển sang project khác, import **TextMeshPro Support (Unity 6)** từ Samples của package. Nếu thiếu shader tương ứng, controller chuyển về `Normal` stencil và cảnh báo một lần để chữ vẫn hiện đúng. Chế độ này giữ mép xước nhưng không có phần feather mềm. Custom material khác cũng cần biến thể SoftMaskable phù hợp.

Trước khi build, mở panel qua các trạng thái trong Editor để package ghi các shader variant vào UISoftMaskProjectSettings, rồi kiểm tra Development Build. Không chỉ kiểm tra bằng Shader.Find trong Editor vì build còn có bước loại shader không dùng. Không cần custom shader mới cho tiến độ của hiệu ứng này.

## Tự làm texture PNG 2048 × 512

Default hoạt động ngay bằng mesh UGUI, không cần mua asset. Nếu muốn tự vẽ nét mực:

1. Tạo ảnh RGBA 2048 × 512 với nền alpha trong suốt trong Krita, GIMP hoặc phần mềm vẽ bạn dùng.
2. Vẽ mực đen theo chiều ngang, dài từ gần cạnh trái tới cạnh phải, dày ở thân và nhọn lệch ở đuôi.
3. Dùng cọ khô hoặc eraser để tạo khe xước dài mảnh, mép không đều. Để vài giọt/mảnh nhỏ tách khỏi nét chính, không thêm chữ hay ký hiệu.
4. Export PNG RGBA, giữ alpha. Không flatten lên nền đen hoặc trắng.
5. Import Unity: Texture Type **Sprite (2D and UI)**, Mesh Type **Full Rect**, Max Size 2048, Alpha Is Transparency bật, Wrap Clamp, Filter Bilinear, Mip Maps tắt, Compression None khi kiểm tra chất lượng, sRGB bật cho ảnh trang trí.
6. Gán vào `Visual Brush Sprite`. Điều chỉnh `Ink Front Width` để nét mực không che quá lâu phần chữ.

Phân biệt:

| Thành phần | Màu/alpha | Vai trò |
|---|---|---|
| Brush Visual Texture | Mực đen, nền trong suốt | Hình cọ chạy trên UI |
| Mask đang dùng | Mesh trắng + alpha ramp | Quyết định nội dung nào được hiển thị |
| Brush Mask Texture tự vẽ, nếu dùng ở hệ khác | Trắng trong vùng hiện, alpha 0 ngoài vùng | Cần cơ chế mở tích lũy riêng, không chỉ di chuyển ảnh |

Không gán sprite mực đen vào mask đang dùng. Các lỗ xước trang trí không nên trở thành những lỗ vĩnh viễn trên chữ của panel.

## Timeline, gọi hàm và hủy

DOTween Sequence chạy một clock unscaled riêng, Linear. SampleOpen tách clock thành các khoảng chồng nhau rồi áp dụng OutBack/OutCubic cho từng track. Controller tự cập nhật duy nhất sequence này, giới hạn mỗi bước bằng `Maximum Frame Step` (mặc định 0,05 giây), để một frame tải scene/camera quá lâu không làm hiệu ứng nhảy thẳng đến cuối. Không đổi Time.timeScale hoặc clock của tween khác:

```csharp
sequence = DOTween.Sequence().SetUpdate(UpdateType.Manual, true).SetTarget(this);
sequence.Append(DOTween.To(() => clock,
    time => { clock = time; SampleOpen(time); },
    duration, duration).SetEase(DG.Tweening.Ease.Linear));
// Trong Update, bỏ qua frame vừa bấm nút:
float step = Mathf.Min(Time.unscaledDeltaTime, maximumFrameStep);
sequence.ManualUpdate(step, step);
```

Các API của controller:

- `OpenSkillPanel()`: mở phần trình bày đã có dữ liệu. Trong combat hãy đi qua `CombatManager.OnSkillsClicked()` để chuẩn bị đúng skills/camera/state.
- `TryOpenPanel(true)`: mở hiệu ứng cho danh sách Vật phẩm đã được tạo.
- `PreparePanels()`: tạo trước hai mask trong lúc mở đầu trận. AdvancedUIManager tự gọi sau khi scene đã khởi tạo, kể cả khi vào trận từ map qua loading.
- `CloseSkillPanel()`: đóng bảng hiện tại và trả về command menu ngay.
- `ResetTransition()`: hủy ngay, khôi phục pose và tắt panel. Dùng khi chọn kỹ năng hoặc ẩn HUD.
- `BrushRevealController.PlayBrushReveal()` và `PlayReverseBrushReveal()`: thử mask riêng sau Initialize.
- `BrushRevealController.CancelPlayback()`: dừng coroutine riêng để controller chính lấy quyền điều khiển.

Mỗi controller giữ một sequence hoặc coroutine. Khi đóng giữa lượt quét, nó dừng animation, tắt mask và khôi phục điều khiển. OnDisable dừng hiệu ứng. Unity dọn các đối tượng sinh thêm cùng cây Canvas khi rời scene. Trong lúc quét, CanvasGroup khóa thao tác trên panel và command menu, còn danh sách Vật phẩm mở lại cuộn ngay khi quét xong.

Input System/Gamepad: gán Submit/Cancel/Move trong EventSystem `InputSystemUIInputModule`. Khi đang quét, controller là selection tạm để nhận Cancel. Khi mở xong, từng SkillItemAnimator chuyển Cancel về controller. Với Input Actions riêng của game, callback Cancel có thể gọi `CombatManager.CancelCurrentAction()`. Không thêm hai InputModule cùng xử lý một input.

Nếu project tái sử dụng không có DOTween, bỏ define `DOTWEEN` để controller dùng coroutine với Time.unscaledDeltaTime. Project hiện tại còn có các script cũ dùng DOTween trực tiếp, vì vậy không gỡ DOTween khỏi toàn project chỉ để thử fallback của hệ mới.

## Kiểm tra và xử lý lỗi

Chọn **Tools → Adventure → UI → Validate Skill Ink Transition** ở Edit Mode. Bộ kiểm tra tạo UI tạm, không ghi scene đang mở hoặc save game. Báo cáo nằm trong `Library/CombatValidation/SkillInkTransition.result.txt`, hình kiểm tra trong `Library/CombatValidation/SkillInkPreviews`.

Kiểm tra trong game thêm các trường hợp:

1. Mở Skills, Back lúc vừa bắt đầu và khi đang hiện các hàng, mở lại nhiều lần.
2. Trong lúc mở, bấm Submit hoặc một hàng. Không được dùng skill trước khi quét xong.
3. Dùng skill để chuyển chọn mục tiêu. Không được còn nét mực hay callback mở command menu muộn.
4. Cancel bằng chuột phải, Esc và gamepad. Quay lại command menu, rồi mở Items.
5. Kiểm tra 16:9, 4:3, 21:9 và thay đổi cửa sổ. Các phần đã reveal không mất lại, chữ/icon không đổi scale hoặc rotation.
6. Kiểm tra player thiếu tài nguyên và ô chưa trang bị. Transition không được biến ô đó thành sử dụng được.

| Triệu chứng | Kiểm tra |
|---|---|
| Chữ thành ô hoặc mất khi quét | TMP sample Unity 6, shader variant và Maskable. Thử tắt Prefer Soft Mask để so sánh stencil |
| Chữ hiện trước icon hoặc vượt mask | Custom shader thiếu SoftMaskable, Canvas con overrideSorting, hoặc graphic tắt Maskable |
| Toàn panel bật ngay | Skills Button đang gọi sai listener hoặc một script khác tự SetActive/mở CanvasGroup trong khi đang chuyển cảnh |
| Hàng giật trở lại vị trí cũ | Có LayoutGroup/animation khác điều khiển cùng RectTransform. Dùng Motion Target con |
| Mực biến mất nhưng panel cũng bị che lại | Đang di chuyển sprite thay cho mask tích lũy. Để BrushRevealController điều khiển InkRevealGraphic |
| Text mờ khi mở nhưng không sáng lại | Kiểm tra CanvasGroup alpha. CombatSkillRowUI đã có multiplier riêng cho transition, không tween thêm cùng alpha |
| Trong build mất mask, Editor vẫn đúng | Kiểm tra shader variants trong UISoftMaskProjectSettings và thử Development Build |
| Chậm khi dùng nhiều panel | Chỉ giữ mask đang transition. Hệ này tắt SoftMask ở 100% và khi đóng |

Tài liệu gốc: [SoftMaskForUGUI](https://github.com/mob-sakai/SoftMaskForUGUI), [DOTween](https://dotween.demigiant.com/documentation.php). Shader TMP sample giữ bản quyền và giấy phép MIT kèm trong thư mục Samples.
