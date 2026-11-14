# Leo gờ ở scene m

## Điều khiển

- **Space**: bám gờ khi có điểm bám hợp lệ, hoặc hạ người bám mép từ trên xuống.
- **W + Space**: Hop Up. Giữ W được; mỗi lần nhấn Space thực hiện một hop.
- **S + Space**: Hop Down.
- **Space ở gờ cuối**: leo lên mặt trên nếu có chỗ đứng hợp lệ.
- **A / D**: shimmy trái/phải. **F**: thả gờ.

PlayerClimb nhận phím trước FarkourController. Khi dùng Space cho leo, lần nhấn đó không kích hoạt đồng thời nhảy/parkour. Nếu không có điểm bám hợp lệ, Space vẫn dùng cho nhảy thường.

## Inspector

Chọn player → **Player Climb**:

- **Climb Key**: Space, có thể đổi lại.
- **Hop Up Offset → Up Hop Up Pos = -0.0434 m**: hạ tay 0.2414 m so với bản 0.198 m trước đó.
- **Hop Down Offset → Up Hop Down Pos = -0.0404 m**: hạ tư thế khoảng 0.2384 m theo hai ảnh Y=-54.98161 và Y=-55.22.
- **Forward Hop Up/Down Pos = -0.05 m**: độ sâu của tay so với gờ.
- **Hop Down Detection → Hop Down Max Drop = 2.5 m**: tầm tìm gờ thấp hơn. **Probe Step = 0.1 m** là khoảng cách giữa các tia, không phải tầm nhảy.
- **Hop Probe Outset = 0.8 m**: bắt đầu tia ngoài gờ nhô ra. Ray Hop Length vẫn tính tầm từ gờ hiện tại.
- **Climb Input Buffer Seconds = 0.2 s**: đệm một lần nhấn khi hop trước đang hồi tư thế.

Chọn player → **Shimmy Controller → Hanging Foot Clearance**:

- **Keep Feet Outside Wall**: chỉnh tư thế chân sau animation, giữ nguyên root và tay.
- **Foot Wall Padding = 0.06 m**: khoảng hở tối thiểu của xương bàn chân/mũi chân với mặt phẳng tường. Tăng nhẹ nếu giày của model dày hơn.
- **Max Foot Wall Correction = 0.45 m**: giới hạn chỉnh mỗi chân, cần đủ lớn cho clip shimmy phải có chân xuyên sâu vào tường.

Chọn player → **Shimmy Controller → Hanging Hand Clearance**:

- **Keep Hands Outside Wall**: giữ bàn tay và ngón tay dưới mép gờ ở ngoài mặt tường khi shimmy, áp dụng cho cả hai hướng.
- **Hand Wall Padding = 0.025 m** và **Max Hand Wall Correction = 0.3 m**: khoảng hở tay và giới hạn chỉnh cánh tay. Ngón tay ở phía trên mép gờ vẫn được vòng vào để bám.
- **Pose Correction Smooth Time = 0.08 s**: làm mềm lúc giảm phần chỉnh tay/chân và khi đổi mặt gờ. Khi chi đi vào mặt tường, giới hạn va chạm được xử lý ngay để tránh xuyên tường.

Chỉnh chân hoạt động liên tục xuyên suốt Grab/Hop/blend về Hang; không đợi hết Hop mới bật. Khi gờ đích nhô ra, hệ thống chuẩn bị khoảng hở trước khi đổi anchor. `idle to hang` dùng cùng cách giữ tay liên tục với Hop, thay cho bước căn root tức thời cuối clip.

Các giá trị đã áp dụng trong Assets/Adventure/Prefabs/Exploration/ExplorationPlayer.prefab. Nếu scene có Override riêng, kiểm tra giá trị trên đối tượng đó.

## Giữ điểm bám

PlayerClimb lưu vị trí đỉnh gờ và pháp tuyến tường, không suy ra lại từ root đang chạy animation. W ưu tiên gờ cao hơn trước khi leo đứng lên. Hop Up/Down dùng MatchTarget XYZ rồi liên tục giữ tay tại đích trong giai đoạn hồi/blend; không dời root đột ngột sau khi animation kết thúc nữa.

Shimmy dùng điểm gờ đã lưu để dò cả mặt bên và mặt trên. Root và anchor di chuyển cùng nhau, dừng trước mép, vẫn cho quay lại. PlayerScript bỏ delta root motion riêng trong blend tree treo ổn định; root motion của Grab, Hop, Drop và leo mặt trên vẫn hoạt động.

LedgeToRoofClimb kiểm tra mặt đứng và khoảng trống CharacterController, bao gồm layer nền của PlayerScript. Các thông số: Maximum Roof Rise=1.5, Roof Landing Search Distance=0.6, Roof Landing Samples=4, Include Player Ground Layers=bật.

## Kiểm tra

Mở scene m ở Edit Mode → **Tools → Adventure → Climbing → Validate m Ledge Route**.

Công cụ đọc tuyến gờ trong scene đang mở, tạo bản sao collider/player trong scene kiểm tra tạm ở xa rồi đóng scene tạm. Nó không sửa/lưu scene m, không vào Play Mode và không truy cập campaign/save.

Kiểm tra gồm 108 trường hợp phân giải gờ với vị trí/chiều cao root và cờ roof cũ khác nhau; Grab bằng idle to hang, năm Hop Up và năm Hop Down bằng Animator/coroutine thật; Hop Up ở 30/60 FPS; shimmy hai hướng trên climb1–climb6, dừng/quay lại ở mép; tầm Hop Down 2.4 m; leo mặt trên và trả điều khiển. Đo độ hở chân/mũi chân, bàn tay/ngón tay, bước chỉnh root và chân trong hồi tư thế. Báo cáo ở Validation.txt, ảnh tư thế shimmy phải ở ShimmyRight.png.

Phép thử không gửi phím vào phiên chơi thật. Cảm giác điều khiển bằng bàn phím cần thử trong lần chơi tiếp theo.
