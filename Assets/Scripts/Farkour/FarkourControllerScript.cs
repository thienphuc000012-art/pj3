using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class FarkourControllerScript : MonoBehaviour
{
    [Header("References")]

    public EnviromentChecker enviromentChecker;

    public Animator animator;

    public PlayerScript playerScript;


    [Header("Parkour Action Area")]

    public List<NewFakourAction> newFakourActions;


    // ============================================================
    // UPDATE
    // ============================================================

    void Update()
    {
        if (CampaignSession.InputBlocked)
            return;


        if (!Input.GetButtonDown("Jump"))
            return;


        if (playerScript == null)
            return;


        if (playerScript.playerInAction)
            return;


        if (playerScript.playerHanging)
            return;


        bool isParkourPerformed = false;


        // ========================================================
        // CHECK OBSTACLE
        // ========================================================

        ObstacleInfo hitData =
            enviromentChecker.checkObstacle();


        // ========================================================
        // CHỈ PARKOUR KHI:
        //
        // - Có obstacle phía trước
        // - Có mặt trên
        // - Collider phía trước = Layer Obstacle
        // - Collider mặt trên = Layer Obstacle
        // ========================================================

        if (hitData.hitFound &&
            hitData.heightHitFound)
        {
            int obstacleLayer =
                LayerMask.NameToLayer("Obstacle");


            bool validFrontLayer =
                hitData.hitInfo.collider != null &&
                hitData.hitInfo.collider.gameObject.layer ==
                obstacleLayer;


            bool validTopLayer =
                hitData.heightHitInfo.collider != null &&
                hitData.heightHitInfo.collider.gameObject.layer ==
                obstacleLayer;


            if (validFrontLayer &&
                validTopLayer)
            {
                foreach (
                    NewFakourAction action
                    in newFakourActions)
                {
                    if (action == null)
                        continue;


                    if (action.checkIfAvailable(
                        hitData,
                        transform))
                    {
                        StartCoroutine(
                            PerformParkourAction(action)
                        );


                        isParkourPerformed = true;

                        break;
                    }
                }
            }
            else
            {
                Debug.LogWarning(
                    "PARKOUR BLOCKED: Object phía trước " +
                    "không phải Layer Obstacle."
                );
            }
        }


        // ========================================================
        // KHÔNG PARKOUR -> NHẢY BÌNH THƯỜNG
        // ========================================================

        if (!isParkourPerformed &&
            playerScript.onSurface)
        {
            playerScript.PerformJump();
        }
    }


    // ============================================================
    // PERFORM PARKOUR
    // ============================================================

    IEnumerator PerformParkourAction(
        NewFakourAction action)
    {
        if (action == null)
            yield break;


        playerScript.SetControl(false);


        CompareTargetParameter
            compareTargetParameter = null;


        if (action.AllowTargetMatching)
        {
            compareTargetParameter =
                new CompareTargetParameter()
                {
                    position =
                        action.ComparePosition,

                    bodyPart =
                        action.CompareBodyPart,

                    positionWeight =
                        action.ComparePositionWeight,

                    startTime =
                        action.CompareStartTime,

                    endTime =
                        action.CompareEndTime
                };
        }


        yield return playerScript.PerformAction(
            action.AnimationName,
            compareTargetParameter,
            action.RequiredRotation,
            action.LookAtObstacle,
            action.ParkourActionDelay
        );


        playerScript.SetControl(true);
    }


    // ============================================================
    // COMPARE TARGET
    // ============================================================

    void CompareTarget(
        NewFakourAction action)
    {
        if (action == null)
            return;


        animator.MatchTarget(
            action.ComparePosition,
            transform.rotation,
            action.CompareBodyPart,
            new MatchTargetWeightMask(
                action.ComparePositionWeight,
                0
            ),
            action.CompareStartTime,
            action.CompareEndTime
        );
    }
}