using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoseBalanceAnim : MonoBehaviour
{
    public GameObject Player;
    public Transform[] LBWaypoints;
    public float MovementSpeed;
    public Transform IdlePos;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Falling());
    }
    IEnumerator MoveToward(Transform target, Transform[] Destination, float speed)
    {
        if (Player.GetComponent<PlayerController>().loseBalance == true)
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
    IEnumerator LoseBalances()
    {
        Player.GetComponent<PlayerController>().isWalking = false;
        yield return MoveToward(transform, LBWaypoints, MovementSpeed);
        yield return new WaitForSeconds(2);
        StartCoroutine(LoseBalances());
    }
    IEnumerator Falling()
    {
        StopAllCoroutines();
        StartCoroutine(LoseBalances());
        yield return null;
    }
}
