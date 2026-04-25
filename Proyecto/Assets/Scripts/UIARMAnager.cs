using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIARMAnager : MonoBehaviour
{
    public GameObject panelColors;
    public GameObject panelFurniture;

    public void OpenColors()
    {
        panelColors.SetActive(true);
        panelFurniture.SetActive(false);
    }

    public void OpenFurniture()
    {
        panelFurniture.SetActive(true);
        panelColors.SetActive(false);
    }

    public void ClearScene()
    {
        Debug.Log("Limpiar escena");
    }

    public void SaveDesign()
    {
        Debug.Log("Guardar diseño");
    }
}