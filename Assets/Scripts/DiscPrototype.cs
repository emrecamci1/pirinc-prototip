using System.Collections.Generic;
using UnityEngine;

// Pul ayıklama prototipi — Emre'nin Maya modelleriyle.
// Bant üzerinde renkli pullar bize doğru kayıyor. Önde renkli kutular var;
// pulu, taşıdığı renklerden birinin kutusuna sürüklüyorsun.
// Hiçbir kutu rengini taşımayan pulu geçirmen gerekiyor; kutuya atarsan ceza.
public class DiscPrototype : MonoBehaviour
{
	private const string ModelPath = "Models/Pul/";

	// Modellerin kendi ölçüsü (Maya'dan): çap 0.063, kalınlık 0.010.
	private const float SourceDiscWidth = 0.063f;

	public enum Hue { Mavi, Kirmizi, Sari, Pembe, Yesil }

	private static readonly string[] HueNames = { "mavi", "kırmızı", "sarı", "pembe", "yeşil" };

	// Her dosya bir renk kombinasyonu. Kod renkleri dosya adından değil
	// bu tablodan okuyor; model eklenince tabloya bir satır yetiyor.
	private struct DiscDef
	{
		public string File;
		public Hue[] Hues;

		public DiscDef(string file, params Hue[] hues) { File = file; Hues = hues; }
	}

	private static readonly DiscDef[] DiscDefs =
	{
		new DiscDef("mavi", Hue.Mavi),
		new DiscDef("kirmizi", Hue.Kirmizi),
		new DiscDef("sari", Hue.Sari),
		new DiscDef("yesil", Hue.Yesil),
		new DiscDef("2_renk_sari_kirmizi", Hue.Sari, Hue.Kirmizi),
		new DiscDef("2_renk_sari_mavi", Hue.Sari, Hue.Mavi),
		new DiscDef("2_renk_sari_pembe", Hue.Sari, Hue.Pembe),
		new DiscDef("2_renk_sari_yesil", Hue.Sari, Hue.Yesil),
		new DiscDef("2_renk_kirmizi_yesil", Hue.Kirmizi, Hue.Yesil),
		new DiscDef("2_renk_mavi_yesil", Hue.Mavi, Hue.Yesil),
		new DiscDef("2_renk_pembe_yesil", Hue.Pembe, Hue.Yesil),
		// 3_renk_sari_mavi_kirmizi şimdilik devre dışı: kutusu yok.
	};

	// Kutular da pullar gibi tek ya da çift renkli. Eşleşme TAM olmak zorunda:
	// mavi-yeşil pul sadece mavi-yeşil kutuya gidiyor, mavi kutuya değil.
	private struct BoxDef
	{
		public string File;
		public Hue[] Hues;

		public BoxDef(string file, params Hue[] hues) { File = file; Hues = hues; }
	}

	private static readonly BoxDef[] BoxDefs =
	{
		new BoxDef("mavi_kutu", Hue.Mavi),
		new BoxDef("sari_kutu", Hue.Sari),
		new BoxDef("yesil_kutu", Hue.Yesil),
		new BoxDef("kirmizi_kutu", Hue.Kirmizi),
		new BoxDef("pembe_kutu", Hue.Pembe),
		new BoxDef("sari_kirmizi_kutu", Hue.Sari, Hue.Kirmizi),
		new BoxDef("sari_mavi_kutu", Hue.Sari, Hue.Mavi),
		new BoxDef("sari_pembe_kutu", Hue.Sari, Hue.Pembe),
		new BoxDef("sari_yesil_kutu", Hue.Sari, Hue.Yesil),
		new BoxDef("kirmizi_yesil_kutu", Hue.Kirmizi, Hue.Yesil),
		new BoxDef("mavi_yesil_kutu", Hue.Mavi, Hue.Yesil),
		new BoxDef("pembe_yesil_kutu", Hue.Pembe, Hue.Yesil),
	};

	// Renk kümeleri aynı mı? Sıra önemsiz.
	private static bool SameSet(Hue[] a, Hue[] b)
	{
		if (a.Length != b.Length)
			return false;

		foreach (var h in a)
		{
			bool found = false;
			foreach (var o in b)
				if (o == h)
					found = true;

			if (!found)
				return false;
		}

		return true;
	}

	private static string Label(Hue[] hues)
	{
		string s = "";
		for (int i = 0; i < hues.Length; i++)
			s += (i > 0 ? "-" : "") + HueNames[(int)hues[i]];

		return s;
	}

	// --- Hata ayıklama ---
	[System.NonSerialized] public bool ShowRails = true;   // fizik duvarlarını kırmızı göster

	// Duvarları içeri (+) / dışarı (−) almak için ince ayar. Kaydediliyor.
	[System.NonSerialized] public float RailLeftNudge = 0f;
	[System.NonSerialized] public float RailRightNudge = 0f;

	// --- Ölçek ---
	[System.NonSerialized] public float DiscDiameter = 0.26f;   // oyundaki pul çapı

	// --- Akış ---
	[System.NonSerialized] public float BeltSpeed = 0.55f;
	[System.NonSerialized] public float BeltGrip = 3.0f;    // bant hızına ne kadar çabuk dönsün
	[System.NonSerialized] public float SideDamp = 1.8f;    // yanal savrulma sönümü
	[System.NonSerialized] public int TargetDiscs = 70;
	[System.NonSerialized] public float TargetRatio = 0.55f;
	[System.NonSerialized] public float LaneLength = 9f;

	// --- Kutular ---
	// Konumu sabit sayı değil: kameradan ekranın alt kısmına ışın atılarak
	// bulunuyor, böylece kamera ayarı ne olursa olsun kadraj içinde kalıyorlar.
	[System.NonSerialized] public float BoxScreenY = 0.15f;
	[System.NonSerialized] public float BoxScale = 1.35f;

	private float BoxZ = 0.45f;

	// --- Kamera ---
	[System.NonSerialized] public float CamHeight = 1.15f;
	[System.NonSerialized] public float CamBack = 1.0f;
	[System.NonSerialized] public float CamLookZ = 3.2f;
	[System.NonSerialized] public float FieldOfView = 42f;

	// --- Tutma ---
	[System.NonSerialized] public float HoldLift = 0.22f;
	[System.NonSerialized] public float HoldFollow = 26f;

	// --- Tur ---
	[System.NonSerialized] public int Lives = 3;
	[System.NonSerialized] public int Level = 1;

	private class Disc
	{
		public Transform Tr;
		public Rigidbody Rb;
		public Collider Col;
		public Hue[] Hues;
		public bool Held;
		public float RestY;
	}

	private class BoxSlot
	{
		public Hue[] Hues;
		public Vector3 Pos;
		public float HalfSize;
	}

	private readonly List<Disc> discs = new List<Disc>();
	private readonly List<BoxSlot> slots = new List<BoxSlot>();

	private Camera cam;
	private Transform root, boxRoot, beltRoot;

	private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
	private PhysicMaterial slick;

	private float discScale = 1f;
	private float laneHalfWidth = 0.8f;
	private float laneLeftX = -0.8f;
	private float laneRightX = 0.8f;
	private float beltTopY;

	private Disc held;
	private Vector3 holdTarget;

	private int score, lives, missed;
	private bool gameOver;
	private float spawnAccum;
	private bool showTuning;

	private void Awake()
	{
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 120;
		Screen.sleepTimeout = SleepTimeout.NeverSleep;

		Physics.defaultSolverIterations = 6;
		Time.fixedDeltaTime = 1f / 60f;

		LoadPrefabs();
		SetupCamera();
		SetupLight();

		root = new GameObject("Scene").transform;
		beltRoot = new GameObject("Belt").transform;
		beltRoot.SetParent(root, false);
		boxRoot = new GameObject("Boxes").transform;
		boxRoot.SetParent(root, false);

		slick = new PhysicMaterial("Disc")
		{
			dynamicFriction = 0.1f,
			staticFriction = 0.1f,
			bounciness = 0f,
			frictionCombine = PhysicMaterialCombine.Minimum,
			bounceCombine = PhysicMaterialCombine.Minimum
		};

		discScale = DiscDiameter / SourceDiscWidth;

		BuildBelt();
		StartLevel(Level);
	}

	private void LoadPrefabs()
	{
		foreach (var d in DiscDefs)
			Cache(d.File);

		foreach (var b in BoxDefs)
			Cache(b.File);

		Cache("palet");
		Cache("sol_bariyer");
		Cache("sag_bariyer");
	}

	private void Cache(string file)
	{
		var go = Resources.Load<GameObject>(ModelPath + file);

		if (go == null)
			Debug.LogError($"Model bulunamadı: {ModelPath}{file}");
		else
			prefabs[file] = go;
	}

	// Prefab'ı geçici olarak sahneye koyup gerçek dünya sınırını ölçüyor,
	// sonra siliyor. Mesh'in yerel sınırı kök dönüşümlerini hesaba katmıyor.
	private Bounds ProbeWorldBounds(GameObject prefab)
	{
		var temp = Instantiate(prefab);
		temp.transform.position = Vector3.zero;
		temp.transform.rotation = Quaternion.identity;
		temp.transform.localScale = Vector3.one * discScale;

		var rends = temp.GetComponentsInChildren<Renderer>();
		Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(Vector3.zero, Vector3.one);

		for (int i = 1; i < rends.Length; i++)
			b.Encapsulate(rends[i].bounds);

		DestroyImmediate(temp);
		return b;
	}

	private static Bounds MeshBounds(GameObject prefab)
	{
		var filters = prefab.GetComponentsInChildren<MeshFilter>();
		if (filters.Length == 0)
			return new Bounds(Vector3.zero, Vector3.one);

		var b = filters[0].sharedMesh.bounds;
		for (int i = 1; i < filters.Length; i++)
			b.Encapsulate(filters[i].sharedMesh.bounds);

		return b;
	}

	private void SetupCamera()
	{
		cam = Camera.main;
		if (cam == null)
		{
			var go = new GameObject("Main Camera");
			go.tag = "MainCamera";
			cam = go.AddComponent<Camera>();
		}

		cam.orthographic = false;
		cam.nearClipPlane = 0.03f;
		cam.farClipPlane = 80f;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0.62f, 0.62f, 0.64f);

		PlaceCamera();
	}

	private void PlaceCamera()
	{
		cam.fieldOfView = FieldOfView;
		cam.transform.position = new Vector3(0f, CamHeight, -CamBack);
		cam.transform.LookAt(new Vector3(0f, 0f, CamLookZ));
	}

	private void SetupLight()
	{
		var go = new GameObject("Sun");
		var l = go.AddComponent<Light>();
		l.type = LightType.Directional;
		l.intensity = 1.0f;
		l.shadows = LightShadows.None;
		go.transform.rotation = Quaternion.Euler(58f, -18f, 0f);

		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
		RenderSettings.ambientSkyColor = new Color(0.72f, 0.72f, 0.75f);
		RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.58f);
		RenderSettings.ambientGroundColor = new Color(0.35f, 0.35f, 0.38f);
	}

	// Bant üç ayrı modelden kuruluyor: palet, sol bariyer, sağ bariyer.
	// Hepsi birlikte arka arkaya diziliyor. Oyun alanının sınırı bariyerlerin
	// İÇ kenarları; çarpışma duvarları tam oraya konuyor.
	private void BuildBelt()
	{
		if (!prefabs.TryGetValue("palet", out var palet))
			return;

		prefabs.TryGetValue("sol_bariyer", out var solB);
		prefabs.TryGetValue("sag_bariyer", out var sagB);

		// Ölçüler mesh'in yerel sınırından değil, sahneye KONMUŞ halinin dünya
		// sınırından okunuyor. FBX kökünde dönüş/kaydırma varsa yerel sınır
		// yanıltıyor ve çizilen bariyerle fizik duvarı birbirini tutmuyordu.
		Bounds pw = ProbeWorldBounds(palet);
		Bounds lw = solB != null ? ProbeWorldBounds(solB) : pw;
		Bounds rw = sagB != null ? ProbeWorldBounds(sagB) : pw;

		float tileLen = pw.size.z;
		beltTopY = pw.max.y;

		// Dosya adına güvenmiyoruz: Unity'nin FBX içe aktarıcısı X eksenini
		// Maya/Blender'ın tersine çevirdiği için "sol_bariyer" sahnede sağda
		// duruyor. Hangisinin nerede olduğunu ölçüden okuyoruz.
		if (solB != null && sagB != null)
		{
			bool solIsLeft = lw.center.x < rw.center.x;

			Bounds leftB = solIsLeft ? lw : rw;
			Bounds rightB = solIsLeft ? rw : lw;

			laneLeftX = leftB.max.x;    // soldaki bariyerin İÇ kenarı
			laneRightX = rightB.min.x;  // sağdakinin iç kenarı
		}
		else
		{
			laneLeftX = pw.min.x;
			laneRightX = pw.max.x;
		}

		// İnce ayar payı: ölçüm doğru olsa da duvarı biraz içeri/dışarı almak isteyebilirsin.
		laneLeftX += RailLeftNudge;
		laneRightX -= RailRightNudge;

		laneHalfWidth = (laneRightX - laneLeftX) * 0.5f;

		if (laneHalfWidth <= 0f)
			Debug.LogError($"[PUL] Şerit genişliği negatif ({laneRightX - laneLeftX:0.###}). "
				+ "Bariyer ölçümü ters okunuyor.");

		float barrierTopY = Mathf.Max(Mathf.Max(lw.max.y, rw.max.y), beltTopY);
		float beltMinZ = pw.min.z;

		float zStart = -CamBack - 1.5f;
		float zEnd = LaneLength + 3f;
		int tiles = Mathf.CeilToInt((zEnd - zStart) / Mathf.Max(tileLen, 0.01f));

		for (int i = 0; i < tiles; i++)
		{
			// Mesh merkezden başlamıyor; dizilim için kendi başlangıcını hesaba katıyoruz.
			float z = zStart + tileLen * i - beltMinZ;

			SpawnBeltPiece(palet, z);
			SpawnBeltPiece(solB, z);
			SpawnBeltPiece(sagB, z);
		}

		float span = zEnd - zStart + 4f;
		float midZ = (zStart + zEnd) * 0.5f;

		// Zemin çarpışması yok: pulların Y ekseni dondurulduğu için zaten
		// bandın üstünde kalıyorlar. Zemin kutusu olsaydı boşuna itişirlerdi.
		float wallH = Mathf.Max((barrierTopY - beltTopY) * 3f, DiscDiameter);

		AddRail(laneLeftX - 0.03f, wallH, span, midZ);
		AddRail(laneRightX + 0.03f, wallH, span, midZ);

		// Ölçülen her şeyi günlüğe yaz: sorunu sayılardan teşhis edebilmek için.
		Bounds dw = prefabs.TryGetValue("mavi", out var mavi) ? ProbeWorldBounds(mavi) : new Bounds();

		Debug.Log($"[PUL] discScale={discScale:0.###}  beltTopY={beltTopY:0.####}  tileLen={tileLen:0.###}\n"
			+ $"palet dunya: x[{pw.min.x:0.###},{pw.max.x:0.###}] y[{pw.min.y:0.###},{pw.max.y:0.###}] z[{pw.min.z:0.###},{pw.max.z:0.###}]\n"
			+ $"sol bariyer: x[{lw.min.x:0.###},{lw.max.x:0.###}] y[{lw.min.y:0.###},{lw.max.y:0.###}]\n"
			+ $"sag bariyer: x[{rw.min.x:0.###},{rw.max.x:0.###}] y[{rw.min.y:0.###},{rw.max.y:0.###}]\n"
			+ $"serit: laneLeftX={laneLeftX:0.###} laneRightX={laneRightX:0.###} genislik={laneRightX - laneLeftX:0.###}\n"
			+ $"duvar yuksekligi={wallH:0.###}  span={span:0.###}  midZ={midZ:0.###}\n"
			+ $"pul dunya: x[{dw.min.x:0.###},{dw.max.x:0.###}] y[{dw.min.y:0.###},{dw.max.y:0.###}] cap={dw.size.x:0.###}");
	}

	private void RebuildBelt()
	{
		for (int i = beltRoot.childCount - 1; i >= 0; i--)
			Destroy(beltRoot.GetChild(i).gameObject);

		BuildBelt();
		StartLevel(Level);
	}

	private void SpawnBeltPiece(GameObject prefab, float z)
	{
		if (prefab == null)
			return;

		var go = Instantiate(prefab, beltRoot);
		go.transform.localScale = Vector3.one * discScale;
		go.transform.position = new Vector3(0f, 0f, z);

		foreach (var r in go.GetComponentsInChildren<Renderer>())
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		// Modelin kendi çarpışması atılıyor; sınırları düz kutular temsil ediyor.
		foreach (var c in go.GetComponentsInChildren<Collider>())
			Destroy(c);
	}

	private void AddRail(float x, float height, float span, float midZ)
	{
		var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
		go.name = "Rail";
		go.transform.SetParent(beltRoot, false);
		go.transform.position = new Vector3(x, beltTopY + height * 0.5f, midZ);
		go.transform.localScale = new Vector3(0.06f, height, span);

		var r = go.GetComponent<Renderer>();
		r.enabled = ShowRails;
		r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		if (ShowRails)
			r.material = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.2f, 0.2f, 1f) };

		go.GetComponent<Collider>().sharedMaterial = slick;
	}

	// Seviye = masadaki kutular. Önce tek renkler, sonra çift renkler giriyor.
	private static string[] LevelBoxes(int level)
	{
		switch (level)
		{
			case 1: return new[] { "mavi_kutu" };
			case 2: return new[] { "mavi_kutu", "kirmizi_kutu" };
			case 3: return new[] { "mavi_kutu", "sari_kutu", "yesil_kutu" };
			case 4: return new[] { "mavi_kutu", "sari_kutu", "sari_yesil_kutu" };
			case 5: return new[] { "sari_kutu", "sari_mavi_kutu", "sari_kirmizi_kutu" };
			default: return new[] { "sari_mavi_kutu", "kirmizi_yesil_kutu", "pembe_yesil_kutu" };
		}
	}

	private void StartLevel(int level)
	{
		Level = Mathf.Clamp(level, 1, 6);

		foreach (var d in discs)
			if (d.Tr != null)
				Destroy(d.Tr.gameObject);

		discs.Clear();
		slots.Clear();
		held = null;

		for (int i = boxRoot.childCount - 1; i >= 0; i--)
			Destroy(boxRoot.GetChild(i).gameObject);

		score = 0;
		missed = 0;
		lives = Lives;
		gameOver = false;
		spawnAccum = 0f;

		BoxZ = ViewportToBelt(0.5f, BoxScreenY).z;
		BuildBoxes(LevelBoxes(Level));

		for (int i = 0; i < TargetDiscs; i++)
			SpawnDisc(Random.Range(BoxZ + 0.8f, LaneLength));
	}

	private void BuildBoxes(string[] files)
	{
		float boxW = 0f;
		var instances = new List<GameObject>();

		foreach (var file in files)
		{
			Hue[] hues = null;
			foreach (var bd in BoxDefs)
				if (bd.File == file)
					hues = bd.Hues;

			if (hues == null || !prefabs.TryGetValue(file, out var prefab))
				continue;

			var go = Instantiate(prefab, boxRoot);
			float k = discScale * BoxScale;
			go.transform.localScale = Vector3.one * k;

			foreach (var r in go.GetComponentsInChildren<Renderer>())
				r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			foreach (var c in go.GetComponentsInChildren<Collider>())
				Destroy(c);

			Bounds b = MeshBounds(prefab);
			boxW = Mathf.Max(boxW, b.size.x * k);

			instances.Add(go);
			slots.Add(new BoxSlot { Hues = hues, HalfSize = b.size.x * k * 0.5f });
		}

		// Kutular önde yan yana, ortalanmış.
		float gap = boxW * 0.12f;
		float step = boxW + gap;
		float startX = -(instances.Count - 1) * step * 0.5f;

		for (int i = 0; i < instances.Count; i++)
		{
			var pos = new Vector3(startX + i * step, beltTopY, BoxZ);
			instances[i].transform.position = pos;
			slots[i].Pos = pos;
		}
	}

	private DiscDef RollDisc()
	{
		// Gelenlerin bir kısmı toplanabilir olsun, gerisi çeldirici.
		bool wantTarget = Random.value < TargetRatio;

		var pool = new List<DiscDef>();

		foreach (var d in DiscDefs)
		{
			bool collectable = false;
			foreach (var s in slots)
				if (SameSet(s.Hues, d.Hues))
					collectable = true;

			if (collectable == wantTarget)
				pool.Add(d);
		}

		if (pool.Count == 0)
			return DiscDefs[Random.Range(0, DiscDefs.Length)];

		return pool[Random.Range(0, pool.Count)];
	}

	private void SpawnDisc(float z)
	{
		var def = RollDisc();

		if (!prefabs.TryGetValue(def.File, out var prefab))
			return;

		var go = Instantiate(prefab, root);
		go.transform.localScale = Vector3.one * discScale;

		// Pul tam bandın üstüne oturuyor: modelin alt kenarı bant yüzeyinde.
		// Önceden kürenin yarıçapı kadar gömülü doğuyorlardı ve PhysX onları fırlatıyordu.
		Bounds mb = MeshBounds(prefab);
		float restY = beltTopY + mb.size.y * discScale * 0.5f;

		float margin = DiscDiameter * 0.5f + 0.02f;
		go.transform.position = new Vector3(
			Random.Range(laneLeftX + margin, laneRightX - margin),
			restY,
			z);

		go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

		foreach (var r in go.GetComponentsInChildren<Renderer>())
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		foreach (var c in go.GetComponentsInChildren<Collider>())
			Destroy(c);

		Bounds b = mb;

		// Çarpışma küre, ama Y donduruldu: yani tepeden bakılan daire-daire fiziği.
		// Kapsül kullanınca Unity onu zaten küreye indirgiyordu (yükseklik < 2*yarıçap)
		// ve yüksekliğe oturmayan toplar bandın içine gömülüp fırlıyordu.
		var col = go.AddComponent<SphereCollider>();
		col.radius = Mathf.Max(b.size.x, b.size.z) * 0.5f;
		col.center = b.center;
		col.sharedMaterial = slick;

		if (ShowRails && discs.Count == 0)
			Debug.Log($"[PUL] collider yaricap(yerel)={col.radius:0.####} merkez={col.center} "
				+ $"-> dunya yaricap={col.radius * discScale:0.####} (beklenen {DiscDiameter * 0.5f:0.####})");

		var rb = go.AddComponent<Rigidbody>();
		rb.mass = 0.05f;
		rb.useGravity = false;
		rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
		rb.interpolation = RigidbodyInterpolation.None;
		rb.constraints = RigidbodyConstraints.FreezePositionY
			| RigidbodyConstraints.FreezeRotationX
			| RigidbodyConstraints.FreezeRotationZ;
		rb.sleepThreshold = 0f;

		discs.Add(new Disc { Tr = go.transform, Rb = rb, Hues = def.Hues, Col = col, RestY = restY });
	}

	// Tam eşleşme: pulun renk kümesi kutununkiyle birebir aynı olmalı.
	// Sarı-mavi pul, sarı kutuya da mavi kutuya da gitmiyor; sarı-mavi kutuya gidiyor.
	private bool Accepts(BoxSlot slot, Disc d) => SameSet(slot.Hues, d.Hues);

	private bool IsCollectable(Disc d)
	{
		foreach (var s in slots)
			if (Accepts(s, d))
				return true;

		return false;
	}

	private void Update()
	{
		if (gameOver)
			return;

		ReadInput();
		Recycle();
		Feed();
	}

	private void ReadInput()
	{
		if (Input.GetMouseButtonDown(0))
			TryGrab();

		if (Input.GetMouseButton(0) && held != null && PointerOnBelt(out Vector3 p))
			holdTarget = p + Vector3.up * HoldLift;

		if (Input.GetMouseButtonUp(0) && held != null)
			Release();
	}

	private void TryGrab()
	{
		Ray ray = cam.ScreenPointToRay(Input.mousePosition);

		if (!Physics.Raycast(ray, out RaycastHit hit, 80f))
			return;

		foreach (var d in discs)
		{
			if (d.Tr == null || !hit.transform.IsChildOf(d.Tr) && hit.transform != d.Tr)
				continue;

			held = d;
			d.Held = true;
			d.Rb.isKinematic = true;

			// Tutarken çarpışma kapanıyor: kalabalığın içinde sürüklenen kinematik
			// bir gövde değdiği her şeyi savuruyordu.
			if (d.Col != null)
				d.Col.enabled = false;

			holdTarget = d.Tr.position + Vector3.up * HoldLift;
			return;
		}
	}

	private Vector3 ViewportToBelt(float vx, float vy)
	{
		var plane = new Plane(Vector3.up, new Vector3(0f, beltTopY, 0f));
		Ray ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));

		return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : new Vector3(0f, beltTopY, 0.5f);
	}

	private bool PointerOnBelt(out Vector3 point)
	{
		var plane = new Plane(Vector3.up, new Vector3(0f, beltTopY, 0f));
		Ray ray = cam.ScreenPointToRay(Input.mousePosition);

		if (plane.Raycast(ray, out float enter))
		{
			Vector3 p = ray.GetPoint(enter);

			// Parmakla da bariyerlerin dışına taşınamıyor. Kutular bandın önünde
			// olduğu için sadece bant boyunca sınırlanıyor.
			if (p.z > BoxZ)
			{
				float margin = DiscDiameter * 0.5f;
				p.x = Mathf.Clamp(p.x, laneLeftX + margin, laneRightX - margin);
			}

			point = p;
			return true;
		}

		point = default;
		return false;
	}

	private void Release()
	{
		BoxSlot target = null;

		foreach (var s in slots)
		{
			if (Mathf.Abs(held.Tr.position.x - s.Pos.x) < s.HalfSize
				&& Mathf.Abs(held.Tr.position.z - s.Pos.z) < s.HalfSize)
			{
				target = s;
				break;
			}
		}

		if (target == null)
		{
			// Bandın üstüne geri bırakılıyor: yüksekliği ve hızı sıfırlanıyor,
			// yoksa kaldırdığın yükseklikten düşüp sıçrıyor.
			Vector3 p = held.Tr.position;
			p.y = held.RestY;
			held.Tr.position = p;

			held.Held = false;
			held.Rb.isKinematic = false;
			held.Rb.velocity = new Vector3(0f, 0f, -BeltSpeed);

			if (held.Col != null)
				held.Col.enabled = true;

			held = null;
			return;
		}

		if (Accepts(target, held))
			score++;
		else
			lives--;

		Kill(held);
		held = null;

		if (lives <= 0)
			gameOver = true;
	}

	private void FixedUpdate()
	{
		if (gameOver)
			return;

		float dt = Time.fixedDeltaTime;

		foreach (var d in discs)
		{
			if (d.Held || d.Rb == null)
				continue;

			Vector3 v = d.Rb.velocity;

			// Hızı sert atamak yerine bant hızına doğru çekiyoruz. Sert atama
			// çarpışmaların Z'de çözülmesini engelliyor, pullar birbirine gömülüp sıçrıyordu.
			v.z = Mathf.MoveTowards(v.z, -BeltSpeed, BeltGrip * dt);

			// Yanal savrulma sönümleniyor, yoksa bir çarpışma sonsuza kadar kayıyor.
			v.x = Mathf.MoveTowards(v.x, 0f, SideDamp * dt);
			v.y = 0f;

			d.Rb.velocity = v;
		}

		if (held != null)
			held.Rb.MovePosition(Vector3.Lerp(held.Rb.position, holdTarget,
				1f - Mathf.Exp(-HoldFollow * dt)));
	}

	private void Recycle()
	{
		for (int i = discs.Count - 1; i >= 0; i--)
		{
			var d = discs[i];

			if (d.Tr == null)
			{
				discs.RemoveAt(i);
				continue;
			}

			if (d.Held || d.Tr.position.z >= -CamBack - 0.4f)
				continue;

			if (IsCollectable(d))
			{
				missed++;
				lives--;

				if (lives <= 0)
					gameOver = true;
			}

			Kill(d);
		}
	}

	private void Feed()
	{
		if (discs.Count >= TargetDiscs)
			return;

		spawnAccum += (TargetDiscs - discs.Count) * Time.deltaTime * 2.5f;

		while (spawnAccum >= 1f && discs.Count < TargetDiscs)
		{
			spawnAccum -= 1f;
			SpawnDisc(LaneLength + Random.Range(0f, 1.5f));
		}
	}

	private void Kill(Disc d)
	{
		if (d.Tr != null)
			Destroy(d.Tr.gameObject);

		discs.Remove(d);
	}

	private void OnGUI()
	{
		float s = Mathf.Max(Screen.width, Screen.height) / 900f;
		GUI.skin.label.fontSize = Mathf.RoundToInt(19 * s);
		GUI.skin.button.fontSize = Mathf.RoundToInt(19 * s);

		float pad = 14f * s;
		float line = 34f * s;

		string want = "";
		for (int i = 0; i < slots.Count; i++)
			want += (i > 0 ? "  |  " : "") + Label(slots[i].Hues);

		GUI.Label(new Rect(pad, pad, 900f * s, line), $"Seviye {Level}   Kutular: {want}");
		GUI.Label(new Rect(pad, pad + line, 900f * s, line), $"Puan {score}   Can {lives}   Kaçan {missed}");
		GUI.Label(new Rect(pad, pad + line * 2f, 900f * s, line), $"FPS {Mathf.RoundToInt(1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f))}");

		if (gameOver)
		{
			float w = 520f * s;
			var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.35f, w, line * 4f);
			GUI.Box(r, "");
			GUI.Label(new Rect(r.x + pad, r.y + pad, w, line), "Bitti");
			GUI.Label(new Rect(r.x + pad, r.y + pad + line, w, line), $"Puan: {score}");

			if (GUI.Button(new Rect(r.x + pad, r.y + line * 2.4f, w - pad * 2f, 46f * s), "Tekrar"))
				StartLevel(Level);

			return;
		}

		if (GUI.Button(new Rect(Screen.width - 150f * s - pad, pad, 150f * s, 44f * s), showTuning ? "Kapat" : "Ayarlar"))
			showTuning = !showTuning;

		if (!showTuning)
			return;

		int big = GUI.skin.label.fontSize;
		GUI.skin.label.fontSize = Mathf.RoundToInt(13 * s);

		float pw = 330f * s;
		float px = Screen.width - pw - 10f * s;
		float py = 62f * s;
		float ln = 42f * s;

		GUI.Box(new Rect(px - 8f * s, py - 8f * s, pw + 16f * s, ln * 10f + 52f * s), "");

		BeltSpeed = Slider(px, ref py, pw, ln, s, "Bant hızı", BeltSpeed, 0.1f, 2.5f);
		TargetDiscs = Mathf.RoundToInt(Slider(px, ref py, pw, ln, s, "Pul yoğunluğu", TargetDiscs, 10f, 180f));
		TargetRatio = Slider(px, ref py, pw, ln, s, "Hedef oranı", TargetRatio, 0.1f, 0.9f);
		HoldLift = Slider(px, ref py, pw, ln, s, "Tutma yüksekliği", HoldLift, 0.05f, 0.6f);

		float ch = Slider(px, ref py, pw, ln, s, "Kamera yüksekliği", CamHeight, 0.3f, 3f);
		float cb = Slider(px, ref py, pw, ln, s, "Kamera geriliği", CamBack, 0.2f, 3f);
		float cl = Slider(px, ref py, pw, ln, s, "Bakış mesafesi", CamLookZ, 0.5f, 9f);
		float by = Slider(px, ref py, pw, ln, s, "Kutu ekran yüksekliği", BoxScreenY, 0.03f, 0.5f);

		if (!Mathf.Approximately(ch, CamHeight) || !Mathf.Approximately(cb, CamBack)
			|| !Mathf.Approximately(cl, CamLookZ) || !Mathf.Approximately(by, BoxScreenY))
		{
			CamHeight = ch;
			CamBack = cb;
			CamLookZ = cl;
			BoxScreenY = by;
			PlaceCamera();
			StartLevel(Level);
		}

		float nl = Slider(px, ref py, pw, ln, s, "Sol duvar içeri", RailLeftNudge, -0.4f, 0.4f);
		float nr = Slider(px, ref py, pw, ln, s, "Sağ duvar içeri", RailRightNudge, -0.4f, 0.4f);

		if (!Mathf.Approximately(nl, RailLeftNudge) || !Mathf.Approximately(nr, RailRightNudge))
		{
			RailLeftNudge = nl;
			RailRightNudge = nr;
			RebuildBelt();
		}

		float bw = (pw - 5f * 5f * s) / 6f;

		for (int i = 0; i < 6; i++)
			if (GUI.Button(new Rect(px + i * (bw + 5f * s), py + 2f * s, bw, 34f * s), $"{i + 1}"))
				StartLevel(i + 1);

		GUI.skin.label.fontSize = big;
	}

	private float Slider(float x, ref float y, float w, float line, float s, string label, float value, float min, float max)
	{
		GUI.Label(new Rect(x, y, w, 18f * s), $"{label}   {value:0.00}");
		float v = GUI.HorizontalSlider(new Rect(x, y + 20f * s, w, 18f * s), value, min, max);
		y += line;
		return v;
	}
}
