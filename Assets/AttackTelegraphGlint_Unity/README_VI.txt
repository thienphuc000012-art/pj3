ATTACK TELEGRAPH GLINT — UNITY (VFX TIA SÁNG BÁO ĐÒN)

1. Kéo AttackTelegraphGlint.cs vào Assets/Scripts trong dự án Unity.
2. Tạo một GameObject rỗng tên AttackFlashPoint, đặt ở vị trí muốn tia sáng xuất hiện
   (có thể là con của enemy để đi theo animation).
3. Gắn component AttackTelegraphGlint vào AttackFlashPoint.
4. Nhấn Play; chọn dấu ba chấm ở component > Play Flash (in Play Mode),
   hoặc gọi GetComponent<AttackTelegraphGlint>().PlayFlash(); từ Animation Event
   đúng thời điểm enemy chuẩn bị ra đòn.
5. Gợi ý: Duration = 0.22, World Size = 1.1; đổi Glint Color sang trắng ngà
   hoặc vàng nhạt. Tia sáng tự xoay hướng mặt camera.

Ví dụ gọi từ một script khác:

public AttackTelegraphGlint attackGlint;
public void OnAttackTelegraph() => attackGlint.PlayFlash();

Không cần asset bên ngoài, DOTween hoặc shader phụ trợ. Nếu muốn sáng mạnh như game,
có thể nâng cấp shader sang additive + bật Bloom (URP/HDRP).

Đây là phiên bản tự tạo dựa trên kiểu Attack Telegraph/Star Glint, không phải
asset gốc được trích xuất từ Clair Obscur: Expedition 33.
