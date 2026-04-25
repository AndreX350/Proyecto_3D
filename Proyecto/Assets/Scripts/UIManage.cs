using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManage : MonoBehaviour
{
    public void StartAR()
    {
        Debug.Log("Iniciar AR");
        // SceneManager.LoadScene("ARScene");
    }

    public void OpenSaved()
    {
        Debug.Log("Abrir guardados");
    }

    public void OpenOptions()
    {
        Debug.Log("Abrir opciones");
    }
}
