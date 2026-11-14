# Ngày đêm và đồng hồ khám phá

Chỉ scene `m` thay đổi ánh sáng. Cả `m` và `mapgame` hiển thị cùng giờ, lấy từ dữ liệu campaign hiện tại. Giờ được lưu cùng các bản lưu sẵn có, giữ nguyên khi chuyển map và được khôi phục khi tải bản lưu. Bản lưu cũ bắt đầu ở giờ mặc định. Không thêm một hệ thống lưu game mới.

## Chỉnh trong Unity

- Tốc độ ngày và giờ bắt đầu: chọn `Assets/Resources/Adventure/WorldTimeSettings.asset`. `Day Length Minutes` là số phút thật cho một ngày đầy đủ (mặc định 24), `Starting Hour` mặc định 8. `Advance Time` cho phép dừng đồng hồ. Giờ bắt đầu áp dụng cho New Game hoặc bản lưu chưa có giờ; bản lưu đã có giờ sẽ dùng giờ đã lưu.
- Ánh sáng: mở scene `m`, chọn `Day Night System`. Chỉnh `Day Sun Intensity`, `Night Moon Intensity`, các màu `Night Ambient`, `Player Light Intensity`, `Player Light Range`, và `Player Light Offset`.
- Đèn player mặc định dịu (`Intensity` 0.8, `Range` 7 m), màu vàng nhạt; `Player Light Offset` (0, 2.8, -1.4) tính theo hướng của player, đặt nguồn sáng cao và lệch ra sau để tránh chói tóc/đầu. Giảm `Player Light Intensity` nếu muốn dịu hơn.
- Mây, sao, trăng: `Day Night System > Sky Visuals`. Các object là instance của đúng prefab được yêu cầu; các material chỉnh riêng nằm ở `Assets/Adventure/Generated/DayNight`.
- Đồng hồ: mở `Assets/Resources/Adventure/UI/AdventureCanvas.prefab`, sửa `HUD > World Time > Time`. Panel neo góc phải trên; không nhận raycast và không chặn click.
- Xem ánh sáng ở Edit Mode: chỉnh `Preview Hour` trên `Day Night System`, mở menu component chọn `Preview Time (m only)`. Chọn `Restore Editor Lighting` trước khi lưu scene. Preview không chạy campaign hay ghi save.

## Hành vi

- Bình minh khoảng 05:00–07:30; ban ngày 07:30–17:00; hoàng hôn 17:00–19:30.
- Mặt trời chuyển góc, màu và cường độ theo giờ. Ambient, sương và sky chuyển mượt; đêm có ánh trăng xanh dịu và mức sáng nền để vẫn nhìn được đường.
- Mặt trời và mặt trăng xoay cùng một quỹ đạo, cách nhau 180 độ. Khoảng 06:00 mặt trời mọc và mặt trăng lặn; 18:00 đổi lại. Moon prefab luôn giữ nguyên instance, di chuyển xuống dưới chân trời vào ban ngày, không xuất hiện tại một vị trí cố định khi trời tối. `Sun Azimuth` đổi hướng quỹ đạo, `Celestial Distance` chỉnh khoảng cách, `Moon Scale` chỉnh kích thước mặt trăng.
- Sao mờ dần khi trời sáng. Đèn quanh player chỉ sáng khi trời tối, không đổ bóng. Moon Fill cũng không đổ bóng và xoay theo mặt trăng.
- Đồng hồ tạm dừng lúc mở menu, hội thoại, chuyển cảnh, stream map đang chặn di chuyển, hoặc combat. `mapgame` vẫn giữ ánh sáng đã có.
- Khi một scene khác active, rig `m` tắt các hiệu ứng và đèn phụ; không ghi RenderSettings vào scene combat hoặc map khác.

## Kiểm tra

Menu `Tools > Adventure > Day Night > Validate Scene m and Clock` chạy kiểm tra trong Edit Mode; không bấm Play và không ghi dữ liệu campaign thật. Kết quả ở `Tools/DayNight/Validation.txt` cùng ảnh preview theo giờ. Setup có bản sao cả scene trên đĩa lẫn scene đang mở trước khi thay đổi.

Kiểm tra thủ công thêm: mở `m`, đi ngoài trời qua bình minh/hoàng hôn/đêm; đi vào hầm để kiểm tra đèn player; mở menu để kiểm tra đồng hồ dừng; chuyển `mapgame` để kiểm tra giờ giữ nguyên và ánh sáng map không đổi; vào combat rồi quay lại; lưu thủ công và tải lại để kiểm tra giờ.
