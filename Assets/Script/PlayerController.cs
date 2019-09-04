using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public GameObject Player;
    public float Speed;
    public float turnSpeed;
    public bool isWalking=false;
    public bool loseBalance=false;
    // Start is called before the first frame update
    void Start()
    {
        Speed = 1f;
        turnSpeed = 50f;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKey(KeyCode.UpArrow))
        {
            isWalking = true;
            transform.Translate(Vector3.forward * Speed * Time.deltaTime);
        }
        else if(Input.GetKey(KeyCode.DownArrow))
        {
            isWalking = true;
            transform.Translate(-Vector3.forward * Speed * Time.deltaTime);
        }
        else
        {
            isWalking = false;
        }
        if(Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Rotate(Vector3.down * turnSpeed * Time.deltaTime);
        }
        if(Input.GetKey(KeyCode.RightArrow))
        {
            transform.Rotate(Vector3.up *turnSpeed* Time.deltaTime);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (Player != null && other.gameObject.tag == "DangerZone")
        {
            Player.GetComponent<Rigidbody>().useGravity = false;
        }
        if (Player != null && other.gameObject.tag=="Pit Hole")
        {
            Speed = 0;
            loseBalance = true;
            StartCoroutine(LoseControl());
        }
        if(Player != null && other.gameObject.tag=="Kill Object")
        {
            Destroy(Player);
        }
    }
    IEnumerator LoseControl()
    {
        yield return new WaitForSeconds(5);
        GetComponent<Rigidbody>().useGravity = true;
        GetComponent<Rigidbody>().AddForce(Vector3.down * 50);
        loseBalance = false;
    }
}
