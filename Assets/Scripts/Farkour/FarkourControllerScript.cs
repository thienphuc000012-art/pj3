using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class FarkourControllerScript : MonoBehaviour
{
    public EnviromentChecker enviromentChecker;

    public Animator animator;
    public PlayerScript playerScript;
    // Có thể xóa hoặc giữ lại biến này nếu không dùng đến
    // [SerializeField] NewFakourAction jumpDownParkourAction;

    [Header("Parkour Action Area")]
    public List<NewFakourAction> newFakourActions;

    void Update()
    {
        if (Input.GetButtonDown("Jump") && !playerScript.playerInAction && !playerScript.playerHanging)
        {
            bool isParkourPerformed = false;
            var hitData = enviromentChecker.checkObstacle();

            // 1. NẾU CÓ CHƯỚNG NGẠI VẬT -> THỬ PARKOUR LEO/VƯỢT
            if (hitData.hitFound)
            {
                foreach (var action in newFakourActions)
                {
                    if (action.checkIfAvailable(hitData, transform))
                    {
                        StartCoroutine(PerformParkourAction(action));
                        isParkourPerformed = true;
                        break;
                    }
                }
            }

            // 2. NẾU KHÔNG CÓ PARKOUR LEO VƯỢT -> NHẢY BÌNH THƯỜNG
            // (Đã bỏ phần kiểm tra playerOnLedge để nhảy xuống)
            if (!isParkourPerformed && playerScript.onSurface)
            {
                playerScript.PerformJump();
            }
        }
    }

    IEnumerator PerformParkourAction(NewFakourAction action)
    {
        playerScript.SetControl(false);
        CompareTargetParameter compareTargetParameter = null;
        if (action.AllowTargetMatching)
        {
            compareTargetParameter = new CompareTargetParameter()
            {
                position = action.ComparePosition,
                bodyPart = action.CompareBodyPart,
                positionWeight = action.ComparePositionWeight,
                startTime = action.CompareStartTime,
                endTime = action.CompareEndTime
            };
        }
        yield return playerScript.PerformAction(action.AnimationName, compareTargetParameter, action.RequiredRotation, action.LookAtObstacle, action.ParkourActionDelay);

        playerScript.SetControl(true);
    }

    void CompareTarget(NewFakourAction action)
    {
        animator.MatchTarget(action.ComparePosition, transform.rotation, action.CompareBodyPart, new MatchTargetWeightMask(action.ComparePositionWeight, 0), action.CompareStartTime, action.CompareEndTime);
    }
}