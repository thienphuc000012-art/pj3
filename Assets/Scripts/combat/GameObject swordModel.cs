using UnityEngine;

public class WeaponDrawController : MonoBehaviour
{
    [Header("References")]
    public GameObject hipSword;   // Kéo kiếm ở hông vào đây
    public GameObject handSword;  // Kéo kiếm ở tay vào đây

    void Start()
    {
        // Mặc định ban đầu: hiện kiếm ở hông, ẩn kiếm ở tay (trạng thái bình thường)
        ShowHipSword();
    }

    // Hàm gọi khi bắt đầu rút kiếm hoặc ở trạng thái bình thường
    public void ShowHipSword()
    {
        if (hipSword != null) hipSword.SetActive(true);
        if (handSword != null) handSword.SetActive(false);
    }

    // Hàm gọi đúng khoảnh khắc rút kiếm ra khỏi bao
    public void DrawSwordToHand()
    {
        if (hipSword != null) hipSword.SetActive(false); // Ẩn kiếm ở hông đi
        if (handSword != null) handSword.SetActive(true);  // Hiện kiếm lên tay
    }
}