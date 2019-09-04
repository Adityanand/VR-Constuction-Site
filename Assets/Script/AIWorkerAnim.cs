using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIWorkerAnim : MonoBehaviour
{
    public GameObject AIWorker;
    public Transform[] Waypoints;
    public Transform IdlePos;
    public float speed;
    
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(MovementAnim());
    }
    IEnumerator MoveToward(Transform target,Transform[] Destination,float speed)
    {
            foreach (var dest in Destination)
            {
                if (AIWorker.GetComponent<AIWorker>().isWalking == true)
                {
                    while (Vector3.Distance(dest.position, target.position) >= 0.05f)
                    {
                        target.position = Vector3.MoveTowards(target.position, dest.position, speed * Time.deltaTime);
                       // yield return new WaitForSeconds(.01f);
                       yield return null;
                    }
                }
                else
                {
                    target.position = Vector3.MoveTowards(target.position, IdlePos.position, speed * Time.deltaTime);
                }
            }
    }
    IEnumerator MovementAnim()
    {
        yield return MoveToward(transform, Waypoints, speed);
        StartCoroutine(MovementAnim());
    }
}
