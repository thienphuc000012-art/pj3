using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class FarkourControllerScript : MonoBehaviour
{
    PlayerClimb ledgeClimb;
    private void Awake() { ledgeClimb = GetComponent<PlayerClimb>(); }
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

        if (ledgeClimb != null && ledgeClimb.ConsumedClimbInputThisFrame)
            return;


        if (!Input.GetButtonDown("Jump"))
            return;

        TryJumpOrParkour();
    }

    void TryJumpOrParkour()
    {

        if (playerScript == null)
            return;


        if (playerScript.IsUsingLadder || playerScript.LadderInputConsumedThisFrame)
            return;

        if (playerScript.playerInAction)
            return;


        if (playerScript.playerHanging)
            return;

        // Space still performs the ordinary jump taught in the first lesson.
        // Only the obstacle/climb branch waits for the Parkour lesson.
        if (!MExplorationTutorial.Allows(ExplorationTutorialAction.Parkour, playerScript))
        {
            if (playerScript.onSurface) playerScript.PerformJump();
            return;
        }

        bool isParkourPerformed = false;

        if (enviromentChecker != null && enviromentChecker.TryCheckFence(out var fenceHit))
        {
            foreach (var action in newFakourActions)
            {
                if (action != null && action.name == "VaultJump" && action.checkIfAvailable(fenceHit, transform, true))
                {
                    StartCoroutine(PerformParkourAction(action));
                    return;
                }
            }
        }


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
