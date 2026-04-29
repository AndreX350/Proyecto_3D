using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManage : MonoBehaviour
{
    private void Start()
    {
        if (SceneManager.GetActiveScene().name == "RoomSelector")
        {
            EnsureARStartButton();
        }
    }

    public void OpenRoomSelector()
    {
        Debug.Log("Abrir selector de habitacion");
        SceneManager.LoadScene("RoomSelector");
    }

    public void OpenRoomDemo()
    {
        Debug.Log("Abrir sala demo");
        SceneManager.LoadScene("RoomDemo");
    }

    public void StartAR()
    {
        Debug.Log("Iniciar AR");
        SceneManager.LoadScene("ARScene");
    }

    public void OpenSaved()
    {
        Debug.Log("Abrir guardados");
    }

    public void OpenOptions()
    {
        Debug.Log("Abrir opciones");
    }

    private void EnsureARStartButton()
    {
        GameObject salaDemoButton = GameObject.Find("BtnStart");
        if (salaDemoButton == null || GameObject.Find("BtnARStart") != null)
        {
            return;
        }

        GameObject arButton = Instantiate(salaDemoButton, salaDemoButton.transform.parent);
        arButton.name = "BtnARStart";
        arButton.transform.SetSiblingIndex(salaDemoButton.transform.GetSiblingIndex());

        Button button = arButton.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(StartAR);
        }

        TextMeshProUGUI label = arButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = "Iniciar";
        }
    }
}
