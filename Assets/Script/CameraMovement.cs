using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CameraMovement : MonoBehaviour
{
    private Transform Lookat;
    public GameObject Player;
    private Vector3 StartOffset;
    public Transform cameraPosition;
    public Transform CameraPositionEndScreen;
    public Canvas Canvas;
    // Start is called before the first frame update
    void Start()
    {
        Lookat = Player.transform;
        StartOffset = transform.position - Lookat.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (Player == null)
        {
            transform.position = CameraPositionEndScreen.position;
            Canvas.enabled=true;
        }
        else if (Player.GetComponent<PlayerController>().loseBalance == true)
        {
            transform.position = Vector3.MoveTowards(transform.position,cameraPosition.position,5*Time.deltaTime);
        }
        else if(Player.GetComponent<PlayerController>().loseBalance==false)
        transform.position = Lookat.position+StartOffset;
        
    }
    public void TryAgain()
    {
        SceneManager.LoadScene("SampleScene");
    }
    public void Quit()
    {
        Application.Quit();
    }
}
