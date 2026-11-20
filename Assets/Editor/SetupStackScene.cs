using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupStackScene
{
	private const string ScenePath = "Assets/Scenes/Stack.unity";

	[MenuItem("Pirinç/1 - YIĞIN  (yeni board oyunu)", false, 1)]
	public static void Setup()
	{
		Directory.CreateDirectory("Assets/Scenes");

		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var camGo = new GameObject("Main Camera");
		camGo.tag = "MainCamera";
		camGo.AddComponent<Camera>();

		var go = new GameObject("StackPrototype");
		go.AddComponent<StackPrototype>();

		EditorSceneManager.SaveScene(scene, ScenePath);

		PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
		EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

		Debug.Log("Yığın sahnesi kuruldu: " + ScenePath + "  —  Play'e bas.");
	}
}
