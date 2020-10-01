using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Unity.Mathematics;

public class NetworkMan : MonoBehaviour
{
    [SerializeField]
    GameObject playerPrefab;

    private List<NewPlayer> unspawnedPlayers = new List<NewPlayer>();
    public List<Player> connectedPlayers = new List<Player>();

    public UdpClient udp;
    // Start is called before the first frame update
    void Start()
    {
        udp = new UdpClient();

        //EC2 Instance
        udp.Connect("100.25.196.191", 12345);

        //udp.Connect("localhost", 12345);

        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
      
        udp.Send(sendBytes, sendBytes.Length);

        udp.BeginReceive(new AsyncCallback(OnReceived), udp);

        InvokeRepeating("HeartBeat", 1, 1);
    }

    void OnDestroy(){
        udp.Dispose();
    }


    public enum commands{
        NEW_CLIENT,
        UPDATE,
        DROP_CLIENT
    };
    
    [Serializable]
    public class Message{
        public commands cmd;
    }
    
    [Serializable]
    public class Player{
        [Serializable]
        public struct receivedColor{
            public float R;
            public float G;
            public float B;
        }
        public string id;
        public receivedColor color;
        public GameObject cube;
    }

    [Serializable]
    public class NewPlayer{
        public Player player;   
    }

    [Serializable]
    public class GameState{
        public Player[] players;
    }

    public Message latestMessage;
    public GameState latestGameState;
    void OnReceived(IAsyncResult result){
        // this is what had been passed into BeginReceive as the second parameter:
        UdpClient socket = result.AsyncState as UdpClient;
        
        // points towards whoever had sent the message:
        IPEndPoint source = new IPEndPoint(0, 0);

        // get the actual message and fill out the source:
        byte[] message = socket.EndReceive(result, ref source);
        
        // do what you'd like with `message` here:
        string returnData = Encoding.ASCII.GetString(message);
        Debug.Log("Got this: " + returnData);
        
        latestMessage = JsonUtility.FromJson<Message>(returnData);
        try{
            switch(latestMessage.cmd){
                case commands.NEW_CLIENT:
                    NewPlayer newPlayer = JsonUtility.FromJson<NewPlayer>(returnData);
                    unspawnedPlayers.Add(newPlayer);
                    break;
                case commands.UPDATE:
                    latestGameState = JsonUtility.FromJson<GameState>(returnData);
                    break;
                case commands.DROP_CLIENT:
                    Player droppedPlayer = JsonUtility.FromJson<Player>(returnData);
                    DestroyPlayer(droppedPlayer.id);
                    break;
                default:
                    Debug.Log("Error");
                    break;
            }
        }
        catch (Exception e){
            Debug.Log(e.ToString());
        }
        
        // schedule the next receive operation once reading is done:
        socket.BeginReceive(new AsyncCallback(OnReceived), socket);
    }

    void SpawnPlayers() {
        foreach (NewPlayer newPlayer in unspawnedPlayers) {
            GameObject npCube = Instantiate(playerPrefab);
            npCube.GetComponent<PlayerScript>().NetworkID = newPlayer.player.id;

            newPlayer.player.cube = npCube;
            connectedPlayers.Add(newPlayer.player);
            unspawnedPlayers.Remove(newPlayer);
        }
    }

    void UpdatePlayers(){
        foreach (Player server in latestGameState.players) {
            foreach (Player client in connectedPlayers) {
                if (server.id == client.id) {
                    client.color = server.color;
                    client.cube.GetComponent<Renderer>().material.color = new Color(client.color.R, client.color.G, client.color.B);
                }
            }
        }
    }

    void DestroyPlayer(string id) {
        foreach (Player player in connectedPlayers) {
            if (player.id == id) {
                player.cube.GetComponent<PlayerScript>().Disconnected = true;
                connectedPlayers.Remove(player);
            }
        }
    }
    
    void HeartBeat(){
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat");
        udp.Send(sendBytes, sendBytes.Length);
    }

    void Update(){
        SpawnPlayers();
        UpdatePlayers();
    }
}
