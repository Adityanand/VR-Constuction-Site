using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WalkingAnimation : MonoBehaviour
{
    public GameObject Player;
    public Transform IdlePos;
    public Transform[] Waypoints;
    public float MovementSpeed;
    
    // Start is called before the first frame update
    private void Start()
    {
        StartCoroutine(WalkingAnim());
    }
    IEnumerator MoveToward(Transform target,Transform[]Destination,float speed)
    {
        if (Player.GetComponent<PlayerController>().isWalking == true)
        {
            foreach (var dest in Destination)
            {
                while (Vector3.Distance(dest.position, target.position) >= .05f)
                {
                    target.position = Vector3.MoveTowards(target.position, dest.position, speed * Time.deltaTime);
               
                    //yield return new WaitForSeconds(.01f);
                    yield return null;
                }
            }
        }
        else
            target.position = Vector3.MoveTowards(target.position, IdlePos.position, speed * Time.deltaTime);
    }
   
    IEnumerator WalkingAnim()
    {
      yield return MoveToward(transform, Waypoints, MovementSpeed);
        StartCoroutine(WalkingAnim());
    }
}
