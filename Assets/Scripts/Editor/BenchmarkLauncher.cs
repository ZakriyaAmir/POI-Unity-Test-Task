using UnityEditor;
using UnityEditor.SceneManagement;

namespace Poi.EditorTools
{
    // Entry point for the command line benchmark:
    // Unity -batchmode -projectPath . -executeMethod Poi.EditorTools.BenchmarkLauncher.Run -poiBenchmark
    // Opens the demo scene and enters play mode; BenchmarkRunner sees -poiBenchmark, measures and exits the editor.
    public static class BenchmarkLauncher
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(PoiProjectSetup.ScenePath);
            EditorApplication.EnterPlaymode();
        }
    }
}
