using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking.Types;

public class PlayerScript : MonoBehaviour
{
    [SerializeField]
    private string networkId;
    private bool disconnected = false;

    public bool Disconnected { get { return disconnected; } set { disconnected = value; } }

    public string NetworkID { get { return networkId; } set { networkId = value; } }

    void Update() {
        if (disconnected) {
            Destroy(gameObject);
        }
    }
}
