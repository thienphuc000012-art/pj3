# Exploration Scan — Unity 6 / URP 17

Hệ thống quét tông cyan công nghệ cao cho các map khám phá trong campaign. Bấm **Q** để phát một sóng hình cầu từ vị trí player. Sóng chạy trên bề mặt sàn, vách đá và các mesh có depth; khi chạm rương, item, NPC, enemy hoặc điểm tương tác, vật thể hiện outline sáng và marker tên/khoảng cách trong vài giây.

## Cấu hình đã tích hợp trong project

- Cấu hình dùng chung: `Assets/Resources/AdventureScanSettings.asset`.
- `CampaignSession.BindMap` tự thêm `AdventureScanner` vào player của map. Áp dụng cả `mapgame`, `m` và các map trong CampaignConfig.
- `WorldInteraction.OnEnable` tự thêm/đăng ký `AdventureScanTarget`. Giữ nguyên cấu hình nếu object đã có component này.
- `PC_Renderer` và `Mobile_Renderer` đã có `Adventure Scan Wave` và `Adventure Scan Highlights`.
- Feature outline riêng dùng Free Outline có sẵn trong project; không thay settings target selection của combat.
- Render Graph phải bật. Project hiện đang dùng Render Graph; không bật Compatibility Mode cho pass này.

Sau khi Unity import/biên dịch xong, vào map khám phá, đóng menu và bấm **Q**. Nếu muốn component hiện sẵn trong Hierarchy trước Play Mode, chọn object player có `PlayerScript` rồi dùng **Tools > Adventure > Scan > Add Scanner To Selected Player**, sau đó lưu scene.

## Kiến trúc và code C#

| File | Trách nhiệm |
| --- | --- |
| `Assets/Scripts/Adventure/AdventureScanSettings.cs` | ScriptableObject chứa cấu hình input, radius/cooldown, shader, outline, HUD/audio; `AdventureScanRuntime` cung cấp snapshot cho renderer. |
| `Assets/Scripts/Adventure/AdventureScanner.cs` | Input, điều kiện gameplay, tiến trình sóng, lọc bán kính/layer, phát hiện khi sóng chạm, thời gian reveal, quản lý rendering layer và outline runtime. |
| `Assets/Scripts/Adventure/AdventureScanTarget.cs` | Registry target đang được load, availability, mesh renderers, tên và anchor marker. Không cần collider để được scan. |
| `Assets/Scripts/Adventure/AdventureScanHUD.cs` | Pool marker, chiếu world position lên overlay canvas, hiện tên/khoảng cách và cooldown. Graphic kim cương mặc định không cần texture/font glyph đặc biệt. |
| `Assets/Scripts/Adventure/AdventureScanCooldownGraphic.cs` | HUD vòng tròn, vòng nạp theo chiều kim đồng hồ và điểm sáng có đuôi glow. Không cần texture hoặc Bloom. |
| `Assets/Scripts/Adventure/AdventureScanRendererFeature.cs` | Raster pass của URP Render Graph, yêu cầu depth, vẽ trước post processing, chỉ bật khi quét và đúng camera game. |
| `Assets/Shaders/AdventureExplorationScan.shader` | Khôi phục world position từ depth, band hình cầu + hologram grid + depth edge + scanlines. |
| `Assets/Editor/AdventureScanSetup.cs` | Menu chọn settings, thêm scanner vào player và thêm scan target vào object được chọn. |

Luồng thực thi:

```text
Q / InputAction / TryScan()
  → kiểm tra cooldown, menu, loading, encounter
  → chụp vị trí player làm tâm cố định
  → lấy registry các target trong chunk đang load
  → lọc layer + bán kính, ưu tiên target gần nhất, giới hạn số target
  → radius = min(maxRadius, elapsed × waveSpeed)
  → RendererFeature chiếu band cyan lên depth của camera
  → khi radius chạm target: bật outline + marker
  → hết revealDuration: gỡ highlight và tái sử dụng marker
```

Scan không làm player tấn công, nhận item, mở quest hay tương tác với enemy. Nó chỉ hiển thị thông tin. Object đã được nhặt/rương đã mở sẽ bị loại theo `WorldInteraction.IsConsumed`. Target bị unload hoặc disable cũng tự rời registry và mất marker.

## Inspector: cấu hình dùng chung

Mở **Tools > Adventure > Scan > Select Shared Scan Settings**, hoặc chọn asset trong Resources.

| Nhóm / thông số | Mặc định | Tác dụng |
| --- | --- | --- |
| Scan Enabled | On | Tắt/bật toàn hệ thống. |
| Scan Key | Q | Phím bàn phím mặc định, cũng là chữ hiện giữa vòng tròn. |
| Radius | 45 m | Bán kính quét, tính khoảng cách 3D. |
| Wave Speed | 22 m/s | Sóng đi hết 45 m trong khoảng 2.05 giây. |
| Cooldown | 8 s | Tính từ lúc quét; vòng sáng HUD chạy một vòng trong thời gian hồi. |
| Reveal Duration | 6 s | Mỗi target sáng trong 6 giây từ lúc sóng chạm nó. |
| Fade Duration | 0.6 s | Fade cuối sóng và marker khi sắp hết thời gian. |
| Max Detected Targets | 48 | Giới hạn công việc hiển thị, ưu tiên target gần. |
| Detection Layers | Everything | Physics/GameObject layer của target được phép phát hiện. |
| Require Line Of Sight | Off | Off cho phép phát hiện qua tường; On yêu cầu đường nhìn không bị chặn. |
| Obstruction Layers | Everything | Layer chặn Linecast, chỉ dùng khi bật Line Of Sight. |
| Scan Sound / Volume | Chưa gán / 0.6 | Âm thanh một lần khi quét; kéo clip SFX của bạn vào. |

Menu, hội thoại, encounter, loading và lúc stream chunk đang chặn chuyển động sẽ chặn input và hủy sóng/reveal hiện tại. Đóng menu không làm sóng cũ chạy lại. Cooldown và thời hạn target dùng thời gian thực; khi rời map scanner được tạo lại nên cooldown không được lưu trong save game.

## Inspector: player và input gamepad

Trên `AdventureScanner`:

- **Settings**: asset dùng chung, để trống sẽ tự tìm `Resources/AdventureScanSettings`.
- **Scan Origin**: mặc định root player. Có thể tạo child `Scan Origin` tại ngực, local Y khoảng 1.1, rồi kéo vào để sóng phát từ thân người.
- **Scan Camera**: camera game thật đang render; mặc định tự dùng Main Camera. Với Cinemachine, gán camera có CinemachineBrain, không gán virtual camera.
- **Scan Action**: tùy chọn Input Action Reference của Input System, action kiểu Button. Có thể bind gamepad như `<Gamepad>/rightStickPress`. Action hoạt động song song với Scan Key (mặc định Q). Scanner chỉ tự enable action nếu trước đó action đang disable, rồi trả lại khi component tắt.
- **On Scan Started**: nối âm thanh/animation/phản hồi khác nếu cần.
- **On Target Detected**: event nhận `AdventureScanTarget` vừa được phát hiện.

Một button UI hay script có thể gọi hàm public:

```csharp
AdventureScanner scanner = player.GetComponent<AdventureScanner>();
bool started = scanner != null && scanner.TryScan();
```

`TryScan` vẫn tuân theo cooldown và các điều kiện gameplay.

## Inspector: object giải đố và marker

WorldInteraction đã tự đăng ký. Với cần gạt, điểm leo, máy móc hoặc vật thể giải đố khác, chọn object rồi dùng **Tools > Adventure > Scan > Add Scan Target To Selection**.

- **Detectable**: bỏ tick để loại object khỏi scan.
- **Interaction**: tùy chọn; tên lấy từ displayName và trạng thái consumed lấy từ WorldInteraction. Object độc lập dùng tên GameObject.
- **Scan Anchor**: điểm để đo radius, hữu ích với mesh có pivot cách xa model.
- **Marker Anchor** và **Marker Offset**: vị trí marker. Mặc định phía trên bounds model; object không có renderer dùng pivot + 1.5 m.
- **Show Marker**: tắt marker riêng nhưng vẫn có thể giữ outline.
- **Marker Icon**: icon riêng; nếu để trống dùng Marker Sprite chung hoặc hình kim cương cyan mặc định.

Marker chỉ hiện khi nằm trong màn hình và trước camera, không hiện mũi tên ngoài mép màn hình. Kích thước marker tính theo Canvas tham chiếu 1920×1080. Tên và khoảng cách có thể tắt riêng trong shared settings. Canvas không bắt raycast nên không chặn thao tác chuột/UI khác.

HUD cooldown nằm ở góc dưới phải, là vòng tròn với **Q** ở giữa. Khi hồi, chữ mờ và điểm sáng chạy từ đỉnh vòng theo chiều kim đồng hồ, để lại đoạn viền đã nạp. Khi nạp đủ, vòng tròn sáng đầy và chữ sáng trở lại. Trong shared settings, chỉnh `Cooldown Position` để đổi vị trí; `Cooldown Size` (mặc định 72×72) để đổi đường kính; `Cooldown Ring Thickness`, `Cooldown Glow Width` và `Cooldown Key Font Size` để đổi độ dày viền, glow và cỡ chữ. Màu vòng lấy từ `Marker Color`.

## Tạo vẻ cyan công nghệ cao

Trong **Environment Hologram**:

- **Scan Color** là HDR. Tăng kênh G/B để có cyan; giữ R thấp. Giá trị mặc định `(0.05, 0.8, 1.4)` cho ánh xanh vừa phải.
- **Wave Width** 1.6 m tạo dải mép rõ, chạy dọc địa hình. Giảm còn 0.5–0.9 m cho đường quét mảnh hơn.
- **Wave Intensity** điều khiển dải chính; tăng vừa phải nếu trời sáng làm sóng khó nhìn.
- **Reveal Trail Width** 12 m là vùng hologram phía sau mép sóng. Không phải thời gian tồn tại outline của item.
- **Grid Scale** 1.5 nghĩa khoảng 1.5 ô mỗi mét. Giảm để lưới to hơn, tăng để chi tiết hơn.
- **Grid Intensity**, **Edge Intensity**, **Environment Tint** lần lượt điều khiển lưới, cạnh hình học nhìn thấy qua depth, và lớp màu nền. Để tint thấp để vẫn đọc được địa hình thật.
- **Hologram Scanline Density/Strength** thêm đường quét ngang nhẹ lên bề mặt, tránh biến cả màn hình thành noise.

Trong **Detected Object Outline**, chỉnh màu HDR, Width, Pulse Amount và Pulse Speed. Muốn glow mềm, bật HDR và Bloom nhẹ trong Volume của camera; scan pass và scan outline đã đặt trước post processing để Bloom có thể nhận ánh sáng. Nếu Bloom tắt, vẫn có viền cyan sắc.

**Outline Rendering Layer = 29** là bit dành riêng cho scan (không phải GameObject/Physics Layer). Hệ thống lưu và trả lại bit gốc của từng renderer khi hết reveal. Đổi giá trị này trước Play Mode; tránh các bit 24–27 đang dùng cho target selection combat.

## Shader và cách chuyển sang Shader Graph

Bản hiện tại dùng HLSL Full Screen Pass để dễ kiểm soát Render Graph và không phải đổi material của từng cây/vách đá. Shader chỉ cộng ánh sáng vào camera color bằng `Blend One One`; không copy scene color sang texture thứ hai.

Công thức chính:

```hlsl
worldPosition = ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP);
distanceFromPlayer = distance(worldPosition, scanOrigin);
behindWave = scanRadius - distanceFromPlayer;
band = 1 - smoothstep(0, waveWidth, abs(behindWave));
tail = step(0, behindWave) * saturate(1 - behindWave / trailWidth);
emission = scanColor * (band * waveIntensity + tail * tail * hologramDetail);
```

Tương đương trong Fullscreen Shader Graph: lấy world position được khôi phục từ scene depth, `Distance` với vector tâm, `Subtract` radius, `Absolute` + `Smoothstep` cho mép; nhánh tail dùng `Saturate`; nhân Color HDR rồi cộng lưới dựa vào `Fraction(worldPosition * gridScale)` và scanline dựa vào `Sine(worldY * density)`. Fullscreen Graph phải chạy trên renderer feature có yêu cầu Depth. Không thay bằng một sphere trong scene nếu cần sóng bám chính xác tất cả bề mặt nhìn thấy.

Lưới trong bản này là procedural world grid + depth edges, tạo cảm giác hologram. Nó **không phải wireframe tam giác thật của mesh**. Wireframe topology thật cần barycentric mesh data hoặc một material/shader bổ sung riêng. Camera depth chỉ chứa bề mặt opaque/alpha clip được camera nhìn thấy; kính/particle transparent thông thường và hình học nằm sau tường không được full-screen shader vẽ lại. Registry detection + outline vẫn có thể đánh dấu target sau tường khi Line Of Sight tắt.

## Chi phí và kiểm tra trong Unity

Pass toàn màn hình chỉ được enqueue trong lúc sóng đang chạy. Shader đọc depth ba lần mỗi pixel, không bật normals prepass và không copy camera color. Không tìm toàn scene mỗi frame, không bake mesh, không thay material gốc và không đòi collider mới. Registry chỉ chứa object của các scene/chunk hiện đang load; scan không ép tải chunk xa.

Kiểm tra nhanh sau import:

1. Chạy map, đứng gần một item/rương và NPC, bấm Q. Sóng lan từ player và từng marker bật khi mép sóng chạm target.
2. Bấm Q tiếp trước 8 giây: không phát scan mới; vòng sáng vẫn nạp dần. Khi vòng sáng đầy và chữ Q sáng, quét lại được.
3. Thử target sau tường, rồi bật Require Line Of Sight và thử lại.
4. Nhặt item/mở rương: marker của item đó biến mất và lần sau không scan lại.
5. Mở menu/hội thoại, vào encounter rồi quay về map: không còn outline hoặc marker bị giữ sai trạng thái.
6. Thử đổi Quality PC/Mobile, vùng đồi cao và mặt sàn; giảm Radius/Max Detected Targets/Grid Intensity nếu cần nhẹ hơn.

Các script runtime và editor đã được biên dịch riêng bằng compiler/thư viện Unity của project. Hình ảnh shader và cảm giác khi chơi cần kiểm tra trong Game View sau khi Unity import asset mới.
