using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupStackScene
{
	private const string ScenePath = "Assets/Scenes/Stack.unity";

	[MenuItem("Pirinç/1 - YIĞIN oyunu  (aç)", false, 1)]
	public static void Open()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			return;

		if (File.Exists(ScenePath))
			EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
		else
			Build();

		MakeActive();
		Debug.Log("YIĞIN açıldı: " + ScenePath + "  —  Play'e bas.");
	}

	[MenuItem("Pirinç/8 - YIĞIN sahnesini sıfırdan kur", false, 80)]
	public static void Rebuild()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			return;

		Build();
		MakeActive();
		Debug.Log("YIĞIN sahnesi yeniden kuruldu: " + ScenePath);
	}

	private static void Build()
	{
		Directory.CreateDirectory("Assets/Scenes");

		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var camGo = new GameObject("Main Camera");
		camGo.tag = "MainCamera";
		camGo.AddComponent<Camera>();

		var go = new GameObject("StackPrototype");
		go.AddComponent<StackPrototype>();

		EditorSceneManager.SaveScene(scene, ScenePath);
	}

	private static void MakeActive()
	{
		PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
		EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
	}
}
