using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupPrototypeScene
{
	private const string ScenePath = "Assets/Scenes/Prototype.unity";

	[MenuItem("Pirinç/Sahneyi Kur ve Aç")]
	public static void Setup()
	{
		Directory.CreateDirectory("Assets/Scenes");

		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var camGo = new GameObject("Main Camera");
		camGo.tag = "MainCamera";
		var cam = camGo.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = 10f;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
		camGo.transform.position = new Vector3(0f, 0f, -10f);

		var go = new GameObject("Prototype");
		go.AddComponent<RicePrototype>();

		EditorSceneManager.SaveScene(scene, ScenePath);

		PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
		PlayerSettings.allowedAutorotateToPortrait = true;
		PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
		PlayerSettings.allowedAutorotateToLandscapeLeft = false;
		PlayerSettings.allowedAutorotateToLandscapeRight = false;
		PlayerSettings.companyName = "Emre Camci";
		PlayerSettings.productName = "Pirinc Prototip";

		EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

		Debug.Log("Sahne kuruldu: " + ScenePath + "  —  Play'e bas.");
	}
}
