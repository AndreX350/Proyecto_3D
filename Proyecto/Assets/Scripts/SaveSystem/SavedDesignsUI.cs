using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SavedDesignsUI : MonoBehaviour
{
    [SerializeField]
    private Transform roomDemoListContent;

    [SerializeField]
    private Button roomDemoItemTemplateButton;

    [SerializeField]
    private TextMeshProUGUI roomDemoEmptyMessageText;

    [SerializeField]
    private Transform arListContent;

    [SerializeField]
    private Button arItemTemplateButton;

    [SerializeField]
    private TextMeshProUGUI arEmptyMessageText;

    private void OnEnable()
    {
        RefreshList();
    }

    public void RefreshList()
    {
        ResolveReferences();
        BuildList(
            DesignSaveManager.GetSavedDesignFiles("RoomDemo"),
            roomDemoListContent,
            roomDemoItemTemplateButton,
            roomDemoEmptyMessageText,
            true,
            false);

        BuildList(
            DesignSaveManager.GetSavedDesignFiles("ARScene"),
            arListContent,
            arItemTemplateButton,
            arEmptyMessageText,
            false,
            true);
    }

    private void ResolveReferences()
    {
        if (roomDemoListContent == null)
        {
            GameObject content = GameObject.Find("RoomDemoSavedListContent");
            if (content != null)
            {
                roomDemoListContent = content.transform;
            }
        }

        if (roomDemoItemTemplateButton == null)
        {
            GameObject template = GameObject.Find("RoomDemoSavedItemTemplate");
            if (template != null)
            {
                roomDemoItemTemplateButton = template.GetComponent<Button>();
            }
        }

        if (roomDemoEmptyMessageText == null)
        {
            GameObject emptyMessage = GameObject.Find("TxtNoRoomDemoSavedDesigns");
            if (emptyMessage != null)
            {
                roomDemoEmptyMessageText = emptyMessage.GetComponent<TextMeshProUGUI>();
            }
        }

        if (arListContent == null)
        {
            GameObject content = GameObject.Find("ARSavedListContent");
            if (content != null)
            {
                arListContent = content.transform;
            }
        }

        if (arItemTemplateButton == null)
        {
            GameObject template = GameObject.Find("ARSavedItemTemplate");
            if (template != null)
            {
                arItemTemplateButton = template.GetComponent<Button>();
            }
        }

        if (arEmptyMessageText == null)
        {
            GameObject emptyMessage = GameObject.Find("TxtNoARSavedDesigns");
            if (emptyMessage != null)
            {
                arEmptyMessageText = emptyMessage.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    private static void BuildList(
        string[] savedFiles,
        Transform listContent,
        Button itemTemplateButton,
        TextMeshProUGUI emptyMessageText,
        bool canLoadRoomDemo,
        bool canLoadARScene)
    {
        ClearGeneratedItems(listContent, itemTemplateButton);

        bool hasSavedFiles = savedFiles.Length > 0;

        if (emptyMessageText != null)
        {
            emptyMessageText.gameObject.SetActive(!hasSavedFiles);
        }

        if (itemTemplateButton == null || listContent == null)
        {
            return;
        }

        itemTemplateButton.gameObject.SetActive(false);

        foreach (string filePath in savedFiles)
        {
            Button itemButton = Instantiate(itemTemplateButton, listContent);
            itemButton.name = "SavedDesign_" + Path.GetFileNameWithoutExtension(filePath);
            itemButton.gameObject.SetActive(true);
            itemButton.interactable = canLoadRoomDemo || canLoadARScene;
            itemButton.onClick.RemoveAllListeners();

            if (canLoadRoomDemo)
            {
                itemButton.onClick.AddListener(() => DesignSaveManager.LoadRoomDemoDesign(filePath));
            }
            else if (canLoadARScene)
            {
                itemButton.onClick.AddListener(() => DesignSaveManager.LoadARDesign(filePath));
            }

            TextMeshProUGUI label = itemButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = FormatFileLabel(filePath);
            }
        }
    }

    private static void ClearGeneratedItems(Transform listContent, Button itemTemplateButton)
    {
        if (listContent == null)
        {
            return;
        }

        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Transform child = listContent.GetChild(i);
            if (itemTemplateButton != null && child == itemTemplateButton.transform)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private static string FormatFileLabel(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string date = File.GetLastWriteTime(filePath).ToString("dd/MM/yyyy HH:mm");

        return fileName + "\n" + date;
    }
}
