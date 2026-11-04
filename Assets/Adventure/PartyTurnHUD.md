# HP Party theo lượt

Scene `combattest`: `Canvas > HP Party > Party Slot 1..4 > Turn Visual`.

- `Portrait`: tự đổi giữa ảnh chờ và ảnh lượt theo nhân vật đang hành động.
- `HP Stain Frame`: dùng ảnh `hp stain`; thanh trên là HP, thanh dưới là Ultimate Energy riêng của từng nhân vật.
- Dòng số `HP / Ultimate Energy` không kèm tên nhân vật hoặc Stain. Số Ultimate tăng cùng thanh năng lượng và về 0 khi dùng Ultimate.
- `BuffContainer` và `Element Icon`: đi cùng cả cụm thẻ.

Chỉnh trực tiếp RectTransform của các phần tử con trên Canvas. `Turn Visual` do script điều khiển scale và vị trí trong lúc chơi; chỉnh offset cố định bằng các phần tử con.

`Assets/Resources/PartyHUDStyle.asset`:

- `Active Scale`: kích thước lúc đến lượt (mặc định 1.22).
- `Transition Duration`: thời gian chuyển kích thước (0.18 giây).
- `Active Lift`: độ nâng thẻ đang hành động.
- `Portraits`: cặp ảnh thường / lượt, đối chiếu với tên object hoặc prefab. Tên hiển thị của nhân vật có thể đổi tự do.
- `HP Color`, `Ultimate Color`: màu số và màu phần thanh được nạp (màu viền nằm sẵn trong ảnh `hp stain`).

Trong `BattleUnit > Party HUD Portraits` có thể gán ảnh riêng để ưu tiên hơn bảng tên. Nếu chưa có ảnh lượt, dùng ảnh thường. Thẻ vẫn phóng lớn khi đến lượt.

`Tools > Adventure > UI > Update Party HP Ultimate Layout` chuyển các thẻ cũ từ HP/Stain sang HP/Ultimate, giữ vị trí slot và ảnh đã chỉnh trên Canvas.

Menu `Tools > Adventure > UI > Setup Party Turn HUD` dựng lại bố cục từ các giá trị mặc định. Chỉ chạy lại khi muốn dựng lại, vì các chỉnh sửa bố cục riêng của thẻ mới sẽ bị thay bằng mặc định. Công cụ lưu bản sao scene vào `Tools/CanvasUI/BeforePartyTurnHUD-*.unity`. HUD cũ được giữ tắt trong `HP Party > Legacy Party HUD`.
