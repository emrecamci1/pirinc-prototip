using UnityEditor;
using UnityEngine;

public static class SetupIOS
{
	[MenuItem("Pirinç/iOS Ayarlarını Yap")]
	public static void Configure()
	{
		if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
		{
			Debug.Log("iOS platformuna geçiliyor — ilk seferde birkaç dakika sürebilir.");
			EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
		}

		PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.emrecamci.pirincprototip");
		PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
		PlayerSettings.iOS.targetOSVersionString = "13.0";
		PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;

		// Imzayi Xcode kendi halletsin: ucretsiz Apple ID ile calisan tek yol bu.
		PlayerSettings.iOS.appleEnableAutomaticSigning = true;

		PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
		PlayerSettings.allowedAutorotateToPortrait = true;
		PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
		PlayerSettings.allowedAutorotateToLandscapeLeft = false;
		PlayerSettings.allowedAutorotateToLandscapeRight = false;

		PlayerSettings.statusBarHidden = true;

		Debug.Log("iOS ayarlari yapildi. File > Build Settings > Build ile Xcode projesi uret, sonra Xcode'dan telefona at.");
	}
}
