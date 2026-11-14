# Description kỹ năng tự động

Chọn một asset **ActionData** của skill. Các thông số combat vẫn được chỉnh trong Inspector như trước. Cuối Inspector có **Description — tự tạo từ chỉ số** và **Preview Language** để xem nội dung Việt/Anh. Không cần nhập Description cho Attack/Skill; văn bản cũ được giữ trong asset để bảo toàn dữ liệu. Item vẫn dùng Description nhập tay.

Nội dung hiển thị trong combat, menu Skills và ô đổi skill:

- Nguyên tố và phạm vi mục tiêu: bản thân, một mục tiêu hoặc toàn bộ phe.
- Công thức sát thương **mỗi đòn** từ `Damage Multiplier` và `Power`. Đây là sát thương cơ bản; ATK, phòng thủ, chí mạng, khắc hệ và trạng thái hiện tại quyết định số HP mất thực tế. Không đoán số hit từ tên animation.
- Điều kiện `Caster Has Burn` hoặc `Target Has Poison`: cả hệ số khi có và không có trạng thái, cùng tùy chọn bỏ qua giảm ATK của Burn.
- Hồi máu, buff chỉ số/lá chắn và số lượt. Crit/CritDamage dùng điểm phần trăm; các chỉ số còn lại dùng lượng cộng thẳng như code combat.
- Debuff: tỷ lệ áp dụng, số lượt, sát thương Burn, mức giảm ATK, tỷ lệ bỏ lượt, hoặc % HP tối đa của Poison. Giữ đúng quy tắc một debuff, không cộng dồn/làm mới, lấy hiệu ứng đầu tiên áp dụng thành công.
- Xóa buff/debuff, hút máu theo HP mất thực tế, triệu hồi và chi phí khi đơn vị triệu hồi chết, điều kiện Ultimate và Stain.

Khi sửa chỉ số trong Play Mode, phần mô tả đang mở tự cập nhật; thay đổi trong Play Mode vẫn tuân theo quy tắc lưu Inspector của Unity. Đổi ngôn ngữ sẽ tạo lại mô tả từ dữ liệu. Nội dung dài có thể cuộn trong khung Description; bố cục các nút được giữ nguyên.

`Status Effect Info Panel` dùng chung phần mô tả tác dụng debuff, giúp nội dung trước khi dùng skill và trạng thái đang có thống nhất với nhau.

Kiểm tra ở Edit Mode: **Tools > Adventure > UI > Validate Generated Skill Descriptions**. Kết quả ở `Tools/SkillDescriptions/Validation.txt`, ví dụ Việt/Anh của toàn bộ action ở `ConfiguredSkills.txt`. Việc kiểm tra không vào combat hay ghi save thật.
