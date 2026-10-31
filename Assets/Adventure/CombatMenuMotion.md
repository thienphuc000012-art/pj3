# Chuyển động thanh lệnh chiến đấu bằng DOTween

Đã gắn sẵn vào `Assets/Scenes/combattest.unity`. Dự án đã có DOTween và module UI trong `Assets/Plugins/Demigiant/DOTween`; không cần cài thêm thư viện.

## Code đầy đủ

- [BattleMenuButtonSlide.cs](../Scripts/combat/BattleMenuButtonSlide.cs): chuyển động, easing, nhận lựa chọn từ EventSystem và dọn tween.
- [BattleMenuStagger.cs](../Scripts/combat/BattleMenuStagger.cs): thứ tự xuất hiện và chọn nút đầu tiên.
- [CombatActionButtonFeedback.cs](../Scripts/combat/CombatActionButtonFeedback.cs): nền cọ, dấu chọn, âm thanh và flash đang dùng trong game.

Hai script đầu dùng được độc lập; không bắt buộc gắn `CombatActionButtonFeedback`.

## Hierarchy

```text
Canvas
├── ActionMenu                      [BattleMenuStagger, CanvasGroup]
│   ├── Attack                      [Button, Image trong suốt, BattleMenuButtonSlide]
│   │   └── Motion                  [RectTransform, CanvasGroup, Image trong suốt]
│   │       ├── Text (TMP)
│   │       ├── Action Ribbon       [Image: Distressed Black Grunge Banner]
│   │       ├── Ribbon Sweep
│   │       ├── Press Flash
│   │       └── Focus Diamond
│   ├── Skill                       [tương tự Attack]
│   ├── Item                        [tương tự Attack]
│   ├── Run Away
│   └── Attack (1)
└── EventSystem                     [StandaloneInputModule hoặc InputSystemUIInputModule]
```

Ba lớp nền/flash được tạo phía sau chữ khi chạy. Button ở ngoài giữ vùng tương tác; `Motion` bên trong kéo toàn bộ hình ảnh, icon và chữ ra. Ảnh trong suốt trên Motion giúp phần thanh đã trượt ra cũng nhận click. Các lớp trang trí không nhận raycast.

`Motion` có anchors Stretch cả hai chiều, offsets = 0, local scale = (1,1,1). Đặt góc nghiêng của thanh trên Button hoặc Motion trong Edit Mode. Script chỉ sửa anchored position của Motion, không sửa rotation, size hoặc scale. Hướng trượt được đổi từ trục ngang của root Canvas sang hệ tọa độ của parent, nên thanh nghiêng vẫn trượt ngang màn hình.

## Chỉnh trong Canvas hiện tại

1. Thoát Play Mode, chọn `Canvas → ActionMenu → Attack` (hoặc Skill/Item).
2. Trong `BattleMenuButtonSlide`, `Motion Target` trỏ tới `Motion` con; `Entrance Group` trỏ tới CanvasGroup của Motion.
3. Chỉnh kích thước, vị trí, góc nghiêng và thứ tự xếp chéo trực tiếp trên Canvas. Các giá trị được giữ nguyên khi chạy.
4. Chọn `ActionMenu → BattleMenuStagger`, kéo các nút vào `Buttons` theo thứ tự muốn xuất hiện. Danh sách đã gán Attack → Skill → Item → Run Away → Attack (1).
5. Chữ vẫn chỉnh trực tiếp ở `Motion → Text (TMP)`. Các trường Font Size/Auto Size của bạn được giữ nguyên.

## Tham số gợi ý

| Component / Trường | Mặc định | Tác dụng |
|---|---:|---|
| Slide Distance | 50 | Khoảng kéo sang phải, tính theo đơn vị root Canvas |
| Slide Out Duration | 0.24 | Tổng thời gian kéo ra và ổn định |
| Slide Out Ease | OutBack | Easing của nhịp kéo ra |
| Back Overshoot | 1.4 | Độ đàn hồi của OutBack |
| Overshoot Extra Pixels | 3 | Phần đi thêm trước khi dừng đúng đích |
| Slide In Duration | 0.18 | Thời gian thu về |
| Slide In Ease | InOutCubic | Easing khi bỏ chọn |
| Entry Offset | (-70, 0) | Vị trí bắt đầu của nhịp xuất hiện |
| Entry Duration | 0.22 | Thời gian một nút đi vào |
| Entry Ease | OutCubic | Easing khi đi vào |
| Stagger Interval | 0.055 | Độ trễ giữa hai nút kế tiếp |
| Select First Button | On | Cho phép dùng bàn phím/gamepad ngay khi menu mở |

50 đơn vị tương ứng 50 pixel khi Canvas hiển thị ở đúng độ phân giải tham chiếu; CanvasScaler có thể thay đổi số pixel thực tế.

Với bộ mặc định, 80% thời gian kéo ra dùng OutBack tới 53, rồi 20% cuối về đúng 50. Cả độ nảy của OutBack và đoạn đi thêm tạo đỉnh vượt khoảng 6–7 đơn vị so với đích. Đặt `Overshoot Extra Pixels = 0` để dùng một nhịp OutBack thuần đến 50; khi đó đỉnh vượt sẽ nhỏ hơn, khoảng 3–4 đơn vị với Back Overshoot = 1.4.

## Gắn vào menu khác

1. Tạo `ButtonSlot` có Button và Image trong suốt (`Raycast Target` bật). LayoutGroup, nếu có, chỉ điều khiển ButtonSlot.
2. Tạo con `Motion`, Stretch phủ ButtonSlot, thêm CanvasGroup và đặt hình nền, chữ, icon bên trong.
3. Gắn `BattleMenuButtonSlide` vào ButtonSlot, gán Motion Target và Entrance Group.
4. Bật Navigation Automatic hoặc gán Explicit Up/Down trên Button. Trong scene hiện tại, Up/Down đã nối thành vòng; không cần bắt phím riêng trong script.
5. Thêm `BattleMenuStagger` vào parent menu, gán danh sách các slider. Để `Play Entrance On Enable` trên từng slider tắt để không chạy hai lần.
6. Nếu dùng một nút độc lập không có stagger controller, bật `Play Entrance On Enable` trên slider đó.

Giữ các nút ngoài vùng cắt của Mask/RectMask2D nếu muốn phần trượt ra không bị cắt. Để dư ít nhất `Slide Distance + 10` ở mép phải của menu.

Chuột đi vào chọn Button qua EventSystem; khi chuột ra ngoài, thanh tự thu về dù EventSystem vẫn nhớ nút đó để điều hướng. Khi thực sự dùng bàn phím/gamepad (Navigate hoặc Submit), thanh được chọn giữ vị trí kéo ra đến khi đổi lựa chọn. Việc tự chọn nút đầu tiên khi mở menu chỉ chuẩn bị điều hướng, không tự kéo nút ra khi chuột chưa chạm. EventSystem cần UI input module đã cấu hình Navigate, Submit và Point cho các thiết bị đang dùng.

## Ngắt tween và vòng đời

Mỗi slider giữ riêng một Sequence cho focus và một Sequence cho entrance. Khi đổi lựa chọn, chỉ Sequence focus cũ của nút đó bị Kill(false), không ép hoàn thành. Tween mới lấy giá trị đang hiển thị làm điểm đầu. Hai offset focus và entrance được cộng vào một anchored position để tránh hai tween ghi đè cùng vị trí.

Nếu chọn một nút đang chờ stagger, phần entrance còn lại được đẩy nhanh trong 0.08 giây từ vị trí hiện tại. Đóng menu hoặc hủy object sẽ kill các tween do script sở hữu và trả Motion về vị trí ban đầu. `SetUpdate(true)` cho phép hiệu ứng chạy khi Time.timeScale = 0.

Trong game hiện tại, `CombatActionMenuTransition` chỉ fade parent khi có `BattleMenuStagger`; từng nút tự slide vào. Hiệu ứng cũ không còn dịch chữ thêm 6 đơn vị hoặc fade cùng CanvasGroup của Motion.

## Kiểm tra trong Play Mode

- Mở menu: các thanh vào lần lượt, cách nhau 0.055 giây.
- Dùng chuột hoặc Up/Down/gamepad: nút mới kéo ra, nút cũ thu về đúng gốc.
- Đổi qua lại thật nhanh giữa hai nút: không có bước nhảy về vị trí đầu hay tween chồng nhau.
- Đóng menu giữa animation rồi mở lại: vị trí gốc không bị cộng dồn.
- Thử một thanh nghiêng: chỉ vị trí ngang đổi, góc, cỡ chữ và kích thước giữ nguyên.
- Kiểm tra cả phần thanh nhô sang phải nhận click và không bị viewport cắt.

Chỉ gắn một hệ thống điều khiển vị trí lên Motion. Tắt các script khác đang ghi anchoredPosition, và không gọi DOKill chung lên Motion vì có thể hủy tween ngoài ý muốn.
