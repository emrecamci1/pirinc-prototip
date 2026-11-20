using UnityEditor;
using UnityEngine;

// Assets/Resources/Art altındaki PNG'ler Sprite olarak içe aktarılsın.
// Proje 3D şablonuyla açıldığı için varsayılan "Default" gelirdi ve sprite olarak kullanılamazdı.
public class ArtImporter : AssetPostprocessor
{
	private const string ArtPath = "Assets/Resources/Art/";

	private void OnPreprocessTexture()
	{
		if (!assetPath.StartsWith(ArtPath))
			return;

		var importer = (TextureImporter)assetImporter;

		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Single;
		importer.spritePixelsPerUnit = 100f;
		importer.alphaIsTransparency = true;
		importer.mipmapEnabled = false;
		importer.filterMode = FilterMode.Bilinear;
		importer.wrapMode = TextureWrapMode.Clamp;
		importer.maxTextureSize = 2048;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
	}

	[MenuItem("Pirinç/Sanatı Yeniden İçe Aktar")]
	private static void Reimport()
	{
		AssetDatabase.ImportAsset("Assets/Resources/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
		Debug.Log("Sanat dosyaları yeniden içe aktarıldı.");
	}
}
