# HP Party theo lượt

Scene `combattest`: `Canvas > HP Party > Party Slot 1..4 > Turn Visual`.

- `Portrait`: tự đổi giữa ảnh chờ và ảnh lượt theo nhân vật đang hành động.
- `HP Stain Frame`: dùng ảnh `hp stain`; thanh trên là HP, thanh dưới là Ultimate Energy riêng của từng nhân vật.
- Dòng số `HP / Ultimate Energy` không kèm tên nhân vật hoặc Stain. Số Ultimate tăng cùng thanh năng lượng và về 0 khi dùng Ultimate.
- `BuffContainer` và `Element Icon`: đi cùng cả cụm thẻ.

Chỉnh trực tiếp RectTransform của các phần tử con trên Canvas. `Turn Visual` do script điều khiển scale và vị trí trong lúc chơi; chỉnh offset cố định bằng các phần tử con.

Màu chữ của thẻ được giữ theo `TextMeshPro > Color` trên Canvas khi vào game, kể cả sau khi theme/ngôn ngữ cập nhật. Quy tắc chữ trắng chung không áp dụng lên thẻ HP Party.

Trong `AdvancedUIManager > Turn Order UI`:

- `Turn Portrait Size`: kích thước tối đa mỗi ảnh (64×64); tự thu nhỏ thêm khi thanh không đủ chỗ.
- `Turn Portrait Spacing`, `Turn Portrait Inset`: khoảng cách giữa các ảnh và lề bên trong thanh (12 và 8).
- `Turn Portrait Active Scale`: độ phóng lớn của ảnh đang đến lượt (1.2). Layout tính cả độ phóng lớn và phần khung để tránh chồng/lệch ra ngoài.
- `Show Turn Portrait Frames`: bật/tắt khung ảnh trong thanh thứ tự lượt.
- `Turn Portrait Frame Sprite`: ảnh khung riêng; để trống dùng khung trắng cắt góc, rỗng ở giữa.
- `Turn Portrait Frame Color`, `Padding`, `Thickness`: màu, khoảng nới và độ dày khung tự vẽ. Với sprite riêng, độ dày nằm trong ảnh.

Khung được tạo dưới từng portrait khi thanh lượt cập nhật, không đổi sprite, kích thước hoặc thứ tự nhân vật.

`Assets/Resources/PartyHUDStyle.asset`:

- `Active Scale`: kích thước lúc đến lượt (mặc định 1.22).
- `Transition Duration`: thời gian chuyển kích thước (0.18 giây).
- `Active Lift`: độ nâng thẻ đang hành động.
- `Portraits`: cặp ảnh thường / lượt, đối chiếu với tên object hoặc prefab. Tên hiển thị của nhân vật có thể đổi tự do.
- `HP Color`, `Ultimate Color`: màu số và màu phần thanh được nạp (màu viền nằm sẵn trong ảnh `hp stain`).

Trong `BattleUnit > Party HUD Portraits` có thể gán ảnh riêng để ưu tiên hơn bảng tên. Nếu chưa có ảnh lượt, dùng ảnh thường. Thẻ vẫn phóng lớn khi đến lượt.

`Tools > Adventure > UI > Update Party HP Ultimate Layout` chuyển các thẻ cũ từ HP/Stain sang HP/Ultimate, giữ vị trí slot và ảnh đã chỉnh trên Canvas.

Menu `Tools > Adventure > UI > Setup Party Turn HUD` dựng lại bố cục từ các giá trị mặc định. Chỉ chạy lại khi muốn dựng lại, vì các chỉnh sửa bố cục riêng của thẻ mới sẽ bị thay bằng mặc định. Công cụ lưu bản sao scene vào `Tools/CanvasUI/BeforePartyTurnHUD-*.unity`. HUD cũ được giữ tắt trong `HP Party > Legacy Party HUD`.
