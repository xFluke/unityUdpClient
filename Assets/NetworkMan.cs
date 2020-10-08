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

    public string myAddress;

    public Dictionary<string, GameObject> currentPlayers; // A list of currently connected players
    public List<string> newPlayers, disconnectedPlayers;
    public GameState latestGameState;
    public Message latestMessage;
    public ListOfPlayers initialSetOfPlayers;

    public UdpClient udp;
    // Start is called before the first frame update
    void Start()
    {
        // Intialize all variables
        newPlayers = new List<string>();
        disconnectedPlayers = new List<string>();
        currentPlayers = new Dictionary<string, GameObject>();
        initialSetOfPlayers = new ListOfPlayers();

        // Connect to client

        udp = new UdpClient();

        //EC2 Instance
        udp.Connect("100.25.196.191", 12345);

        // Local
        //udp.Connect("localhost", 12345);
        Byte[] sendBytes = Encoding.ASCII.GetBytes("connect");
        udp.Send(sendBytes, sendBytes.Length);
        udp.BeginReceive(new AsyncCallback(OnReceived), udp);

        InvokeRepeating("HeartBeat", 1, 1/30f);
    }

    void OnDestroy(){
        udp.Dispose();
    }


    public enum commands{
        NEW_CLIENT,
        UPDATE,
        DROP_CLIENT,
        APPROVED_CONNECTION,
        LIST_OF_PLAYERS
    };
    
    // Structure to replicate message dictionary on server
    [Serializable]
    public class Message{
        public commands cmd;
    }

    [Serializable]
    public struct Position
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class Player{
        public string id;
        public Position position;
    }

    [Serializable]
    public class ListOfPlayers{
        public Player[] players;
        
        public ListOfPlayers() {
            players = new Player[0];
        }
    }

    [Serializable]
    public class ListOfDisconnectedPlayers
    {
        public string[] disconnectedPlayers;
    }

    // Structure to replicate game state dictionary on server
    [Serializable]
    public class GameState{
        public int pktID;
        public Player[] players;
    }


    void OnReceived(IAsyncResult result){
        // this is what had been passed into BeginReceive as the second parameter:
        UdpClient socket = result.AsyncState as UdpClient;
        
        // points towards whoever had sent the message:
        IPEndPoint source = new IPEndPoint(0, 0);

        // get the actual message and fill out the source:
        byte[] message = socket.EndReceive(result, ref source);
        
        // do what you'd like with `message` here:
        string returnData = Encoding.ASCII.GetString(message);
        //Debug.Log("Got this: " + returnData);
        
        latestMessage = JsonUtility.FromJson<Message>(returnData);
        try{
            switch(latestMessage.cmd){
                case commands.NEW_CLIENT:
                    ListOfPlayers latestPlayer = JsonUtility.FromJson<ListOfPlayers>(returnData);
                    foreach (Player player in latestPlayer.players) {
                        newPlayers.Add(player.id);
                    }
                    break;
                case commands.UPDATE:
                    latestGameState = JsonUtility.FromJson<GameState>(returnData);
                    break;
                case commands.DROP_CLIENT:
                    Debug.Log(returnData);
                    ListOfDisconnectedPlayers latestDroppedPlayer = JsonUtility.FromJson<ListOfDisconnectedPlayers>(returnData);
                    foreach (string player in latestDroppedPlayer.disconnectedPlayers) {
                        disconnectedPlayers.Add(player);
                    }
                    break;
                case commands.APPROVED_CONNECTION:
                    ListOfPlayers myPlayer = JsonUtility.FromJson<ListOfPlayers>(returnData);
                    Debug.Log(returnData);
                    foreach (Player player in myPlayer.players) {
                        newPlayers.Add(player.id);
                        myAddress = player.id;
                    }
                    break;
                case commands.LIST_OF_PLAYERS:
                    initialSetOfPlayers = JsonUtility.FromJson<ListOfPlayers>(returnData);
                    break;
                default:
                    Debug.Log("Error: " + returnData);
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
        if (newPlayers.Count > 0) {
            foreach (string playerID in newPlayers) {
                currentPlayers.Add(playerID, Instantiate(playerPrefab, new Vector3(0, 0, 0), quaternion.identity));
                currentPlayers[playerID].name = playerID;
            }
            newPlayers.Clear();
        }

       if (initialSetOfPlayers.players.Length > 0) {
            Debug.Log(initialSetOfPlayers);
            foreach (Player player in initialSetOfPlayers.players) {
                if (player.id == myAddress) {
                    continue;
                }

                currentPlayers.Add(player.id, Instantiate(playerPrefab, new Vector3(0, 0, 0), quaternion.identity));
                currentPlayers[player.id].name = player.id;
            }
            initialSetOfPlayers.players = new Player[0];
        }
    }

    void UpdatePlayers(){
       

        if (latestGameState.players.Length > 0) {
            foreach (Player player in latestGameState.players) {
                if (myAddress != player.id) {
                    currentPlayers[player.id].transform.position = new Vector3(player.position.x, player.position.y, player.position.z);
                }
            }
            latestGameState.players = new Player[0];
        }
    }

    void DestroyPlayers() {
        if (disconnectedPlayers.Count > 0) {

            foreach (string playerID in disconnectedPlayers) {
                Destroy(currentPlayers[playerID].gameObject);
                currentPlayers.Remove(playerID);
            }
        }

        disconnectedPlayers.Clear();
    }
    
    void HeartBeat(){
        Byte[] sendBytes = Encoding.ASCII.GetBytes("heartbeat");
        udp.Send(sendBytes, sendBytes.Length);

        sendBytes = Encoding.ASCII.GetBytes(JsonUtility.ToJson(currentPlayers[myAddress].transform.position));
        udp.Send(sendBytes, sendBytes.Length);
    }

    void Update(){
        SpawnPlayers();
        UpdatePlayers();
        DestroyPlayers();
    }
}
