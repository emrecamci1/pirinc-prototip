using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupTriangleScene
{
	private const string ScenePath = "Assets/Scenes/Triangle.unity";

	[MenuItem("Pirinç/2 - Üçgen kule  (eski)", false, 2)]
	public static void Setup()
	{
		Directory.CreateDirectory("Assets/Scenes");

		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var camGo = new GameObject("Main Camera");
		camGo.tag = "MainCamera";
		camGo.AddComponent<Camera>();

		var go = new GameObject("TrianglePrototype");
		go.AddComponent<TrianglePrototype>();

		EditorSceneManager.SaveScene(scene, ScenePath);

		PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
		EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

		Debug.Log("Üçgen sahnesi kuruldu: " + ScenePath + "  —  Play'e bas.");
	}
}
