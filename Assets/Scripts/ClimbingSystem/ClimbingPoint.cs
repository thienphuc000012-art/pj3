using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;


public class ClimbingPoint : MonoBehaviour
{
    public bool MountPoint;
    public List<Neighbour> neighbours;

     void Awake()
    {
        var twoWayClimbNeighbour = neighbours.Where(n => n.isPointToWay);
        foreach(var neighbour in twoWayClimbNeighbour)
        {
            neighbour.climbingPoint?.CreatPointConnection(this, -neighbour.pointDirection, neighbour.connectionType, neighbour.isPointToWay);
        }
    }

    public void CreatPointConnection(ClimbingPoint climbingPoint, Vector2 pointDirection, ConnectionType connectionType, bool isPointToWay)
    {
        var neighbour = new Neighbour()
        {
            climbingPoint = climbingPoint,
            pointDirection = pointDirection,
            connectionType = connectionType,
            isPointToWay = isPointToWay
        };

        neighbours.Add(neighbour);
    }

    public Neighbour GetNeighbour(Vector2 climbDirection)
    {
        Neighbour neighbour = null;
        if (climbDirection.y !=  0)
        {
            neighbour = neighbours.FirstOrDefault(n => n.pointDirection.y == climbDirection.y);
        }
        if (neighbour == null &&  climbDirection.x != 0)
        {
            neighbour = neighbours.FirstOrDefault(n => n.pointDirection.x == climbDirection.x);
        }
        return neighbour;
    }

    private void OnDrawGizmos()
    {
        Debug.DrawRay(transform.position, transform.forward, Color.red);
        foreach(var neighbour in neighbours)
        {
            if(neighbour.climbingPoint != null)
            {
                Debug.DrawLine(transform.position, neighbour.climbingPoint.transform.position, (neighbour.isPointToWay) ? Color.green : Color.black);
            }
        }
    }
}
[System.Serializable]

public class Neighbour
{
    public ClimbingPoint climbingPoint;
    public Vector2 pointDirection;
    public ConnectionType connectionType;
    public bool isPointToWay = true;
}

public enum ConnectionType { Jump, Move}
