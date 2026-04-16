using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Prefab del mueble")]
    public GameObject mueblePrefab;

    [Header("Posición de aparición")]
    public Transform puntoSpawn;

    private GameObject objetoActual;

    // 🔹 Agregar mueble
    public void AgregarMueble()
    {
        if (mueblePrefab != null && puntoSpawn != null)
        {
            objetoActual = Instantiate(mueblePrefab, puntoSpawn.position, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("Falta asignar prefab o punto de spawn");
        }
    }

    // 🔹 Rotar mueble
    public void RotarMueble()
    {
        if (objetoActual != null)
        {
            objetoActual.transform.Rotate(0, 45, 0);
        }
    }

    // 🔹 Eliminar mueble
    public void EliminarMueble()
    {
        if (objetoActual != null)
        {
            Destroy(objetoActual);
        }
    }

    // 🔹 Cambiar color (simple)
    public void CambiarColor()
    {
        if (objetoActual != null)
        {
            Renderer rend = objetoActual.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = Random.ColorHSV();
            }
        }
    }
}