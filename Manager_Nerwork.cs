using SimproNET.Unity;
using UnityEngine;

public class Manager_Network : MonoBehaviour
{
    private NetworkComponent_UNITY _netManager;
    private string nameSpace = "Network";

    public GameObject cube;
    public Material red;
    public Material green;

    private void Awake()
    {
        if (_netManager == null)
            _netManager = gameObject.AddComponent<NetworkComponent_UNITY>();

        _netManager.ReciveTcpMessage += _netManager_ReciveTcpMessage;
    }

    /// <summary>
    /// Funcion encargada de procesa los sms
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void _netManager_ReciveTcpMessage(object sender, TcpMessage e)
    {
        switch (e.Type)
        {
            case MessageTypeTCP.Play:
                Debug.Log($"[{nameSpace}] {e.Payload}");

                cube.GetComponent<MeshRenderer>().material.color = red.color;

                break;
            case MessageTypeTCP.Pause:
                break;

        }
    }
}
