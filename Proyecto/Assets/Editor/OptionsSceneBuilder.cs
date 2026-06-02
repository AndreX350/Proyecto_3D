using UnityEditor;
using UnityEditor.SceneManagement;

public static class OptionsSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Options.unity";

    [MenuItem("DecorAR/Open Options Scene")]
    public static void OpenOptionsScene()
    {
        EditorSceneManager.OpenScene(ScenePath);
    }
}
