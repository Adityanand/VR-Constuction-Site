using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIWorker : MonoBehaviour
{
    public Transform[] Waypoints;
    public float Speed;
    public float TurningSpeed;
    public bool isWalking;
    private GameObject Player;
    // Start is called before the first frame update
    void Start()
    {
        Speed = 1f;
        TurningSpeed = 10f;
        Player = GameObject.FindGameObjectWithTag("Player");
        StartCoroutine(Movement());
    }
    IEnumerator MoveToward(Transform target,Transform[] Destination,float Speed)
    {
        foreach(var Dest in Destination)
        {
          while (Vector3.Distance(Dest.position, target.position) >= .05f)
          {
            if (Player!=null&&(Vector3.Distance(Player.transform.position,target.position)<=2.5f)!=true)
            {
              isWalking = true;
              target.position = Vector3.MoveTowards(target.position, Dest.position, Speed *Time.deltaTime);
            }
            else
            {
              isWalking = false;
              target.position = CurrentPosition.position;
            }
            yield return new WaitForSeconds(.01f);
          }
          isWalking = false;
          yield return new WaitForSeconds(4);
          transform.Rotate(0, 90, 0);
        }
    }
    IEnumerator Movement()
    {
        yield return MoveToward(transform, Waypoints, Speed);
        StartCoroutine(Movement());
    }
    public Transform CurrentPosition;
    private void Update()
    {
      CurrentPosition = this.gameObject.transform;  
    }
}
