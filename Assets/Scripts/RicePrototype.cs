using System.Collections.Generic;
using UnityEngine;

// Pirinç ayıklama prototipi — 3B, gerçek fizik.
// Tek soru: parmağını yığına daldırıp karıştırmak iyi hissettiriyor mu?
// Model yok, sanat yok. Taneler kapsül, taşlar küre. Fiziği PhysX yapıyor.
public class RicePrototype : MonoBehaviour
{
	// --- Alan ---
	[System.NonSerialized] public float TraySize = 1.0f;
	// Zemin daraldı: aynı pirinç dar alanda üst üste biniyor, yığın derinleşiyor.
	// Geniş zeminde taneler tek katmana yayılıyordu, boğulma hissi oluşmuyordu.
	[System.NonSerialized] public float FloorSpan = 1.3f;
	[System.NonSerialized] public float PileSpread = 0.07f;   // tur başında yığın ne kadar dar düşsün
	[System.NonSerialized] public float BinSize = 0.34f;
	[System.NonSerialized] public float BinWall = 0.07f;
	[System.NonSerialized] public float BinScreenY = 0.085f;  // kutunun ekrandaki yüksekliği
	[System.NonSerialized] public float GuardHeight = 0.10f;  // yığını tutan alçak bariyer

	// --- Taneler ---
	[System.NonSerialized] public int GrainCount = 900;
	[System.NonSerialized] public int StoneCount = 12;
	[System.NonSerialized] public float GrainLength = 0.15f;
	[System.NonSerialized] public float GrainWidth = 0.072f;
	[System.NonSerialized] public float StoneSize = 0.105f;
	[System.NonSerialized] public float GrainMass = 0.01f;
	[System.NonSerialized] public float StoneMass = 0.04f;

	// --- Parmak sondası ---
	[System.NonSerialized] public float ProbeRadius = 0.058f;
	[System.NonSerialized] public float ProbeFollow = 22f;
	[System.NonSerialized] public float GrabRadius = 0.13f;
	[System.NonSerialized] public float FlingScale = 1.0f;

	// Basılı tuttukça parmak yığına batıyor, bıraktığında çıkıyor.
	// Sabit yükseklikte gezince "içine girme" hissi hiç oluşmuyordu.
	[System.NonSerialized] public float RestY = 0.42f;       // yığının üstünde beklediği yer
	[System.NonSerialized] public float DeepY = 0.045f;      // en derin nokta (taban hizası)
	[System.NonSerialized] public float SinkSpeed = 1.2f;    // batma hızı, birim/sn
	[System.NonSerialized] public float RiseSpeed = 2.2f;    // çıkma hızı
	[System.NonSerialized] public float DeepDrag = 0.30f;    // derinde takip ne kadar yavaşlasın
	[System.NonSerialized] public float FingerLength = 0.30f;   // fizik kapsülünün boyu
	[System.NonSerialized] public float FingerTilt = 32f;       // kameraya doğru yatma açısı
	[System.NonSerialized] public float FingerScreenFrac = 0.20f;  // 2B parmağın ekran yüksekliğine oranı
	[System.NonSerialized] public float FingerSpriteTilt = 18f;    // 2B parmağın ekrandaki eğimi

	// --- Kamera ---
	// Alçak açı: yığının profilini görüyorsun, yüksekliği okunuyor.
	// Tepeden bakınca ne kadar derin olduğu hiç anlaşılmıyordu.
	[System.NonSerialized] public float CamAngle = 36f;
	[System.NonSerialized] public float CamDistance = 1.5f;
	[System.NonSerialized] public float CamHeightOffset = 0.18f;

	// --- Fizik ---
	// Sürtünme yığının şev açısını belirliyor: yüksek sürtünme = dik ve yüksek yığın.
	[System.NonSerialized] public float Friction = 1.0f;
	[System.NonSerialized] public float Bounce = 0f;

	private Camera cam;
	private Transform root;
	private Rigidbody probe;
	private Transform probeVisual, fingerPivot;
	private SpriteRenderer fingerSr;

	private readonly List<Rigidbody> grains = new List<Rigidbody>();
	private readonly List<Rigidbody> stones = new List<Rigidbody>();

	private Material riceMat, stoneMat, trayMat, probeMat;
	private PhysicMaterial grainPhys;

	// Blender'dan gelen pirinç modeli. Çarpışma için yine kapsül kullanılıyor:
	// 650 adet mesh collider mobilde kaldırılacak gibi değil.
	private Mesh riceMesh;
	private float riceMeshLength = 1f;
	private float riceMeshRadius = 0.25f;

	private Vector3 probeTarget;
	private Vector3 probeVel;
	private float probeY;
	private bool dragging;

	private Rigidbody heldStone;
	private int stonesRemoved;
	private Vector3 binPos;
	private Vector3 heapCenter;
	private Collider binLid;

	private bool showTuning = true;

	private void Awake()
	{
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 120;
		Screen.sleepTimeout = SleepTimeout.NeverSleep;

		// Granül yığında çok fazla temas var; çözücüyü biraz güçlendirmek
		// yığının oturmasını ve uykuya geçmesini sağlıyor.
		Physics.defaultSolverIterations = 8;
		Physics.defaultSolverVelocityIterations = 2;
		Time.fixedDeltaTime = 1f / 60f;

		LoadPrefs();
		LoadRiceMesh();
		MakeMaterials();
		SetupCamera();
		SetupLight();

		root = new GameObject("Scene").transform;

		BuildTray();
		BuildProbe();
		SpawnPile();
		ApplyBinIgnores();
	}

	// Ayarlar cihazda saklanıyor: Play'den çıkıp girince ya da uygulamayı
	// kapatıp açınca bulduğun değerler duruyor.
	private const string PrefPrefix = "rice.";

	// Koddaki varsayılanları her değiştirdiğimde bu sayı artıyor.
	// Eski kayıt otomatik düşüyor, yeni değerler geçerli oluyor —
	// böylece senin ayarların kalıcı ama benim değişikliklerim de ulaşıyor.
	private const int DefaultsVersion = 3;

	private void LoadPrefs()
	{
		if (PlayerPrefs.GetInt(PrefPrefix + "version", 0) != DefaultsVersion)
		{
			PlayerPrefs.DeleteKey(PrefPrefix + "saved");
			return;
		}

		if (PlayerPrefs.GetInt(PrefPrefix + "saved", 0) == 0)
			return;

		CamAngle = Get("CamAngle", CamAngle);
		CamDistance = Get("CamDistance", CamDistance);
		DeepY = Get("DeepY", DeepY);
		SinkSpeed = Get("SinkSpeed", SinkSpeed);
		DeepDrag = Get("DeepDrag", DeepDrag);
		GrabRadius = Get("GrabRadius", GrabRadius);
		ProbeFollow = Get("ProbeFollow", ProbeFollow);
		FingerScreenFrac = Get("FingerScreenFrac", FingerScreenFrac);
		FingerSpriteTilt = Get("FingerSpriteTilt", FingerSpriteTilt);
		ProbeRadius = Get("ProbeRadius", ProbeRadius);
		FingerLength = Get("FingerLength", FingerLength);
		GrainCount = Mathf.RoundToInt(Get("GrainCount", GrainCount));
		Friction = Get("Friction", Friction);
		PileSpread = Get("PileSpread", PileSpread);
		BinScreenY = Get("BinScreenY", BinScreenY);
	}

	private void SavePrefs()
	{
		Set("CamAngle", CamAngle);
		Set("CamDistance", CamDistance);
		Set("DeepY", DeepY);
		Set("SinkSpeed", SinkSpeed);
		Set("DeepDrag", DeepDrag);
		Set("GrabRadius", GrabRadius);
		Set("ProbeFollow", ProbeFollow);
		Set("FingerScreenFrac", FingerScreenFrac);
		Set("FingerSpriteTilt", FingerSpriteTilt);
		Set("ProbeRadius", ProbeRadius);
		Set("FingerLength", FingerLength);
		Set("GrainCount", GrainCount);
		Set("Friction", Friction);
		Set("PileSpread", PileSpread);
		Set("BinScreenY", BinScreenY);

		PlayerPrefs.SetInt(PrefPrefix + "saved", 1);
		PlayerPrefs.SetInt(PrefPrefix + "version", DefaultsVersion);
		PlayerPrefs.Save();
	}

	private static float Get(string key, float fallback) => PlayerPrefs.GetFloat(PrefPrefix + key, fallback);
	private static void Set(string key, float value) => PlayerPrefs.SetFloat(PrefPrefix + key, value);

	private void OnApplicationQuit() => SavePrefs();

	private void OnApplicationPause(bool paused)
	{
		if (paused)
			SavePrefs();
	}

	private void LoadRiceMesh()
	{
		var prefab = Resources.Load<GameObject>("Models/RiceGrain");

		if (prefab == null)
		{
			Debug.LogWarning("RiceGrain.fbx bulunamadı, kapsüle düşülüyor.");
			return;
		}

		var mf = prefab.GetComponentInChildren<MeshFilter>();
		if (mf == null || mf.sharedMesh == null)
			return;

		riceMesh = mf.sharedMesh;

		// Modelin gerçek ölçüsünü okuyup normalleştiriyoruz: içe aktarma ölçeği
		// ne olursa olsun "uzunluk = GrainLength" sözü bozulmasın.
		Vector3 size = riceMesh.bounds.size;
		riceMeshLength = Mathf.Max(size.x, size.y, size.z);
		riceMeshRadius = Mathf.Min(size.x, size.z) * 0.5f;
	}

	private void MakeMaterials()
	{
		var shader = Shader.Find("Standard");

		riceMat = new Material(shader) { color = new Color(0.94f, 0.91f, 0.80f) };
		riceMat.SetFloat("_Glossiness", 0.25f);

		stoneMat = new Material(shader) { color = new Color(0.22f, 0.20f, 0.19f) };
		stoneMat.SetFloat("_Glossiness", 0.1f);

		trayMat = new Material(shader) { color = new Color(0.40f, 0.39f, 0.42f) };
		trayMat.SetFloat("_Glossiness", 0.05f);

		probeMat = new Material(shader) { color = new Color(0.86f, 0.66f, 0.54f) };
		probeMat.SetFloat("_Glossiness", 0.2f);

		grainPhys = new PhysicMaterial("Grain")
		{
			dynamicFriction = Friction,
			staticFriction = Friction,
			bounciness = Bounce,
			frictionCombine = PhysicMaterialCombine.Average,
			bounceCombine = PhysicMaterialCombine.Minimum
		};
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
		cam.fieldOfView = 45f;
		cam.nearClipPlane = 0.05f;
		cam.farClipPlane = 50f;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0.14f, 0.14f, 0.16f);

		PlaceCamera();
	}

	private void PlaceCamera()
	{
		float rad = CamAngle * Mathf.Deg2Rad;
		Vector3 pivot = new Vector3(0f, CamHeightOffset, 0f);

		cam.transform.position = pivot + new Vector3(0f, Mathf.Sin(rad), -Mathf.Cos(rad)) * CamDistance;
		cam.transform.LookAt(pivot);
	}

	private void SetupLight()
	{
		var go = new GameObject("Sun");
		var l = go.AddComponent<Light>();
		l.type = LightType.Directional;
		l.intensity = 1.1f;
		l.shadows = LightShadows.Soft;
		l.shadowStrength = 0.55f;
		go.transform.rotation = Quaternion.Euler(50f, -25f, 0f);

		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
		RenderSettings.ambientSkyColor = new Color(0.55f, 0.55f, 0.6f);
		RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.45f);
		RenderSettings.ambientGroundColor = new Color(0.25f, 0.25f, 0.28f);
	}

	// Duvar yok: açık bir tezgah. Pirinci kenara itip yayabilesin, incelttikçe
	// altından taş çıksın. Kapalı kutuda yığın hiç dağılmıyordu.
	private void BuildTray()
	{
		float t = 0.05f;
		float floor = TraySize * FloorSpan;

		AddBox("Floor", new Vector3(0f, -t * 0.5f, 0f), new Vector3(floor * 1.6f, t, floor * 1.6f));

		// Taş kutusu ekranın alt ortasına oturuyor: konumu sabit sayıdan değil,
		// kameradan ekranın alt orta noktasına ışın atarak bulunuyor. Böylece
		// kamera açısını değiştirince kutu ekran dışında kalmıyor.
		binPos = ViewportToFloor(0.5f, BinScreenY);

		// Yığın alanı: önde kutunun hemen arkasındaki bariyerden başlıyor,
		// arkada zeminin kenarında bitiyor. Kutunun çevresi böylece temiz kalıyor.
		float guardZ = binPos.z + BinSize * 0.5f + 0.10f;
		float backZ = floor * 0.5f;
		float halfW = floor * 0.5f;
		float depth = backZ - guardZ;

		heapCenter = new Vector3(0f, 0f, (guardZ + backZ) * 0.5f);

		AddBox("GuardFront", new Vector3(0f, GuardHeight * 0.5f, guardZ),
			new Vector3(floor + t * 2f, GuardHeight, t));

		AddBox("GuardBack", new Vector3(0f, GuardHeight * 0.5f, backZ),
			new Vector3(floor + t * 2f, GuardHeight, t));

		AddBox("GuardL", new Vector3(-halfW, GuardHeight * 0.5f, heapCenter.z),
			new Vector3(t, GuardHeight, depth));

		AddBox("GuardR", new Vector3(halfW, GuardHeight * 0.5f, heapCenter.z),
			new Vector3(t, GuardHeight, depth));

		var binMat = new Material(Shader.Find("Standard")) { color = new Color(0.45f, 0.2f, 0.16f) };
		float half = BinSize * 0.5f;
		float wt = 0.02f;

		AddBox("BinFloor", binPos + new Vector3(0f, 0.01f, 0f), new Vector3(BinSize, 0.02f, BinSize))
			.GetComponent<Renderer>().material = binMat;

		AddBox("BinN", binPos + new Vector3(0f, BinWall * 0.5f, half), new Vector3(BinSize, BinWall, wt))
			.GetComponent<Renderer>().material = binMat;

		AddBox("BinS", binPos + new Vector3(0f, BinWall * 0.5f, -half), new Vector3(BinSize, BinWall, wt))
			.GetComponent<Renderer>().material = binMat;

		AddBox("BinW", binPos + new Vector3(-half, BinWall * 0.5f, 0f), new Vector3(wt, BinWall, BinSize))
			.GetComponent<Renderer>().material = binMat;

		AddBox("BinE", binPos + new Vector3(half, BinWall * 0.5f, 0f), new Vector3(wt, BinWall, BinSize))
			.GetComponent<Renderer>().material = binMat;

		// Görünmez kapak: pirinç kutuya giremiyor. Taşlar için bu çarpışma
		// kapatılıyor, onlar kapağın içinden geçip kutuya düşüyor.
		var lid = AddBox("BinLid", binPos + new Vector3(0f, BinWall + 0.01f, 0f),
			new Vector3(BinSize + wt * 2f, 0.02f, BinSize + wt * 2f));

		lid.GetComponent<Renderer>().enabled = false;
		binLid = lid.GetComponent<Collider>();

		ApplyBinIgnores();
	}

	private void ApplyBinIgnores()
	{
		if (binLid == null)
			return;

		foreach (var s in stones)
		{
			if (s == null)
				continue;

			var c = s.GetComponent<Collider>();
			if (c != null)
				Physics.IgnoreCollision(c, binLid, true);
		}
	}

	private void RebuildTray()
	{
		for (int i = root.childCount - 1; i >= 0; i--)
		{
			var c = root.GetChild(i);
			if (c.name.StartsWith("Bin") || c.name == "Floor")
				Destroy(c.gameObject);
		}

		BuildTray();
	}

	private Vector3 ViewportToFloor(float vx, float vy)
	{
		Ray ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
		var floorPlane = new Plane(Vector3.up, Vector3.zero);

		if (floorPlane.Raycast(ray, out float enter))
		{
			Vector3 p = ray.GetPoint(enter);
			return new Vector3(p.x, 0f, p.z);
		}

		return new Vector3(0f, 0f, -TraySize);
	}

	private GameObject AddBox(string name, Vector3 pos, Vector3 size)
	{
		var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
		go.name = name;
		go.transform.SetParent(root, false);
		go.transform.position = pos;
		go.transform.localScale = size;

		var r = go.GetComponent<Renderer>();
		r.sharedMaterial = trayMat;
		r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		go.GetComponent<Collider>().sharedMaterial = grainPhys;
		return go;
	}

	// Parmak: kök nesne Rigidbody, altındaki kapsül ucu tam kökün konumunda.
	// Böylece "kazdığın nokta" parmağın ucu oluyor, merkezi değil.
	private void BuildProbe()
	{
		var rootGo = new GameObject("Probe");
		rootGo.transform.SetParent(root, false);

		var pivot = new GameObject("FingerPivot").transform;
		pivot.SetParent(rootGo.transform, false);

		var finger = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		finger.name = "Finger";
		finger.transform.SetParent(pivot, false);

		// Kapsül sadece fizik için: görünmüyor, parmağı 2B sprite çiziyor.
		finger.GetComponent<Renderer>().enabled = false;
		finger.GetComponent<Collider>().sharedMaterial = grainPhys;

		probe = rootGo.AddComponent<Rigidbody>();
		probe.isKinematic = true;
		probe.useGravity = false;
		probe.interpolation = RigidbodyInterpolation.Interpolate;

		probeVisual = finger.transform;
		fingerPivot = pivot;
		ShapeFinger();

		probeY = RestY;
		rootGo.transform.position = new Vector3(0f, probeY, 0f);
		probeTarget = rootGo.transform.position;

		BuildFingerSprite();
	}

	// 2B parmak: kameranın çocuğu, sabit uzaklıkta duruyor.
	// Yani hep tanelerin önünde ve ekrandaki boyu hiç değişmiyor.
	private void BuildFingerSprite()
	{
		var go = new GameObject("FingerSprite");
		go.transform.SetParent(cam.transform, false);

		fingerSr = go.AddComponent<SpriteRenderer>();
		fingerSr.sprite = Resources.Load<Sprite>("Art/Finger") ?? MakePlaceholderFinger();
		fingerSr.sortingOrder = 100;
	}

	// Maya'dan/Photoshop'tan Finger.png gelene kadar kaba bir parmak silueti.
	private Sprite MakePlaceholderFinger()
	{
		const int w = 96;
		const int h = 256;
		var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };

		var skin = new Color(0.90f, 0.72f, 0.60f);
		var edge = new Color(0.72f, 0.54f, 0.44f);

		for (int y = 0; y < h; y++)
		{
			// Uca doğru hafif inceliyor.
			float t = y / (float)(h - 1);
			float halfWidth = Mathf.Lerp(w * 0.30f, w * 0.44f, Mathf.SmoothStep(0f, 1f, t));
			float tipRound = Mathf.Min(1f, y / (w * 0.42f));

			for (int x = 0; x < w; x++)
			{
				float dx = Mathf.Abs(x - w * 0.5f);
				float limit = halfWidth * Mathf.Sqrt(Mathf.Clamp01(tipRound));
				float a = Mathf.Clamp01(limit - dx);

				var c = Color.Lerp(edge, skin, Mathf.Clamp01((limit - dx) / Mathf.Max(limit, 1f) * 2.2f));
				tex.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
			}
		}

		tex.Apply();

		// Pivot ucun biraz üstünde: sprite'ın ucu kazdığın noktaya oturuyor.
		return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.06f), 100f);
	}

	private void ShapeFinger()
	{
		// Kapsül primitifi 2 birim boyunda, 0.5 yarıçapında; ölçek buna göre.
		float halfLen = FingerLength * 0.5f;

		fingerPivot.localRotation = Quaternion.Euler(-FingerTilt, 0f, 0f);
		probeVisual.localScale = new Vector3(ProbeRadius * 2f, halfLen, ProbeRadius * 2f);

		// Ucu kökte kalsın diye gövde kendi ekseninde yukarı kaydırılıyor.
		probeVisual.localPosition = new Vector3(0f, halfLen, 0f);
	}

	// Yığını silip yeniden döküyor. Yükseklik üç şeye bağlı: tane sayısı,
	// sürtünme (şev açısı) ve dökülme sütununun darlığı.
	private void Respawn()
	{
		foreach (var g in grains)
			if (g != null)
				Destroy(g.gameObject);

		foreach (var s in stones)
			if (s != null)
				Destroy(s.gameObject);

		grains.Clear();
		stones.Clear();
		heldStone = null;
		stonesRemoved = 0;

		grainPhys.dynamicFriction = Friction;
		grainPhys.staticFriction = Friction;
		grainPhys.bounciness = Bounce;

		SpawnPile();
		ApplyBinIgnores();
	}

	private void SpawnPile()
	{
		// Dar bir sütundan dökülüyorlar: duvar olmadığı için yığını kendi şevi tutuyor.
		float spread = TraySize * PileSpread;

		for (int i = 0; i < GrainCount + StoneCount; i++)
		{
			bool isStone = i < StoneCount;

			GameObject go;

			if (isStone || riceMesh == null)
			{
				go = GameObject.CreatePrimitive(isStone ? PrimitiveType.Sphere : PrimitiveType.Capsule);
			}
			else
			{
				// Görünüş Blender modeli, çarpışma kapsül.
				go = new GameObject("Grain");
				go.AddComponent<MeshFilter>().sharedMesh = riceMesh;
				go.AddComponent<MeshRenderer>();

				var cc = go.AddComponent<CapsuleCollider>();
				cc.direction = 1;                       // uzun eksen Y
				cc.height = riceMeshLength;
				cc.radius = riceMeshRadius;
				cc.center = riceMesh.bounds.center;
			}

			go.name = isStone ? "Stone" : "Grain";
			go.transform.SetParent(root, false);

			// Taşlar doğrudan tabana seriliyor, pirinç üstlerine dökülüyor.
			// Böylece hepsi gerçekten gömülü başlıyor; kazmadan bulamıyorsun.
			go.transform.position = heapCenter + (isStone
				? new Vector3(Random.Range(-spread, spread), StoneSize * 0.6f, Random.Range(-spread, spread))
				: new Vector3(Random.Range(-spread, spread), 0.30f + i * 0.0025f, Random.Range(-spread, spread)));

			go.transform.rotation = Random.rotation;

			if (isStone)
			{
				go.transform.localScale = Vector3.one * StoneSize * Random.Range(0.8f, 1.2f);
			}
			else if (riceMesh != null)
			{
				// Model kendi oranlarını koruyor; sadece uzunluğunu ayarlıyoruz.
				float k = GrainLength / riceMeshLength * Random.Range(0.9f, 1.1f);
				go.transform.localScale = Vector3.one * k;
			}
			else
			{
				go.transform.localScale = new Vector3(GrainWidth, GrainLength * 0.5f, GrainWidth);
			}

			var r = go.GetComponent<Renderer>();
			r.sharedMaterial = isStone ? stoneMat : riceMat;

			// Yüzlerce gölge mobilde pahalı; sadece ışık alsınlar yetiyor.
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			go.GetComponent<Collider>().sharedMaterial = grainPhys;

			var rb = go.AddComponent<Rigidbody>();
			rb.mass = isStone ? StoneMass : GrainMass;
			rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
			rb.interpolation = RigidbodyInterpolation.None;

			// Uyku eşiği: yığın oturunca hesaplanmayı bıraksın. Titremenin ilacı bu.
			rb.sleepThreshold = 0.012f;

			if (isStone)
				stones.Add(rb);
			else
				grains.Add(rb);
		}
	}

	private void Update()
	{
		// Basılı tutuyorsan batıyor. Ama taşı yakaladığın an yukarı çıkıyorsun:
		// taşı yığından çıkarıp kutuya taşıyorsun, dipte sürüklemiyorsun.
		bool diving = dragging && heldStone == null;

		float targetY = diving ? DeepY : RestY;
		float speed = diving ? SinkSpeed : RiseSpeed;
		probeY = Mathf.MoveTowards(probeY, targetY, speed * Time.deltaTime);

		if (Input.GetMouseButtonDown(0))
		{
			dragging = true;
		}
		else if (Input.GetMouseButtonUp(0))
		{
			ReleaseStone();
			dragging = false;
		}

		if (PointerOnPlane(out Vector3 p))
			probeTarget = p;

		// Yakalama her karede deneniyor: parmak battıkça altındaki taşa ulaşıyorsun.
		if (dragging && heldStone == null)
			TryGrabStone(probeTarget);

		CollectStones();
	}

	private void LateUpdate()
	{
		if (fingerSr == null)
			return;

		// Sprite kameradan sabit uzaklıkta; ekrandaki yeri sondanın ekrandaki yeri.
		const float dist = 0.6f;

		Vector3 screen = cam.WorldToScreenPoint(probe.position);
		screen.z = dist;

		fingerSr.transform.position = cam.ScreenToWorldPoint(screen);
		fingerSr.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, FingerSpriteTilt);

		// Ekran yüksekliğine oranla sabit boy: perspektif onu büyütüp küçültmüyor.
		float frustumH = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
		float want = frustumH * FingerScreenFrac;
		float k = want / Mathf.Max(fingerSr.sprite.bounds.size.y, 0.0001f);

		fingerSr.transform.localScale = new Vector3(k, k, 1f);
	}

	private bool PointerOnPlane(out Vector3 point)
	{
		// İmleç her zaman ZEMİNDEKİ noktaya bakıyor, sondanın yüksekliğine değil.
		// Yoksa taşı yukarıda taşırken kutuyu işaret etmek işe yaramıyor:
		// ışın yüksek düzleme çarpıp kutunun çok önüne denk geliyordu.
		var plane = new Plane(Vector3.up, Vector3.zero);
		Ray ray = cam.ScreenPointToRay(Input.mousePosition);

		if (plane.Raycast(ray, out float enter))
		{
			Vector3 hit = ray.GetPoint(enter);

			// Sınır zeminin tamamı: kutu kenarda duruyor, parmak oraya ulaşabilmeli.
			float limit = TraySize * FloorSpan * 0.5f;
			point = new Vector3(Mathf.Clamp(hit.x, -limit, limit), probeY, Mathf.Clamp(hit.z, -limit, limit));
			return true;
		}

		point = default;
		return false;
	}

	// 0 = yüzeyde, 1 = en derinde
	private float DepthT => Mathf.InverseLerp(RestY, DeepY, probeY);

	private void FixedUpdate()
	{
		// Derinde malzeme direnç gösteriyor: parmak ağırlaşıyor, geriden geliyor.
		float follow = ProbeFollow * Mathf.Lerp(1f, DeepDrag, DepthT);

		// Sonda kinematik: MovePosition ile hareket edince taneleri doğru itiyor.
		Vector3 next = Vector3.Lerp(probe.position, probeTarget, 1f - Mathf.Exp(-follow * Time.fixedDeltaTime));
		probeVel = (next - probe.position) / Time.fixedDeltaTime;
		probe.MovePosition(next);

		if (heldStone != null)
			heldStone.MovePosition(next);
	}

	// Yakalama yatay mesafeye bakıyor. 3B mesafeye bakınca, taş dipteyken parmak
	// daha yüzeyde olduğu için aradaki yükseklik farkı yarıçabı aşıyor ve hiç tutmuyordu.
	private void TryGrabStone(Vector3 at)
	{
		Rigidbody best = null;
		float bestSqr = GrabRadius * GrabRadius;

		foreach (var s in stones)
		{
			if (s == null)
				continue;

			Vector3 d = s.position - at;
			d.y = 0f;

			float sqr = d.sqrMagnitude;
			if (sqr < bestSqr)
			{
				bestSqr = sqr;
				best = s;
			}
		}

		if (best == null)
			return;

		heldStone = best;
		heldStone.isKinematic = true;
	}

	private void ReleaseStone()
	{
		if (heldStone == null)
			return;

		heldStone.isKinematic = false;
		heldStone.velocity = probeVel * FlingScale;
		heldStone = null;
	}

	private void CollectStones()
	{
		for (int i = stones.Count - 1; i >= 0; i--)
		{
			var s = stones[i];

			if (s == null)
			{
				stones.RemoveAt(i);
				continue;
			}

			if (s == heldStone)
				continue;

			bool inBin = Mathf.Abs(s.position.x - binPos.x) < BinSize * 0.5f
				&& Mathf.Abs(s.position.z - binPos.z) < BinSize * 0.5f
				&& s.position.y < BinWall + 0.05f;

			bool lost = s.position.y < -1f;

			if (inBin || lost)
			{
				if (inBin)
					stonesRemoved++;

				Destroy(s.gameObject);
				stones.RemoveAt(i);
			}
		}
	}

	private void OnGUI()
	{
		float s = Mathf.Max(Screen.width, Screen.height) / 900f;
		GUI.skin.label.fontSize = Mathf.RoundToInt(19 * s);
		GUI.skin.button.fontSize = Mathf.RoundToInt(19 * s);

		float pad = 14f * s;
		float line = 34f * s;

		GUI.Label(new Rect(pad, pad, 700f * s, line), $"Çıkarılan taş: {stonesRemoved}/{StoneCount}");
		GUI.Label(new Rect(pad, pad + line, 700f * s, line), $"Kalan taş: {stones.Count}    FPS {Mathf.RoundToInt(1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f))}");

		if (GUI.Button(new Rect(Screen.width - 150f * s - pad, pad, 150f * s, 44f * s), showTuning ? "Kapat" : "Ayarlar"))
		{
			showTuning = !showTuning;

			// Paneli kapatınca değerler diske yazılıyor.
			if (!showTuning)
				SavePrefs();
		}

		if (!showTuning)
			return;

		// Panel sıkışık: 13 satır büyük punto ile ekrandan taşıyordu.
		int bigFont = GUI.skin.label.fontSize;
		GUI.skin.label.fontSize = Mathf.RoundToInt(13 * s);

		float w = 330f * s;
		float x = Screen.width - w - 10f * s;
		float y = 62f * s;
		float ln = 42f * s;

		GUI.Box(new Rect(x - 8f * s, y - 8f * s, w + 16f * s, ln * 15f + 52f * s), "");

		float a = Slider(x, ref y, w, ln, s, "Kamera açısı", CamAngle, 20f, 89f);
		float d = Slider(x, ref y, w, ln, s, "Kamera uzaklığı", CamDistance, 0.6f, 4f);

		if (!Mathf.Approximately(a, CamAngle) || !Mathf.Approximately(d, CamDistance))
		{
			CamAngle = a;
			CamDistance = d;
			PlaceCamera();
		}

		DeepY = Slider(x, ref y, w, ln, s, "En derin nokta", DeepY, 0.01f, 0.5f);
		SinkSpeed = Slider(x, ref y, w, ln, s, "Batma hızı", SinkSpeed, 0.1f, 4f);
		DeepDrag = Slider(x, ref y, w, ln, s, "Derinde direnç", DeepDrag, 0.05f, 1f);
		GrabRadius = Slider(x, ref y, w, ln, s, "Yakalama yarıçapı", GrabRadius, 0.05f, 0.3f);
		ProbeFollow = Slider(x, ref y, w, ln, s, "Takip hızı", ProbeFollow, 4f, 60f);

		FingerScreenFrac = Slider(x, ref y, w, ln, s, "2B parmak boyu", FingerScreenFrac, 0.1f, 0.8f);
		FingerSpriteTilt = Slider(x, ref y, w, ln, s, "2B parmak eğimi", FingerSpriteTilt, -45f, 45f);

		float pr = Slider(x, ref y, w, ln, s, "Fizik kalınlığı", ProbeRadius, 0.03f, 0.2f);
		float fl = Slider(x, ref y, w, ln, s, "Fizik uzunluğu", FingerLength, 0.15f, 1f);

		if (!Mathf.Approximately(pr, ProbeRadius) || !Mathf.Approximately(fl, FingerLength))
		{
			ProbeRadius = pr;
			FingerLength = fl;
			ShapeFinger();
		}

		// --- Yığın yüksekliğini belirleyen üç ayar ---
		GrainCount = Mathf.RoundToInt(Slider(x, ref y, w, ln, s, "Tane sayısı", GrainCount, 150f, 1400f));
		Friction = Slider(x, ref y, w, ln, s, "Sürtünme (şev açısı)", Friction, 0.2f, 2f);
		PileSpread = Slider(x, ref y, w, ln, s, "Dökülme darlığı", PileSpread, 0.03f, 0.4f);

		float by = Slider(x, ref y, w, ln, s, "Kutu yüksekliği", BinScreenY, 0.02f, 0.4f);
		if (!Mathf.Approximately(by, BinScreenY))
		{
			BinScreenY = by;
			RebuildTray();
		}

		// Buton en sonda ve font geri alınmadan önce: aksi halde üstündeki
		// satırlarla çakışıyordu.
		float bw = (w - 6f * s) * 0.5f;

		if (GUI.Button(new Rect(x, y + 2f * s, bw, 34f * s), "Yeniden dök"))
			Respawn();

		if (GUI.Button(new Rect(x + bw + 6f * s, y + 2f * s, bw, 34f * s), "Sıfırla"))
		{
			PlayerPrefs.DeleteKey(PrefPrefix + "saved");
			PlayerPrefs.Save();
		}

		GUI.skin.label.fontSize = bigFont;
	}

	private float Slider(float x, ref float y, float w, float line, float s, string label, float value, float min, float max)
	{
		GUI.Label(new Rect(x, y, w, 18f * s), $"{label}   {value:0.00}");
		float v = GUI.HorizontalSlider(new Rect(x, y + 20f * s, w, 18f * s), value, min, max);
		y += line;
		return v;
	}
}
