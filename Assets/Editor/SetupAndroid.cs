using UnityEditor;
using UnityEngine;

public static class SetupAndroid
{
	[MenuItem("Pirinç/Android Ayarlarını Yap")]
	public static void Configure()
	{
		if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
		{
			Debug.Log("Android platformuna geçiliyor — ilk seferde birkaç dakika sürebilir.");
			EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
		}

		PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
		PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
		PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
		PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.emrecamci.pirincprototip");

		PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
		PlayerSettings.allowedAutorotateToPortrait = true;
		PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
		PlayerSettings.allowedAutorotateToLandscapeLeft = false;
		PlayerSettings.allowedAutorotateToLandscapeRight = false;

		// Prototip: ekran hep açık kalsın, his testi sırasında kilitlenmesin.
		PlayerSettings.Android.startInFullscreen = true;

		EditorUserBuildSettings.development = true;
		EditorUserBuildSettings.allowDebugging = false;
		EditorUserBuildSettings.androidBuildType = AndroidBuildType.Development;
		EditorUserBuildSettings.buildAppBundle = false;

		Debug.Log("Android ayarları yapıldı. File > Build Settings > Build And Run ile telefona at.");
	}
}
