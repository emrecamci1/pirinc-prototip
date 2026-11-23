using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupTriangleScene
{
	private const string ScenePath = "Assets/Scenes/Triangle.unity";

	[MenuItem("Pirinç/2 - Üçgen kule  (eski, aç)", false, 2)]
	public static void Open()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			return;

		if (File.Exists(ScenePath))
			EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
		else
			Build();

		MakeActive();
		Debug.Log("Üçgen kule açıldı: " + ScenePath);
	}

	[MenuItem("Pirinç/9 - Üçgen sahnesini sıfırdan kur", false, 81)]
	public static void Rebuild()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			return;

		Build();
		MakeActive();
		Debug.Log("Üçgen sahnesi yeniden kuruldu: " + ScenePath);
	}

	private static void Build()
	{
		Directory.CreateDirectory("Assets/Scenes");

		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var camGo = new GameObject("Main Camera");
		camGo.tag = "MainCamera";
		camGo.AddComponent<Camera>();

		var go = new GameObject("TrianglePrototype");
		go.AddComponent<TrianglePrototype>();

		EditorSceneManager.SaveScene(scene, ScenePath);
	}

	private static void MakeActive()
	{
		PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
		EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
	}
}
