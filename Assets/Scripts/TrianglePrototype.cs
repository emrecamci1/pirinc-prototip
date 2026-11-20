using System.Collections.Generic;
using UnityEngine;

// Ucgen devirme his testi.
// Basamakli bir kulenin dibinden baslayip ayni renktekileri toplayarak yukari tirmaniyorsun.
// Hepsi toplaninca tepedeki bayrak aciliyor; ona ulasmak bolumu bitiriyor.
public class TrianglePrototype : MonoBehaviour
{
	private const int DefaultsVersion = 27;

	// Yonelim = dik acinin hucre icinde hangi kosede oldugu.
	// bit0: 0 = sol, 1 = sag     bit1: 0 = alt, 1 = ust
	// Ayni hucrede sadece tamamlayici yonelim (ori ^ 3) durabilir.
	private static readonly int[] DirX = { 1, -1, 0, 0 };
	private static readonly int[] DirY = { 0, 0, 1, -1 };

	// 0 = sol-alt, 1 = sag-alt, 2 = sol-ust, 3 = sag-ust
	private static readonly float[] OriAngle = { 0f, 270f, 90f, 180f };
	// Ucgen duz bir karo gibi sadece KENARLARININ uzerinden takla atar, hic kaymaz.
	// 0 : yatay bacagin uzerinden -> dikey komsu hucre
	// 1 : dikey bacagin uzerinden -> yatay komsu hucre
	// 2 : hipotenusun uzerinden   -> ayni karenin obur yarisi
	// Ucgenin kenari uctur, dolayisiyla hamlesi de uctur. Baska hamle yok.
	// Daha once dort ok yonunun hepsi calissin diye iki ve uc taklalik bilesik
	// hamleler, yerinde donme ve inisi kaydirma vardi; hepsi uc kenarli sekle
	// dort yonlu girdi dayatmanin yamasiydi ve hareketi dogal olmaktan cikariyordu.
	private const int MoveCount = 3;
	private const int Flip = 2;

	private const float PoleLen = 1.7f;
	private const float IntroTime = 1.1f;
	private const float WinTime = 0.6f;
	private const float WideFov = 45f;

	private static readonly Color[] Palette =
	{
		new Color(0.88f, 0.42f, 0.40f),
		new Color(0.46f, 0.78f, 0.47f),
		new Color(0.93f, 0.76f, 0.34f),
		new Color(0.70f, 0.50f, 0.86f),
	};

	[System.NonSerialized] public int GridW = 9;
	[System.NonSerialized] public int GridH = 18;
	[System.NonSerialized] public float Density = 0.80f;
	[System.NonSerialized] public float BlueRatio = 0.22f;
	[System.NonSerialized] public float SlideSpeed = 13f;
	[System.NonSerialized] public bool Prism = true;
	[System.NonSerialized] public float PrismPitch = 16f;
	[System.NonSerialized] public float PrismDist = 40f;
	[System.NonSerialized] public bool Rival = true;
	[System.NonSerialized] public float RivalDelay = 0.22f;
	[System.NonSerialized] public bool Marks = true;
	[System.NonSerialized] public bool Hints = true;
	[System.NonSerialized] public bool Chain;
	// Adim artik skor degil butce: bitince kaybediyorsun. Mavi yemek geri veriyor.
	// Hissetmedigin bedel bedel degil; sayaci bu yuzden ters cevirdik.
	[System.NonSerialized] public float BudgetPerRow = 6f;
	[System.NonSerialized] public float BlueRefund = 3f;
	[System.NonSerialized] public float HalfRatio = 0.20f;
	[System.NonSerialized] public float Shoulder = 0.55f;
	[System.NonSerialized] public float TumbleTime = 0.13f;
	[System.NonSerialized] public float HopHeight = 0.06f;
	[System.NonSerialized] public float PopTime = 0.22f;
	[System.NonSerialized] public float CamAngle = 58f;
	[System.NonSerialized] public float CamZoom = 1f;
	[System.NonSerialized] public float CloseRows = 7f;
	[System.NonSerialized] public float CloseAngle = 62f;
	[System.NonSerialized] public float CloseFov = 14f;
	[System.NonSerialized] public float CloseZoom = 1.35f;
	[System.NonSerialized] public float IntroHold = 0.9f;
	[System.NonSerialized] public float SwipeMin = 45f;
	[System.NonSerialized] public float Inset = 0.07f;
	[System.NonSerialized] public float TriThick = 0.10f;
	[System.NonSerialized] public float WallChance = 0.55f;
	[System.NonSerialized] public float WallMax = 5f;

	private struct Tri
	{
		public int Cx, Cy, Ori;
		// 0 = kendi rengin (mavi), >0 = palet rengi. Renk hem hedef hem yakit.
		public int Col;
		public Transform T;
	}

	private struct Move
	{
		public int Cx, Cy, Ori;
		public int A, B;
		// Kayma tek hamlede cok ucgen yiyor, A/B yetmiyor.
		public int[] Swept;
		public int Cost;
		public int Spent;
	}

	private const int Me = 0;
	// Rakibin topladigi renk: palette[0], kirmizi. Rakip de kirmizi, tipki oyuncunun
	// parlak mavi olup koyu mavi toplamasi gibi: kimin neyi topladigi renkten okunuyor.
	private const int RivalCol = 1;

	private Camera cam;
	private Transform board;
	private Mesh triMesh;
	private Material matMe;
	private Material matTarget;
	private Material matHintTake;
	private Material matBreak;
	private Material matHint;
	// Satir basina zemin rengi, komsu yuzler icin iki tonu: [satir * 2 + tekcift].
	private Material[] matFloorRow;
	private Material matLine;
	private Material matWall;
	private Material matRival;
	private Material matPad;
	private Material[] matObstacle;

	private readonly List<Vector3Int> plan = new List<Vector3Int>();
	private readonly List<int> planCol = new List<int>();
	private Tri[] tris;
	private int[,,] occ;
	// Toplanan mavi hem hedefe yazilir hem de harcanabilir menzil olur.
	private int charge;
	private bool sliding;
	private float slideT;
	private float slideDur;
	private int slideCells;
	private int slideDone;
	private Vector3 slideFrom;
	private Vector3 slideTo;
	private int slideCx, slideCy;
	private readonly List<int> slideEat = new List<int>();
	private readonly List<int> slideAt = new List<int>();
	private int takeA, takeB;
	private int lastDir = -1;

	// Kule silueti: her satirin hangi sutunlardan olustugu. Disi tahta disi sayilir.
	// Yatay cizgilerdeki duvarlar: wallH[x, y] = (x, y-1) ile (x, y) arasindaki gecis kapali.
	private bool[,] wallH;
	private int[] rowLo;
	private int[] rowHi;
	private int cellCount;
	private int goalX, goalY;
	private Transform flag;
	private MeshRenderer flagMr;

	private bool tumbling;
	private float tumbleT;
	private Vector3 tumblePivot;
	private Vector3 tumbleAxis;
	private Vector3 tumbleFromPos;
	private Quaternion tumbleFromRot;
	private float tumbleAngle;
	private Vector3 tumbleUp = Vector3.up;

	private Transform[] marks;
	private Transform[] hints;
	// Kaymanin dort hedefi. Gorunurlukten bagimsiz hesaplaniyor: isaretleri kapatsan
	// da kayma calismali, sadece nisan gorunmez olmali.
	private readonly int[] markCx = new int[4];
	private readonly int[] markCy = new int[4];
	private readonly int[] markGone = new int[4];
	private int rivalIdx = -1;
	private bool rivalTumbling;
	private float rivalT;
	private float rivalWait;
	private float rivalAngle;
	private Vector3 rivalPivot;
	private Vector3 rivalAxis;
	private Vector3 rivalFromPos;
	private Vector3 rivalUp = Vector3.up;
	private Quaternion rivalFromRot;
	private int rivalPendCx, rivalPendCy, rivalPendOri;
	private int rivalTakeA, rivalTakeB;
	private bool lost;
	private bool stuck;

	private struct Shard
	{
		public Transform T;
		public Vector3 Vel;
		public Vector3 Spin;
		public float Life;
		public float Max;
		public float Size;
	}

	private Shard[] shards;
	private int shardAt;
	private string lostText = "";
	private int budget;
	private float budgetFlash;
	private float tumbleLift;
	private int pendingCx, pendingCy, pendingOri;
	private int queued = -1;

	private readonly List<int> popping = new List<int>();
	private readonly List<float> popT = new List<float>();

	private readonly List<Move> history = new List<Move>();
	private int steps;
	private bool solved;
	private float solvedAt;
	private float winT;
	private Vector3 winFrom;
	private Quaternion winFromRot;

	private float camWideDist;
	private float camCloseDist;
	private float camBlend;
	private float introT;
	private float focusZ;
	private float focusVel;
	private float focusX;
	private float focusVelX;
	private float camAng;
	private float camAngVel;
	private float focusY;

	private Vector2Int camScreen;
	private Vector2 pressPos;
	private bool pressed;
	private bool showPanel;
	private Vector2 panelScroll;
	private float panelLeft;
	private int seed = 1;

	private void Awake()
	{
		LoadPrefs();

		Application.targetFrameRate = 60;

		cam = Camera.main;
		if (cam == null)
			cam = new GameObject("Main Camera").AddComponent<Camera>();
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0.80f, 0.79f, 0.75f);
		cam.orthographic = false;
		cam.fieldOfView = WideFov;

		triMesh = BuildTriMesh(Inset, TriThick);

		// Tek yonlu isik: kalinligi ve kulenin yuvarlakligini okutan sey bu.
		// Ortam isigi yuksek tutuluyor, golgede kalan yuzler kararmasin.
		var sun = new GameObject("Isik");
		sun.transform.SetParent(transform, false);
		sun.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
		var li = sun.AddComponent<Light>();
		li.type = LightType.Directional;
		li.intensity = 0.95f;
		li.shadows = LightShadows.None;
		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
		RenderSettings.ambientLight = new Color(0.52f, 0.54f, 0.60f);

		matMe = MakeMat(new Color(0.40f, 0.82f, 1.00f));
		matTarget = MakeMat(new Color(0.19f, 0.50f, 0.84f));
		matHintTake = MakeMat(new Color(1.00f, 0.84f, 0.33f));
		matLine = MakeMat(new Color(0.17f, 0.13f, 0.12f));
		matWall = MakeMat(new Color(0.30f, 0.13f, 0.06f));
		matRival = MakeMat(new Color(0.95f, 0.33f, 0.26f));
		// Kirmaya gidecek isaret farkli renkte: sari kare sadece "oraya gidersin" demiyor,
		// "oradakileri ezersin" diyor.
		matBreak = MakeMat(new Color(1.00f, 0.45f, 0.20f));
		matHint = MakeMat(new Color(0.62f, 0.64f, 0.70f));
		matPad = MakeMat(new Color(0.34f, 0.27f, 0.23f));

		matObstacle = new Material[Palette.Length];
		for (int i = 0; i < Palette.Length; i++)
			matObstacle[i] = MakeMat(Palette[i]);

		// Parca havuzu bir kez kuruluyor, tahta yeniden kurulunca yok olmuyor.
		var bin = new GameObject("Parcalar").transform;
		bin.SetParent(transform, false);
		shards = new Shard[220];
		for (int i = 0; i < shards.Length; i++)
		{
			var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
			Destroy(q.GetComponent<Collider>());
			q.name = "Parca";
			q.transform.SetParent(bin, false);
			var mr = q.GetComponent<MeshRenderer>();
			mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			mr.receiveShadows = false;
			q.SetActive(false);
			shards[i].T = q.transform;
		}

		Build();
	}

	// Isikli shader. Unlit'te her yuzey ayni duz renk cikiyordu: ne ucgenin kalinligi
	// ne de kulenin yuvarlakligi okunuyordu, dokuz yuz de birebir ayni gorunuyordu.
	private static Material MakeMat(Color c)
	{
		var sh = Shader.Find("Legacy Shaders/Diffuse");
		if (sh == null)
			sh = Shader.Find("Diffuse");
		if (sh == null)
			sh = Shader.Find("Unlit/Color");

		var m = new Material(sh);
		m.color = c;
		return m;
	}

	private static void AddFace(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 nrm)
	{
		int i = v.Count;
		v.Add(p0);
		v.Add(p1);
		v.Add(p2);
		n.Add(nrm);
		n.Add(nrm);
		n.Add(nrm);
		t.Add(i);
		t.Add(i + 1);
		t.Add(i + 2);
	}

	private static Mesh BuildTriMesh(float inset, float thick)
	{
		// Dik aci yerelde (0,0), bacaklar +X ve +Z boyunca.
		Vector3 a = new Vector3(0f, 0f, 0f);
		Vector3 b = new Vector3(1f, 0f, 0f);
		Vector3 c = new Vector3(0f, 0f, 1f);
		Vector3 mid = (a + b + c) / 3f;
		a = Vector3.LerpUnclamped(a, mid, inset);
		b = Vector3.LerpUnclamped(b, mid, inset);
		c = Vector3.LerpUnclamped(c, mid, inset);

		// Kalinlik yuzeyin iki yanina esit dagiliyor. 180 derecelik takladan sonra
		// ucgen simetrik oldugu icin yine duzgun oturuyor; tek yone kalinlik verseydik
		// her takladan sonra zeminin icine gomulurdu.
		Vector3 up = Vector3.up * (thick * 0.5f);

		var verts = new List<Vector3>();
		var norms = new List<Vector3>();
		var tris = new List<int>();

		AddFace(verts, norms, tris, a + up, c + up, b + up, Vector3.up);
		AddFace(verts, norms, tris, a - up, b - up, c - up, Vector3.down);

		Vector3[] loop = { a, b, c };
		for (int i = 0; i < 3; i++)
		{
			Vector3 p = loop[i];
			Vector3 q = loop[(i + 1) % 3];
			Vector3 nrm = Vector3.Cross(Vector3.up, q - p).normalized;
			AddFace(verts, norms, tris, p - up, q + up, q - up, nrm);
			AddFace(verts, norms, tris, p - up, p + up, q + up, nrm);
		}

		var m = new Mesh();
		m.SetVertices(verts);
		m.SetNormals(norms);
		m.SetTriangles(tris, 0);
		m.RecalculateBounds();
		return m;
	}

	// Prizma kipinde tahta GridW yuzlu bir kulenin etrafina sariliyor. Her YUZ hala duz,
	// bu yuzden ucgen gercekten duz bir zeminin uzerinde duruyor ve butun takla hesabi
	// aynen gecerli kaliyor. Egri bir silindir olsaydi her hucre farkli egilir ve bozulurdu.
	// gx yuzler boyunca donuyor, gy kule boyunca yukari cikiyor, yuzey normali disa bakiyor.
	private float PrismRadius()
	{
		return 0.5f / Mathf.Sin(Mathf.PI / GridW);
	}

	private Vector3 PrismCorner(int i)
	{
		float a = i / (float)GridW * Mathf.PI * 2f;
		float r = PrismRadius();
		return new Vector3(-Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
	}

	// Hucrenin yuzunun disa bakan normali. Isaretler, gx'in artis yonu ile
	// ucgenin kendi +X ekseni ayni kalsin diye secildi; ters olsaydi butun
	// yonelimler aynalanir ve dik acinin yeri yanlis tarafa duserdi.
	private Vector3 FacetOut(int cx)
	{
		float a = (cx + 0.5f) / GridW * Mathf.PI * 2f;
		return new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
	}

	private Vector3 CornerToWorld(float gx, float gy)
	{
		if (!Prism)
			return new Vector3(gx - GridW * 0.5f, 0f, gy - GridH * 0.5f);

		int i = Mathf.FloorToInt(gx);
		return Vector3.Lerp(PrismCorner(i), PrismCorner(i + 1), gx - i) + Vector3.up * (gy - GridH * 0.5f);
	}

	private Vector3 StateToPos(int cx, int cy, int ori)
	{
		return CornerToWorld(cx + (ori & 1), cy + ((ori >> 1) & 1));
	}

	private Quaternion StateToRot(int cx, int ori)
	{
		var spin = Quaternion.Euler(0f, OriAngle[ori], 0f);
		if (!Prism)
			return spin;

		// Ucgen mesh'i XZ duzleminde ve normali +Y. Yuze yatirmak icin normali disari,
		// kendi +Z'sini de kulenin yukari yonune bakacak sekilde ceviriyoruz.
		return Quaternion.LookRotation(Vector3.up, FacetOut(cx)) * spin;
	}

	private int WrapX(int x)
	{
		return Prism ? (x % GridW + GridW) % GridW : x;
	}

	private bool In(int cx, int cy)
	{
		if (cy < 0 || cy >= GridH)
			return false;
		// Prizmada yatayda kenar yok, tahta kendine kapaniyor; sutun yine de
		// dizinin icinde kalmali, cagiran taraf WrapX ile sariyor.
		if (Prism)
			return cx >= 0 && cx < GridW;
		return cx >= rowLo[cy] && cx < rowHi[cy];
	}

	// --- hareket ---

	// Takla atilan kenar yerinde kalir, karsi kose kenarin obur tarafina gecer.
	// Ucu de tersine cevrilebilir (ayni kenardan ikinci takla basa dondurur),
	// yani ulasilabilirlik simetrik ve tikanma imkansiz.
	private void Target(int cx, int cy, int ori, int m, out int nx, out int ny, out int no)
	{
		if (m == Flip)
		{
			nx = cx;
			ny = cy;
			no = ori ^ 3;
			return;
		}

		int rx = cx + (ori & 1);
		int ry = cy + ((ori >> 1) & 1);
		int lx = (ori & 1) == 0 ? 1 : -1;
		int ly = ((ori >> 1) & 1) == 0 ? 1 : -1;

		if (m == 0)
			ly = -ly;
		else
			lx = -lx;

		nx = WrapX(lx > 0 ? rx : rx - 1);
		ny = ly > 0 ? ry : ry - 1;
		no = (lx < 0 ? 1 : 0) | (ly < 0 ? 2 : 0);
	}

	// Taklanin ekseni: uzerinden devrildigi kenar.
	// Eksenin isareti ucgen daima tahtanin USTUNDEN donsun diye seciliyor; yanlis
	// isarette zeminin altindan geciyor ve kareler arkasinda kaybolup bozuk gorunuyordu.
	private void Swing(int cx, int cy, int ori, int m, out Vector3 point, out Vector3 axis)
	{
		int rx = cx + (ori & 1);
		int ry = cy + ((ori >> 1) & 1);
		int lx = (ori & 1) == 0 ? 1 : -1;
		int ly = ((ori >> 1) & 1) == 0 ? 1 : -1;

		if (m == 0)
		{
			point = CornerToWorld(rx, ry);
			axis = (CornerToWorld(rx + lx, ry) - point).normalized * (lx * (ly > 0 ? -1f : 1f));
			return;
		}

		if (m == 1)
		{
			point = CornerToWorld(rx, ry);
			axis = (CornerToWorld(rx, ry + ly) - point).normalized * (ly * (lx > 0 ? 1f : -1f));
			return;
		}

		point = CornerToWorld(rx + lx, ry);
		axis = (CornerToWorld(rx, ry + ly) - point).normalized * (lx * ly > 0 ? -1f : 1f);
	}

	// -4 = engel ya da oyuncu, -3 = duvar, -2 = tahta disi, -1 = bos. >= 0 yenilecek ucgen.
	// Sadece kendi rengini yiyorsun. Diger renkler gercek engel: uzerine cikilmiyor,
	// patlatilmiyor. Tikanma bu yuzden yeniden mumkun; Z ile geri alinir.
	private bool Blocks(int i, int myCol)
	{
		if (i < 0)
			return false;
		return i == Me || i == rivalIdx || tris[i].Col != myCol;
	}

	// O yarim hucreye temiz inilebiliyor mu: kendi rengin ve bosluk sorun degil,
	// baska renk ya da oyuncu varsa hayir.
	private bool Clear(int nx, int ny, int no, int myCol)
	{
		return !Blocks(occ[nx, ny, no] - 1, myCol)
			&& !Blocks(occ[nx, ny, no ^ 1] - 1, myCol)
			&& !Blocks(occ[nx, ny, no ^ 2] - 1, myCol);
	}

	// Hamlenin inis yeri. Ucgen kenarinin uzerinden devriliyor, baska bir sey yok:
	// inis yarisi hamlenin kendisinden cikiyor, kaydirma ya da duzeltme yapilmiyor.
	private bool Landing(int cx, int cy, int ori, int m, int myCol, out int nx, out int ny, out int no)
	{
		Target(cx, cy, ori, m, out nx, out ny, out no);
		return In(nx, ny) && !Barred(cx, cy, ori, m) && Clear(nx, ny, no, myCol);
	}

	private int Legal(int cx, int cy, int ori, int m, int myCol = 0)
	{
		Target(cx, cy, ori, m, out int nx, out int ny, out int no);
		if (!In(nx, ny))
			return -2;
		if (Barred(cx, cy, ori, m))
			return -3;
		return Landing(cx, cy, ori, m, myCol, out _, out _, out _) ? 0 : -4;
	}

	// --- tahta kurulumu ---

	private void Build()
	{
		if (board != null)
			Destroy(board.gameObject);
		board = new GameObject("Board").transform;

		// Kalinlik ve pay slider'da; mesh tahtayla birlikte yeniden kuruluyor.
		if (triMesh != null)
			Destroy(triMesh);
		triMesh = BuildTriMesh(Inset, TriThick);

		// Kule zemini: asagidan yukari isinan sicak tas, komsu yuzler arasinda hafif
		// ton farki. Isik zaten yuzleri ayristiriyor, bu onu guclendiriyor; ayrica
		// ne kadar yukseldigini zeminin renginden de okuyorsun.
		if (matFloorRow != null)
			for (int i = 0; i < matFloorRow.Length; i++)
				Destroy(matFloorRow[i]);

		matFloorRow = new Material[GridH * 2];
		for (int y = 0; y < GridH; y++)
		{
			Color low = new Color(0.26f, 0.19f, 0.17f);
			Color high = new Color(0.56f, 0.43f, 0.34f);
			Color cc = Color.Lerp(low, high, y / Mathf.Max(1f, GridH - 1f));
			matFloorRow[y * 2] = MakeMat(cc);
			matFloorRow[y * 2 + 1] = MakeMat(new Color(cc.r * 0.84f, cc.g * 0.84f, cc.b * 0.84f));
		}

		board.SetParent(transform, false);

		history.Clear();
		popping.Clear();
		popT.Clear();
		steps = 0;
		solved = false;
		tumbling = false;
		queued = -1;
		introT = 0f;
		camBlend = 0f;

		BuildShape();
		BuildFloor();
		BuildFlag();

		var rnd = new System.Random(seed);
		BuildWalls(rnd);
		MakePlan(rnd, Density);

		tris = new Tri[plan.Count];
		for (int i = 0; i < plan.Count; i++)
		{
			var s = plan[i];
			var go = new GameObject(i == Me ? "Ben" : i == rivalIdx ? "Rakip" : planCol[i] == 0 ? "Mavi" : "Engel");
			go.transform.SetParent(board, false);
			go.transform.position = StateToPos(s.x, s.y, s.z);
			go.transform.rotation = StateToRot(s.x, s.z);
			go.AddComponent<MeshFilter>().sharedMesh = triMesh;
			var mr = go.AddComponent<MeshRenderer>();
			mr.sharedMaterial = i == Me ? matMe : i == rivalIdx ? matRival : planCol[i] == 0 ? matTarget : matObstacle[planCol[i] - 1];
			mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			mr.receiveShadows = false;
			tris[i] = new Tri { Cx = s.x, Cy = s.y, Ori = s.z, Col = planCol[i], T = go.transform };
		}

		hints = new Transform[MoveCount];
		for (int i = 0; i < MoveCount; i++)
		{
			var g = new GameObject("Yonerge");
			g.transform.SetParent(board, false);
			g.transform.localScale = Vector3.one * 0.42f;
			g.AddComponent<MeshFilter>().sharedMesh = triMesh;
			var gm = g.AddComponent<MeshRenderer>();
			gm.sharedMaterial = matHint;
			gm.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			gm.receiveShadows = false;
			g.SetActive(false);
			hints[i] = g.transform;
		}

		marks = new Transform[4];
		for (int i = 0; i < 4; i++)
		{
			var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
			Destroy(q.GetComponent<Collider>());
			q.name = "Menzil";
			q.transform.SetParent(board, false);
			q.transform.localScale = Vector3.one * 0.3f;
			q.GetComponent<MeshRenderer>().sharedMaterial = matHintTake;
			q.SetActive(false);
			marks[i] = q.transform;
		}

		// Baslangicta bir kaymalik menzil: ilk hamlede sikisip kalmak mumkun olmasin.
		charge = 2;
		budget = Mathf.RoundToInt(Mathf.Max(1f, BudgetPerRow) * GridH);
		budgetFlash = 0f;
		lostText = "";
		sliding = false;
		rivalTumbling = false;
		rivalWait = RivalDelay;
		lost = false;

		var start = tris[Me].T.position;
		focusZ = start.z;
		focusVel = 0f;
		focusX = start.x;
		focusVelX = 0f;
		focusY = start.y;
		camAng = Mathf.Atan2(-start.x, start.z) * Mathf.Rad2Deg;
		camAngVel = 0f;
		SetupCamera();
	}

	// Asagisi genis, yukarisi basamak basamak daralip tek sutunluk zirveye cikiyor.
	// Daralma zorluk egrisini siluetten veriyor: yukarida rota sayisi kendiliginden dusuyor.
	private void BuildShape()
	{
		rowLo = new int[GridH];
		rowHi = new int[GridH];
		cellCount = 0;

		// Prizma kendine kapandigi icin daralan siluet yok: her satir tam halka.
		// Kulenin govdesini 3B bicim veriyor, satir maskesi degil.
		if (Prism)
		{
			for (int y = 0; y < GridH; y++)
			{
				rowLo[y] = 0;
				rowHi[y] = GridW;
			}
			cellCount = GridW * GridH;
			goalY = GridH - 1;
			goalX = GridW / 2;
			return;
		}

		int shoulder = Mathf.Clamp(Mathf.RoundToInt(GridH * Shoulder), 1, GridH - 2);
		for (int y = 1; y < GridH; y++)
		{
			int w = GridW;
			if (y > shoulder)
				w = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(GridW, 1f, (y - shoulder) / (float)(GridH - 1 - shoulder))));
			rowLo[y] = (GridW - w) / 2;
			rowHi[y] = rowLo[y] + w;
			cellCount += w;
		}

		// Satir 0 kuleye ait degil: dipte, ortada, tek hucrelik baslangic pedi.
		// Yanlari tahta disi oldugu icin pedden cikisin tek yonu var, yukari.
		rowLo[0] = (GridW - 1) / 2;
		rowHi[0] = rowLo[0] + 1;
		cellCount += 1;

		goalY = GridH - 1;
		goalX = (rowLo[goalY] + rowHi[goalY] - 1) / 2;
	}

	// Duvarlar sadece YATAY cizgilerde durur ve hucre kaplamaz: iki satir arasindaki
	// gecisi keser. Bir cizgideki gecilebilir sutunlarin hepsi birden kapanmaz,
	// her cizgide en az bir gedik kalir. Zirveye giden yol boylece hep vardir;
	// tikanma uretim sirasinda sinanmadan, kuralin kendisinden garanti.
	private void BuildWalls(System.Random rnd)
	{
		wallH = new bool[GridW, GridH + 1];

		for (int y = 1; y < GridH; y++)
		{
			int lo = Mathf.Max(rowLo[y - 1], rowLo[y]);
			int hi = Mathf.Min(rowHi[y - 1], rowHi[y]);
			int span = hi - lo;
			if (span < 2 || rnd.NextDouble() >= WallChance)
				continue;

			int len = 1 + rnd.Next(0, Mathf.Min(span - 1, Mathf.Max(1, Mathf.RoundToInt(WallMax))));
			// Prizmada duvar dikisin uzerinden de gecebilmeli, yoksa 0. sutun
			// hic duvar gormez ve surekli acik bir otoyol olur.
			int at = Prism ? rnd.Next(0, GridW) : lo + rnd.Next(0, span - len + 1);
			for (int k = 0; k < len; k++)
				wallH[WrapX(at + k), y] = true;

			// Ucgenlerin arasindaki pay kadar kalin, ucgen duzleminin uzerinde:
			// altina koyarsak ustundeki ucgenler duvari gizler.
			if (Prism)
			{
				for (int k = 0; k < len; k++)
				{
					int x = WrapX(at + k);
					FacetQuad("Duvar", matWall, x, x + 0.5f, y, 1f, Inset * 1.7f, 0.01f);
				}
				continue;
			}

			var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
			Destroy(q.GetComponent<Collider>());
			q.name = "Duvar";
			q.transform.SetParent(board, false);
			q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			q.transform.position = (CornerToWorld(at, y) + CornerToWorld(at + len, y)) * 0.5f + Vector3.up * 0.01f;
			q.transform.localScale = new Vector3(len, Inset * 1.7f, 1f);
			q.GetComponent<MeshRenderer>().sharedMaterial = matWall;
		}
	}

	// Prizmada her parca kendi yuzunun uzerine, yuzun normaline dik yatiriliyor.
	// Yuz duz oldugu icin tek bir quad yetiyor, egrilik yok.
	private void FacetQuad(string name, Material mat, int cx, float gx, float gy, float w, float h, float lift)
	{
		var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
		Destroy(q.GetComponent<Collider>());
		q.name = name;
		q.transform.SetParent(board, false);
		Vector3 outw = FacetOut(cx);
		q.transform.position = CornerToWorld(gx, gy) + outw * lift;
		q.transform.rotation = Quaternion.LookRotation(-outw, Vector3.up);
		q.transform.localScale = new Vector3(w, h, 1f);
		q.GetComponent<MeshRenderer>().sharedMaterial = mat;
	}

	// Duvara yalnizca satir degistiren takla carpar; ayni satirda kalan hamleler serbest.
	// Yuvarlanmanin iki adimi da ayri ayri sinanir, duvarin ustunden atlanamaz.
	private bool Barred(int cx, int cy, int ori, int m)
	{
		if (m != 0)
			return false;

		Target(cx, cy, ori, m, out int nx, out int ny, out int _);
		return wallH[cx, Mathf.Max(cy, ny)];
	}

	// Mavi artik 5 tane nadir odul degil, tahtanin malzemesi: her engel kendi rengini
	// BlueRatio olasiligiyla mavi olarak cekiyor. Hedef "su 5 ucgen" degil, "su kadar mavi".
	// Yakit da ayni mavilerden geldigi icin toplamak ve ilerlemek ayni ise hizmet ediyor.
	// Cozulebilirlik sinamasina gerek yok: her ucgen yenilebilir, her tahta oynanir.
	private void MakePlan(System.Random rnd, float density)
	{
		plan.Clear();
		planCol.Clear();
		occ = new int[GridW, GridH, 4];

		// Oyuncu pedin uzerinde.
		Put(rnd.Next(rowLo[0], rowHi[0]), 0, rnd.Next(0, 4), 0);

		// Rakip kulenin tam karsi yuzunde basliyor: ilk anda gorunmuyor,
		// kivrimin arkasindan donerek ortaya cikiyor.
		rivalIdx = -1;
		if (Rival && Prism)
		{
			int at = plan.Count;
			if (Put(WrapX(plan[0].x + GridW / 2), 0, rnd.Next(0, 4), -1))
				rivalIdx = at;
		}

		for (int o = 0; o < 4; o++)
			occ[goalX, goalY, o] = -1;

		// Hucre ya bos ya da iki yarisi da dolu; iki yarinin rengi ayri ayri cekiliyor.
		int cells = Mathf.RoundToInt(cellCount * density);
		int made = 0;
		for (int i = 0; i < cells * 25 && made < cells; i++)
		{
			int cy = rnd.Next(1, GridH);
			int cx = rnd.Next(rowLo[cy], rowHi[cy]);
			int ori = rnd.Next(0, 4);
			if (!Put(cx, cy, ori, Roll(rnd)))
				continue;
			if (rnd.NextDouble() >= HalfRatio)
				Put(cx, cy, ori ^ 3, Roll(rnd));
			made++;
		}

	// Pedin hemen ustunde, oyuncunun sutununda ve iki yaninda mavi garanti.
		// Ilk hamlede yakit bulamamak kotu baslangic; ama dar tutuluyor, yoksa hedef
		// ilk iki satirda biter ve tirmanisin anlami kalmaz.
		int startX = plan[0].x;
		for (int i = 1; i < plan.Count; i++)
		{
			int d = Mathf.Abs(plan[i].x - startX);
			if (Prism)
				d = Mathf.Min(d, GridW - d);
			if (plan[i].y == 1 && d <= 1)
				planCol[i] = 0;
		}

		for (int o = 0; o < 4; o++)
			occ[goalX, goalY, o] = 0;
	}

	private int Roll(System.Random rnd)
	{
		return rnd.NextDouble() < BlueRatio ? 0 : 1 + rnd.Next(0, Palette.Length);
	}

	private bool Put(int cx, int cy, int ori, int col)
	{
		if (!Free(cx, cy, ori))
			return false;
		plan.Add(new Vector3Int(cx, cy, ori));
		planCol.Add(col);
		occ[cx, cy, ori] = plan.Count;
		return true;
	}

	private bool Free(int cx, int cy, int ori)
	{
		if (!In(cx, cy))
			return false;
		return occ[cx, cy, ori] == 0 && occ[cx, cy, ori ^ 1] == 0 && occ[cx, cy, ori ^ 2] == 0;
	}

	private void BuildFloor()
	{
		// Prizmada her hucre kendi yuzunun bir parcasi, satir boyu tek quad olmuyor.
		// Dikey cizgi de cizmiyoruz: her yuz tam bir hucre genisliginde oldugu icin
		// sutun ayrimini prizmanin kendi kenar kirigi zaten gosteriyor.
		if (Prism)
		{
			for (int y = 0; y < GridH; y++)
				for (int x = 0; x < GridW; x++)
				{
					FacetQuad("Zemin", matFloorRow[y * 2 + (x & 1)], x, x + 0.5f, y + 0.5f, 1f, 1f, -0.06f);
					FacetQuad("Cizgi", matLine, x, x + 0.5f, y, 1f, 0.03f, -0.04f);
				}

			for (int x = 0; x < GridW; x++)
				FacetQuad("Cizgi", matLine, x, x + 0.5f, GridH, 1f, 0.03f, -0.04f);
			return;
		}

		for (int y = 0; y < GridH; y++)
		{
			var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
			Destroy(q.GetComponent<Collider>());
			q.name = "Zemin";
			q.transform.SetParent(board, false);
			q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			q.transform.position = (CornerToWorld(rowLo[y], y) + CornerToWorld(rowHi[y], y + 1)) * 0.5f + Vector3.down * 0.06f;
			q.transform.localScale = new Vector3(rowHi[y] - rowLo[y], 1f, 1f);
			q.GetComponent<MeshRenderer>().sharedMaterial = y == 0 ? matPad : matFloorRow[y * 2];
		}

		// Cizgiler siluetin icinde kalsin: her kenar dizisi tek parca cizilip disarida
		// hic cizgi birakilmiyor, yoksa kule dikdortgen gibi gorunuyor.
		for (int y = 0; y <= GridH; y++)
		{
			int lo = -1;
			for (int x = 0; x <= GridW; x++)
			{
				bool on = x < GridW && (In(x, y) || In(x, y - 1));
				if (on && lo < 0)
					lo = x;
				if (!on && lo >= 0)
				{
					Line(CornerToWorld(lo, y), CornerToWorld(x, y));
					lo = -1;
				}
			}
		}

		for (int x = 0; x <= GridW; x++)
		{
			int lo = -1;
			for (int y = 0; y <= GridH; y++)
			{
				bool on = y < GridH && (In(x, y) || In(x - 1, y));
				if (on && lo < 0)
					lo = y;
				if (!on && lo >= 0)
				{
					Line(CornerToWorld(x, lo), CornerToWorld(x, y));
					lo = -1;
				}
			}
		}
	}

	private void Line(Vector3 a, Vector3 b)
	{
		var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
		Destroy(q.GetComponent<Collider>());
		q.name = "Cizgi";
		q.transform.SetParent(board, false);
		q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		q.transform.position = (a + b) * 0.5f + Vector3.down * 0.04f;
		float len = Vector3.Distance(a, b);
		bool horiz = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
		q.transform.localScale = horiz ? new Vector3(len, 0.03f, 1f) : new Vector3(0.03f, len, 1f);
		q.GetComponent<MeshRenderer>().sharedMaterial = matLine;
	}

	// Zirvenin ustundeki direk. Bayrak renkler bitene kadar sonuk, sonra altin.
	private void BuildFlag()
	{
		Vector3 foot = CornerToWorld(goalX + 0.5f, goalY + 1);
		Vector3 tip = Prism ? foot + Vector3.up * PoleLen : foot + Vector3.forward * PoleLen;

		if (Prism)
			FacetQuad("Direk", matLine, goalX, goalX + 0.5f, goalY + 1 + PoleLen * 0.5f, 0.08f, PoleLen, -0.04f);
		else
		{
			var pole = GameObject.CreatePrimitive(PrimitiveType.Quad);
			Destroy(pole.GetComponent<Collider>());
			pole.name = "Direk";
			pole.transform.SetParent(board, false);
			pole.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			pole.transform.position = (foot + tip) * 0.5f + Vector3.down * 0.04f;
			pole.transform.localScale = new Vector3(0.08f, PoleLen, 1f);
			pole.GetComponent<MeshRenderer>().sharedMaterial = matLine;
		}

		var go = new GameObject("Bayrak");
		go.transform.SetParent(board, false);
		go.AddComponent<MeshFilter>().sharedMesh = triMesh;
		var mr = go.AddComponent<MeshRenderer>();
		mr.sharedMaterial = matHintTake;
		mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		mr.receiveShadows = false;
		go.transform.position = Prism
			? CornerToWorld(goalX + 0.5f, goalY + 1 + PoleLen - 0.85f) + FacetOut(goalX) * 0.004f
			: tip + new Vector3(0f, 0.004f, -0.85f);
		go.transform.rotation = StateToRot(goalX, 0);
		go.transform.localScale = Vector3.one * 0.85f;
		flag = go.transform;
		flagMr = mr;
	}

	// --- kamera ---

	// Acilista kule ve bayrak tamamen kadrajda, hafif perspektifle.
	// Yakin planda kamera duzlesip tahtanin uzerine dikleniyor ve oyuncuyu takip ediyor;
	// kenarlar kadraj disina tassa da kamera pesinden gittigi icin kaybolmuyorsun.
	private static void CamBasis(float angle, out Vector3 dir, out Vector3 up)
	{
		float rad = angle * Mathf.Deg2Rad;
		dir = new Vector3(0f, Mathf.Sin(rad), -Mathf.Cos(rad));
		up = new Vector3(0f, Mathf.Cos(rad), Mathf.Sin(rad));
	}

	private void SetupCamera()
	{
		camScreen = new Vector2Int(Screen.width, Screen.height);

		// Prizmada kadraj kule boyunu tutacak kadar geri cekiliyor; genislik zaten
		// yaricapla sinirli oldugu icin kose sinamasina gerek yok.
		if (Prism)
		{
			cam.fieldOfView = WideFov;
			camWideDist = PrismRadius() + (GridH * 0.5f + PoleLen + 1f) / Mathf.Tan(WideFov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.2f, CamZoom);
			camCloseDist = PrismRadius() + PrismDist / Mathf.Max(0.3f, CloseZoom);
			return;
		}

		float extra = PoleLen + 0.4f;
		camWideDist = FitDistance(GridW, GridH + extra, new Vector3(0f, 0f, extra * 0.5f), CamAngle, WideFov);
		camCloseDist = FitDistance(GridW, Mathf.Min(GridH, CloseRows), Vector3.zero, CloseAngle, CloseFov) / Mathf.Max(0.3f, CloseZoom);
	}

	// Perspektifte yakin kose uzaktakinden buyuk cikar, bu yuzden olcup duzeltiyoruz.
	private float FitDistance(float cols, float rows, Vector3 focus, float angle, float fov)
	{
		CamBasis(angle, out Vector3 dir, out Vector3 up);

		cam.fieldOfView = fov;
		float halfV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
		float halfH = halfV * cam.aspect;
		float d = Mathf.Max(cols * 0.5f / halfH, rows * 0.5f * Mathf.Sin(angle * Mathf.Deg2Rad) / halfV) / Mathf.Max(0.2f, CamZoom);

		for (int i = 0; i < 16; i++)
		{
			cam.transform.position = focus + dir * d;
			cam.transform.rotation = Quaternion.LookRotation(-dir, up);

			float worst = 0f;
			for (int c = 0; c < 4; c++)
			{
				var corner = focus + new Vector3((c & 1) == 0 ? -cols * 0.5f : cols * 0.5f, 0f, (c & 2) == 0 ? -rows * 0.5f : rows * 0.5f);
				var v = cam.WorldToViewportPoint(corner);
				worst = Mathf.Max(worst, Mathf.Abs(v.x - 0.5f) * 2f, Mathf.Abs(v.y - 0.5f) * 2f);
			}

			if (worst <= 0.93f)
				break;
			d *= worst / 0.93f;
		}
		return d;
	}

	private void UpdateCamera()
	{
		introT += Time.deltaTime;
		camBlend = Mathf.MoveTowards(camBlend, introT < IntroHold ? 0f : 1f, Time.deltaTime / IntroTime);

		if (Prism)
		{
			UpdatePrismCamera();
			return;
		}

		// Kamerayi tahtanin kendisine kilitliyoruz: kenarlar kadraj disina tasabilir
		// ama tahtanin otesindeki bos alan gosterilmez, oyuncu ekranin dibinden baslar.
		float halfV = Mathf.Tan(CloseFov * 0.5f * Mathf.Deg2Rad);
		float visZ = camCloseDist * halfV / Mathf.Max(0.2f, Mathf.Sin(CloseAngle * Mathf.Deg2Rad));
		float visX = camCloseDist * halfV * cam.aspect;

		float loZ = -GridH * 0.5f + visZ - 0.5f;
		float hiZ = GridH * 0.5f + PoleLen + 0.3f - visZ;
		float loX = -GridW * 0.5f + visX;
		float hiX = GridW * 0.5f - visX;

		var at = tris[Me].T.position;
		focusX = Mathf.SmoothDamp(focusX, hiX > loX ? Mathf.Clamp(at.x, loX, hiX) : 0f, ref focusVelX, 0.18f);
		focusZ = Mathf.SmoothDamp(focusZ, hiZ > loZ ? Mathf.Clamp(at.z, loZ, hiZ) : (loZ + hiZ) * 0.5f, ref focusVel, 0.18f);

		float e = camBlend * camBlend * (3f - 2f * camBlend);
		float extra = PoleLen + 0.4f;
		Vector3 focus = Vector3.Lerp(new Vector3(0f, 0f, extra * 0.5f), new Vector3(focusX, 0f, focusZ), e);

		cam.fieldOfView = Mathf.Lerp(WideFov, CloseFov, e);
		CamBasis(Mathf.Lerp(CamAngle, CloseAngle, e), out Vector3 dir, out Vector3 up);
		cam.transform.position = focus + dir * Mathf.Lerp(camWideDist, camCloseDist, e);
		cam.transform.rotation = Quaternion.LookRotation(-dir, up);
	}


	// Nereye devrilebildigini gosteren soluk ucgenler. Yarim hucre sistemi yuzunden
	// ulasilabilirlik yonelime bagli ve bu kural ekranda hicbir yerde gorunmuyordu:
	// yanindaki maviyi neden alamadigini bilmemek en bastaki sorun.
	// Her isaret hedef yarinin dik acisina oturuyor, yani inisin yonelimini de gosteriyor.
	private void UpdateHints()
	{
		if (hints == null)
			return;

		bool live = Hints && !tumbling && !sliding && !solved && !lost;
		int cx = tris[Me].Cx;
		int cy = tris[Me].Cy;
		int ori = tris[Me].Ori;

		for (int m = 0; m < MoveCount; m++)
		{
			int nx = 0;
			int ny = 0;
			int no = 0;
			bool ok = live && Landing(cx, cy, ori, m, 0, out nx, out ny, out no);
			hints[m].gameObject.SetActive(ok);
			if (!ok)
				continue;

			int t = occ[nx, ny, no] - 1;
			hints[m].GetComponent<MeshRenderer>().sharedMaterial = t > 0 ? matHintTake : matHint;
			hints[m].position = StateToPos(nx, ny, no) + (Prism ? FacetOut(nx) : Vector3.up) * 0.015f;
			hints[m].rotation = StateToRot(nx, no);
		}
	}

	// Menzilin dort yonde nereye vardigini hesaplar ve gosterir. Kaymanin kendi
	// yuruyusunu kullaniyor, yani isaret her zaman gercegi soyluyor: duvara
	// carpiyorsa duvarin onunde, kenara geliyorsa orada duruyor.
	private void UpdateMarks()
	{
		if (marks == null)
			return;

		bool live = charge >= 2 && !tumbling && !sliding && !solved && !lost;
		int cx = tris[Me].Cx;
		int cy = tris[Me].Cy;

		for (int d = 0; d < 4; d++)
		{
			int px = cx;
			int py = cy;
			markGone[d] = live ? Walk(cx, cy, DirX[d], DirY[d], charge, false, out px, out py) : 0;
			markCx[d] = px;
			markCy[d] = py;

			marks[d].gameObject.SetActive(Marks && markGone[d] >= 2);
			if (markGone[d] < 2)
				continue;

			// Yolda kiracagi bir sey varsa isaret turuncu: o kayma sadece ulasim degil,
			// yikim. Hangi yonun yol actigini basmadan once goruyorsun.
			bool breaks = false;
			int bx = cx;
			int by = cy;
			for (int k = 1; k <= markGone[d] && !breaks; k++)
			{
				bx = WrapX(bx + DirX[d]);
				by += DirY[d];
				for (int o = 0; o < 4 && !breaks; o++)
				{
					int t = occ[bx, by, o] - 1;
					breaks = t > 0 && t != rivalIdx && tris[t].Col != 0;
				}
			}
			marks[d].GetComponent<MeshRenderer>().sharedMaterial = breaks ? matBreak : matHintTake;

			Vector3 outw = Prism ? FacetOut(px) : Vector3.up;
			marks[d].position = CornerToWorld(px + 0.5f, py + 0.5f) + outw * 0.02f;
			marks[d].rotation = Prism ? Quaternion.LookRotation(-outw, Vector3.up) : Quaternion.Euler(90f, 0f, 0f);
		}
	}

	// Diger renkler gecilmez oldugu icin sikismak mumkun: ne gidilecek bir yarim hucre
	// ne de yol acacak menzil kalmissa oyuncuya soyluyoruz. Oyun kilitlenmiyor,
	// Z ile geri alinip baska bir rota denenebiliyor.
	private bool Stuck()
	{
		if (marks == null)
			return false;

		for (int d = 0; d < 4; d++)
			if (markGone[d] >= 2)
				return false;

		int cx = tris[Me].Cx;
		int cy = tris[Me].Cy;
		int ori = tris[Me].Ori;
		for (int m = 0; m < MoveCount; m++)
			if (Legal(cx, cy, ori, m) >= -1)
				return false;

		return true;
	}

	// Kule kamerasi: oyuncunun bulundugu yuzun acisini takip edip kuleyi dolaniyor.
	// Aci farki en kisa yoldan kapatiliyor, yoksa son sutundan ilk sutuna gecerken
	// kamera butun kulenin etrafini dolasip geri geliyor.
	private void UpdatePrismCamera()
	{
		var at = tris[Me].T.position;
		camAng = Mathf.SmoothDampAngle(camAng, Mathf.Atan2(-at.x, at.z) * Mathf.Rad2Deg, ref camAngVel, 0.22f);
		focusY = Mathf.SmoothDamp(focusY, at.y, ref focusVel, 0.18f);

		float e = camBlend * camBlend * (3f - 2f * camBlend);
		float pitch = Mathf.Lerp(CamAngle * 0.45f, PrismPitch, e) * Mathf.Deg2Rad;
		float a = camAng * Mathf.Deg2Rad;

		Vector3 outw = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
		Vector3 dir = outw * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
		Vector3 focus = Vector3.up * Mathf.Lerp(0f, focusY, e);

		cam.fieldOfView = Mathf.Lerp(WideFov, CloseFov, e);
		cam.transform.position = focus + dir * Mathf.Lerp(camWideDist, camCloseDist, e);
		cam.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
	}
	// Ekran dokunusunu surekli izgara koordinatina cevirir. Duz tahtada zemin
	// duzlemiyle, prizmada kuleyi cevreleyen silindirle kesisim alinir.
	private bool PickGrid(Vector3 screen, out float gx, out float gy)
	{
		gx = 0f;
		gy = 0f;
		var ray = cam.ScreenPointToRay(screen);

		if (!Prism)
		{
			var plane = new Plane(Vector3.up, Vector3.zero);
			if (!plane.Raycast(ray, out float dist))
				return false;

			Vector3 flat = ray.GetPoint(dist);
			gx = flat.x + GridW * 0.5f;
			gy = flat.z + GridH * 0.5f;
			return true;
		}

		float r = PrismRadius();
		Vector2 o = new Vector2(ray.origin.x, ray.origin.z);
		Vector2 d = new Vector2(ray.direction.x, ray.direction.z);
		float qa = Vector2.Dot(d, d);
		float qb = 2f * Vector2.Dot(o, d);
		float disc = qb * qb - 4f * qa * (Vector2.Dot(o, o) - r * r);
		if (qa < 1e-5f || disc < 0f)
			return false;

		// Yakin kok kulenin bize bakan yuzu; uzak kok arka yuzu verirdi.
		float t = (-qb - Mathf.Sqrt(disc)) / (2f * qa);
		if (t < 0f)
			return false;

		Vector3 hit = ray.GetPoint(t);
		gx = Mathf.Atan2(-hit.x, hit.z) * Mathf.Rad2Deg / 360f * GridW;
		if (gx < 0f)
			gx += GridW;
		gy = hit.y + GridH * 0.5f;
		return true;
	}

	private bool PickCell(Vector3 screen, out int cell, out int row)
	{
		cell = 0;
		row = 0;
		if (!PickGrid(screen, out float gx, out float gy))
			return false;

		cell = WrapX(Mathf.FloorToInt(gx));
		row = Mathf.FloorToInt(gy);
		return true;
	}

	private bool PickFine(Vector3 screen, out int cell, out int row, out float fx, out float fy)
	{
		cell = 0;
		row = 0;
		fx = 0f;
		fy = 0f;
		if (!PickGrid(screen, out float gx, out float gy))
			return false;

		cell = WrapX(Mathf.FloorToInt(gx));
		row = Mathf.FloorToInt(gy);
		fx = gx - Mathf.Floor(gx);
		fy = gy - Mathf.Floor(gy);
		return true;
	}


	// --- girdi ---

	private void Update()
	{
		if (camScreen.x != Screen.width || camScreen.y != Screen.height)
			SetupCamera();

		// Acilis gosterimi bekletmesin: ilk dokunusta kamera iceri girer.
		if (camBlend < 1f && Input.anyKeyDown)
			introT = Mathf.Max(introT, IntroHold);

		StepPops();
		StepShards();
		StepWin();
		UpdateCamera();
		UpdateMarks();
		UpdateHints();
		stuck = !tumbling && !sliding && !solved && !lost && Stuck();

		// Rakip oyuncu havadayken de ilerliyor: yarisin surekli olmasi icin
		// kendi saati var, oyuncunun hamle sirasina bagli degil.
		if (rivalIdx > 0 && !solved && !lost)
			RivalStep();

		// Butce bitti. Hamle havadayken dusup kaybetmeyelim diye yere indikten sonra bakiyoruz.
		if (budget <= 0 && !tumbling && !sliding && !solved && !lost)
		{
			lost = true;
			lostText = "ADIMIN BITTI";
			solvedAt = Time.time;
		}

		budgetFlash = Mathf.MoveTowards(budgetFlash, 0f, Time.deltaTime * 2.5f);

		if (lost)
		{
			if (Time.time - solvedAt > 2.2f)
			{
				seed++;
				Build();
			}
			return;
		}

		if (sliding)
		{
			StepSlide();
			return;
		}

		int m = ReadMove();

		if (tumbling)
		{
			if (m >= 0)
				queued = m;
			StepTumble();
			return;
		}

		if (solved)
		{
			if (Time.time - solvedAt > 2.2f)
			{
				seed++;
				SavePrefs();
				Build();
			}
			return;
		}

		if (Input.GetKeyDown(KeyCode.Z))
		{
			Undo();
			return;
		}
		if (Input.GetKeyDown(KeyCode.R))
		{
			seed++;
			Build();
			return;
		}

		if (m >= 0)
			TryMove(m);
	}

	private int ReadMove()
	{
		int key = KeyMove(false);
		if (key >= 0)
			return key;

		if (showPanel && Input.mousePosition.x > panelLeft)
			return -1;

		if (Input.GetMouseButtonDown(0))
		{
			pressPos = Input.mousePosition;
			pressed = true;
			return -1;
		}
		if (!pressed || !Input.GetMouseButtonUp(0))
			return -1;

		pressed = false;
		Vector2 delta = (Vector2)Input.mousePosition - pressPos;
		float scale = Mathf.Max(Screen.width, Screen.height) / 900f;
		if (delta.magnitude > SwipeMin * scale)
			return Steer(Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? (delta.x > 0f ? 0 : 1) : (delta.y > 0f ? 2 : 3));

		// Once menzil atisi denenir: tam charge kare uzaktaki kareye basildiysa kayma olur.
		if (TapSlide(Input.mousePosition))
			return -1;

		return TapMove(Input.mousePosition);
	}

	// held = tus basili tutulurken devam etsin diye.
	private int KeyMove(bool held)
	{
		// Fare eldeyken ok tuslari uzak kaliyor; sol el icin A S D E ayni islerde.
		if (held ? Input.GetKey(KeyCode.RightArrow) : Input.GetKeyDown(KeyCode.RightArrow)) return Steer(0);
		if (held ? Input.GetKey(KeyCode.LeftArrow) : Input.GetKeyDown(KeyCode.LeftArrow)) return Steer(1);
		if (held ? Input.GetKey(KeyCode.UpArrow) : Input.GetKeyDown(KeyCode.UpArrow)) return Steer(2);
		if (held ? Input.GetKey(KeyCode.DownArrow) : Input.GetKeyDown(KeyCode.DownArrow)) return Steer(3);
		if (held ? Input.GetKey(KeyCode.D) : Input.GetKeyDown(KeyCode.D)) return Steer(0);
		if (held ? Input.GetKey(KeyCode.A) : Input.GetKeyDown(KeyCode.A)) return Steer(1);
		if (held ? Input.GetKey(KeyCode.E) : Input.GetKeyDown(KeyCode.E)) return Steer(2);
		if (held ? Input.GetKey(KeyCode.S) : Input.GetKeyDown(KeyCode.S)) return Steer(3);
		if (held ? Input.GetKey(KeyCode.W) : Input.GetKeyDown(KeyCode.W)) return Flip;
		if (held ? Input.GetKey(KeyCode.Space) : Input.GetKeyDown(KeyCode.Space)) return Flip;
		return -1;
	}

	private int Steer(int dir)
	{
		lastDir = dir;
		int cx = tumbling ? pendingCx : tris[Me].Cx;
		int cy = tumbling ? pendingCy : tris[Me].Cy;
		int ori = tumbling ? pendingOri : tris[Me].Ori;
		int wx = WrapX(cx + DirX[dir]);
		int wy = cy + DirY[dir];

		// Iki bacaktan hangisi o yone goturuyorsa o. Goturen yoksa hamle yok:
		// ucgenin o anda nereye devrilebildigini yonerge ucgenleri gosteriyor.
		for (int m = 0; m < 2; m++)
		{
			Target(cx, cy, ori, m, out int nx, out int ny, out int _);
			if (nx == wx && ny == wy && Legal(cx, cy, ori, m) >= -1)
				return m;
		}
		return -1;
	}

	// Ipucu ucgenlerinden birine dokunmak oraya goturur.
	private int TapMove(Vector3 screen)
	{
		if (!PickFine(screen, out int cell, out int row, out float fx, out float fy))
			return -1;

		int cx = tumbling ? pendingCx : tris[Me].Cx;
		int cy = tumbling ? pendingCy : tris[Me].Cy;
		int ori = tumbling ? pendingOri : tris[Me].Ori;

		for (int m = 0; m < MoveCount; m++)
		{
			if (!Landing(cx, cy, ori, m, 0, out int nx, out int ny, out int no))
				continue;
			if (nx != cell || ny != row || !Inside(no, fx, fy))
				continue;

			// Zincir gercekten gidilen yonde devam etsin. Dokunarak oynarken lastDir
			// eski bir ok tusundan kaliyordu ve ucgen secmedigin yone gidiyordu.
			lastDir = -1;
			for (int d = 0; d < 4; d++)
				if (nx == WrapX(cx + DirX[d]) && ny == cy + DirY[d])
					lastDir = d;
			return m;
		}

		// Yarinin tam uzerine degil de karenin herhangi bir yerine dokunulduysa:
		// o kareye goturen bir kenar varsa onu kullan.
		for (int m = 0; m < MoveCount; m++)
		{
			if (!Landing(cx, cy, ori, m, 0, out int nx, out int ny, out int _))
				continue;
			if (nx == cell && ny == row)
				return m;
		}
		return -1;
	}

	private static bool Inside(int ori, float fx, float fy)
	{
		switch (ori)
		{
			case 0: return fx + fy < 1f;
			case 3: return fx + fy > 1f;
			case 1: return fy < fx;
			default: return fy > fx;
		}
	}

	private void TryMove(int m)
	{
		int cx = tris[Me].Cx;
		int cy = tris[Me].Cy;
		int ori = tris[Me].Ori;
		if (Legal(cx, cy, ori, m) < -1)
			return;

		// Inis yeri Landing ile: dogal yari kapaliysa kendi rengimizin yarisina oturuyor.
		if (!Landing(cx, cy, ori, m, 0, out pendingCx, out pendingCy, out pendingOri))
			return;

		// Inis yerini fiziksel olarak kapatan her ucgen yenir: ya tek yarim,
		// ya da kareyi ters yonde bolen karsi capraz ikisi.
		takeA = occ[pendingCx, pendingCy, pendingOri] - 1;
		takeB = -1;
		if (takeA < 0)
		{
			takeA = occ[pendingCx, pendingCy, pendingOri ^ 1] - 1;
			takeB = occ[pendingCx, pendingCy, pendingOri ^ 2] - 1;
		}

		// Her hamle bir kenarin uzerinden tek takla: bedeli de hep bir.
		int cost = 1;

		occ[cx, cy, ori] = 0;
		history.Add(new Move { Cx = cx, Cy = cy, Ori = ori, A = takeA, B = takeB, Cost = cost });

		tumbleLift = HopHeight;
		StartTumble(cx, cy, ori, m);
		steps += cost;
		budget -= cost;
	}

	private void Swallow(int i)
	{
		if (i <= 0)
			return;

		occ[tris[i].Cx, tris[i].Cy, tris[i].Ori] = 0;
		popping.Add(i);
		popT.Add(0f);

		// Maviye YURUYORSUN: kucuk, yumusak. Diger rengi EZIYORSUN: buyuk, sert.
		// Iki eylem elde farkli hissedilsin diye patlamalari da ayri.
		bool mine = tris[i].Col == 0;
		Burst(
			tris[i].T.position,
			Prism ? FacetOut(tris[i].Cx) : Vector3.up,
			tris[i].T.GetComponent<MeshRenderer>().sharedMaterial,
			mine ? 4 : 9,
			mine ? 2.4f : 6.2f);

		if (!mine)
			return;
		charge++;
		budget += Mathf.RoundToInt(BlueRefund);
		budgetFlash = 1f;
	}

	private void Revive(int i)
	{
		if (i <= 0)
			return;

		int at = popping.IndexOf(i);
		if (at >= 0)
		{
			popping.RemoveAt(at);
			popT.RemoveAt(at);
		}
		tris[i].T.localScale = Vector3.one;
		tris[i].T.gameObject.SetActive(true);
		occ[tris[i].Cx, tris[i].Cy, tris[i].Ori] = i + 1;

		if (tris[i].Col != 0)
			return;
		charge--;
		budget -= Mathf.RoundToInt(BlueRefund);
		solved = false;
	}

	private void StartTumble(int cx, int cy, int ori, int m)
	{
		Swing(cx, cy, ori, m, out tumblePivot, out tumbleAxis);
		tumbleFromPos = tris[Me].T.position;
		tumbleFromRot = tris[Me].T.rotation;
		// Prizmada komsu yuze gecerken tahta duz degil: iki yuz arasindaki aci kadar
		// eksik donuyoruz, yoksa ucgen inecegi yuzun icine gomuluyor.
		tumbleAngle = Prism && m == 1 ? 180f - 360f / GridW : 180f;
		tumbleUp = Prism ? FacetOut(cx) : Vector3.up;
		tumbleT = 0f;
		tumbling = true;
	}

	private void StepTumble()
	{
		tumbleT += Time.deltaTime / Mathf.Max(0.02f, TumbleTime);
		float t = Mathf.Clamp01(tumbleT);
		float e = t * t * (3f - 2f * t);

		// Kenarin uzerinden devrilirken hafif bir yay: ucgen kendi kalinligi yuzunden
		// zemine surtmesin diye.
		float lift = Mathf.Sin(e * Mathf.PI);

		var rot = Quaternion.AngleAxis(tumbleAngle * e, tumbleAxis);
		var tr = tris[Me].T;
		tr.position = tumblePivot + rot * (tumbleFromPos - tumblePivot) + tumbleUp * (lift * tumbleLift);
		tr.rotation = rot * tumbleFromRot;

		if (t < 1f)
			return;

		tumbling = false;
		tris[Me].Cx = pendingCx;
		tris[Me].Cy = pendingCy;
		tris[Me].Ori = pendingOri;
		tr.position = StateToPos(pendingCx, pendingCy, pendingOri);
		tr.rotation = StateToRot(pendingCx, pendingOri);

		bool ate = takeA > 0 && tris[takeA].Col == 0 || takeB > 0 && tris[takeB].Col == 0;
		Swallow(takeA);
		Swallow(takeB);

		// Once yenenler temizlenir, sonra oyuncu yerlesir: ters sirada yenen ucgenin
		// occ silinisi oyuncunun kendi kaydini da siliyordu.
		occ[pendingCx, pendingCy, pendingOri] = Me + 1;

		if (pendingCx == goalX && pendingCy == goalY)
		{
			solved = true;
			solvedAt = Time.time;
			winT = 0f;
			winFrom = tr.position;
			winFromRot = tr.rotation;
			flagMr.enabled = false;
		}

		if (solved)
			return;

		// Tus basili tutuldukca tekrar eden hamle kaldirildi: bir takla 0.13 sn,
		// normal bir tusa basis 0.2-0.3 sn surdugu icin tek basis uc hamle oluyordu.
		// Uzaga gitmenin yolu artik kayma; tekrar mekanizmasinin isi kalmadi.
		if (queued < 0 && Chain && ate && lastDir >= 0)
			queued = Steer(lastDir);
		if (queued >= 0)
		{
			int mv = queued;
			queued = -1;
			TryMove(mv);
		}
	}

	// Bitis: oyuncunun ucgeni iki takla atarak direge konup bayrak oluyor.
	private void StepWin()
	{
		if (!solved || winT >= 1f)
			return;

		winT += Time.deltaTime / WinTime;
		float t = Mathf.Clamp01(winT);
		float e = t * t * (3f - 2f * t);

		var tr = tris[Me].T;
		tr.position = Vector3.Lerp(winFrom, flag.position, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.8f);
		tr.rotation = Quaternion.AngleAxis(720f * e, Vector3.right) * Quaternion.Slerp(winFromRot, flag.rotation, e);
		tr.localScale = Vector3.one * Mathf.Lerp(1f, flag.localScale.x, e);
	}


	// Kirilma hissi. Kural degisikligi tek basina yetmiyor: darbenin gorunmesi lazim.
	// Ayrica iki eylem gorsel olarak da ayriliyor; maviye YURUMEK kucuk ve yumusak,
	// diger rengi EZMEK buyuk ve sert bir patlama veriyor.
	private void Burst(Vector3 at, Vector3 up, Material mat, int count, float force)
	{
		for (int i = 0; i < count; i++)
		{
			int k = shardAt;
			shardAt = (shardAt + 1) % shards.Length;

			float size = UnityEngine.Random.Range(0.07f, 0.16f) * (force > 4f ? 1.3f : 0.8f);
			var t = shards[k].T;
			t.gameObject.SetActive(true);
			t.position = at;
			t.rotation = UnityEngine.Random.rotation;
			t.localScale = Vector3.one * size;
			t.GetComponent<MeshRenderer>().sharedMaterial = mat;

			Vector3 dir = (up * UnityEngine.Random.Range(0.5f, 1.2f) + UnityEngine.Random.insideUnitSphere).normalized;
			shards[k].Vel = dir * (force * UnityEngine.Random.Range(0.6f, 1.4f));
			shards[k].Spin = UnityEngine.Random.insideUnitSphere * 720f;
			shards[k].Size = size;
			shards[k].Max = UnityEngine.Random.Range(0.45f, 0.85f);
			shards[k].Life = shards[k].Max;
		}
	}

	private void StepShards()
	{
		float dt = Time.deltaTime;
		for (int i = 0; i < shards.Length; i++)
		{
			if (shards[i].Life <= 0f)
				continue;

			shards[i].Life -= dt;
			if (shards[i].Life <= 0f)
			{
				shards[i].T.gameObject.SetActive(false);
				continue;
			}

			shards[i].Vel += Vector3.down * (11f * dt);
			shards[i].T.position += shards[i].Vel * dt;
			shards[i].T.Rotate(shards[i].Spin * dt, Space.World);
			shards[i].T.localScale = Vector3.one * (shards[i].Size * Mathf.Clamp01(shards[i].Life / shards[i].Max));
		}
	}
	private void StepPops()
	{
		for (int i = popping.Count - 1; i >= 0; i--)
		{
			popT[i] += Time.deltaTime / Mathf.Max(0.02f, PopTime);
			float t = Mathf.Clamp01(popT[i]);
			var tr = tris[popping[i]].T;
			tr.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(t * Mathf.PI)) * (1f - t);
			if (t < 1f)
				continue;
			tr.gameObject.SetActive(false);
			popping.RemoveAt(i);
			popT.RemoveAt(i);
		}
	}

	// Kaymanin yurudugu yol. Hem gercek hamle hem de nereye varilacagini gosteren
	// isaretler ayni hesabi kullansin diye tek yerde: ikisi ayrilirsa isaret yalan soyler.
	private int Walk(int cx, int cy, int sx, int sy, int want, bool record, out int px, out int py)
	{
		px = cx;
		py = cy;
		int gone = 0;

		for (int k = 1; k <= want; k++)
		{
			int nx = WrapX(px + sx);
			int ny = py + sy;
			if (!In(nx, ny))
				break;
			// Duvar yatay cizgide durur, dikey kaymayi keser. Carpinca orada kaliyoruz
			// ve sadece gidilen kadari harcaniyor.
			if (sy != 0 && wallH[px, Mathf.Max(py, ny)])
				break;

			// Kayma yikim araci: diger renkleri ezip geciyor, onu sadece duvar durduruyor.
			// Tek takla ise onlarin uzerine cikamiyor. Kendi rengini YURUYEREK aliyorsun,
			// digerlerini ancak EZEREK kiriyorsun: iki renk iki ayri fiil oldu.
			// Oyuncunun ve rakibin icinden gecilmiyor.
			bool actor = false;
			for (int o = 0; o < 4 && !actor; o++)
			{
				int t = occ[nx, ny, o] - 1;
				actor = t == Me || (rivalIdx > 0 && t == rivalIdx);
			}
			if (actor)
				break;

			px = nx;
			py = ny;
			gone = k;

			if (!record)
				continue;
			for (int o = 0; o < 4; o++)
			{
				int t = occ[px, py, o] - 1;
				if (t > 0 && !slideEat.Contains(t))
				{
					slideEat.Add(t);
					slideAt.Add(k);
				}
			}
		}

		return gone;
	}

	// Ucgenin uzerindeki sayi hem toplanan mavi hem de menzil tavani.
	// Gitmek istedigin kareye basiyorsun, ucgen o yone kayarak gidiyor ve serit
	// uzerindeki her seyi yiyor. Takla bir kare, kayma menzil kadar kare:
	// iki girdinin agirligi farkli, monotonluk boyle kiriliyor.
	private bool TapSlide(Vector3 screen)
	{
		if (charge < 2 || tumbling || sliding || solved)
			return false;

		if (!PickCell(screen, out int cell, out int row) || !In(cell, row))
			return false;

		// Kayma SADECE sari isarete basinca olur. Onceden iki kareden uzak olan her
		// tiklama kaymayi tetikliyordu, bu yuzden tek adim atmak isterken ucgen
		// uzaga gidiyordu. Artik isaret ne diyorsa o; baska her tiklama tek adim.
		int dir = -1;
		for (int d = 0; d < 4; d++)
			if (markGone[d] >= 2 && markCx[d] == cell && markCy[d] == row)
				dir = d;
		if (dir < 0)
			return false;

		int cx = tris[Me].Cx;
		int cy = tris[Me].Cy;
		int sx = DirX[dir];
		int sy = DirY[dir];

		slideEat.Clear();
		slideAt.Clear();
		int gone = Walk(cx, cy, sx, sy, charge, true, out int px, out int py);

		// Sarmalda px - cx dikisin uzerinde yaniltir, gidilen adimi sayarak tutuyoruz.
		slideCells = gone;
		if (slideCells < 2)
			return false;

		occ[cx, cy, tris[Me].Ori] = 0;
		history.Add(new Move
		{
			Cx = cx,
			Cy = cy,
			Ori = tris[Me].Ori,
			A = -1,
			B = -1,
			Swept = slideEat.ToArray(),
			Cost = slideCells,
			Spent = slideCells,
		});

		slideCx = px;
		slideCy = py;
		slideFrom = tris[Me].T.position;
		slideTo = StateToPos(px, py, tris[Me].Ori);
		slideDur = slideCells / Mathf.Max(1f, SlideSpeed);
		slideT = 0f;
		slideDone = 0;
		sliding = true;
		charge -= slideCells;
		steps += slideCells;
		budget -= slideCells;
		lastDir = sx > 0 ? 0 : sx < 0 ? 1 : sy > 0 ? 2 : 3;
		return true;
	}

	private void StepSlide()
	{
		slideT += Time.deltaTime / slideDur;
		float e = Mathf.Clamp01(slideT);
		tris[Me].T.position = Vector3.Lerp(slideFrom, slideTo, e);

		// Yemek varista oluyor, basta degil: serit onceden bosalirsa ucgen
		// bos alanda kayiyormus gibi gorunuyor, ezip gectigi hissi kayboluyor.
		float at = e * slideCells;
		while (slideDone < slideEat.Count && slideAt[slideDone] <= at)
			Swallow(slideEat[slideDone++]);

		if (e < 1f)
			return;

		sliding = false;
		tris[Me].Cx = slideCx;
		tris[Me].Cy = slideCy;
		tris[Me].T.position = slideTo;
		occ[slideCx, slideCy, tris[Me].Ori] = Me + 1;

		if (slideCx == goalX && slideCy == goalY)
		{
			solved = true;
			solvedAt = Time.time;
			winT = 0f;
			winFrom = tris[Me].T.position;
			winFromRot = tris[Me].T.rotation;
			flagMr.enabled = false;
		}
	}

	// Prizmada iki yon de gecerli, kisa olani sayilir.
	private int GapDist(int a, int b)
	{
		int d = Mathf.Abs(a - b);
		return Prism ? Mathf.Min(d, GridW - d) : d;
	}

	// Rakip bilerek basit: kendi rengini yemeye, duvarin gedigini bulmaya ve yukari
	// cikmaya calisiyor, baska bir sey dusunmuyor. Olculecek sey yapay zekanin zekasi
	// degil, ayni yuzeyde bir rakibin varliginin heyecan verip vermedigi. Sadece tek
	// taklalar kullaniyor; Flip sayesinde dort yonelime de ulasabildigi icin bu yeterli.
	private void RivalThink()
	{
		int cx = tris[rivalIdx].Cx;
		int cy = tris[rivalIdx].Cy;
		int ori = tris[rivalIdx].Ori;

		int best = -1;
		float bestScore = float.NegativeInfinity;

		// Ustteki gecis duvarla kapaliysa rakip gedigi ariyor. Bu olmadan duvarin
		// onunde saga sola salinip takiliyordu: yan hamlenin tek basina puani yok,
		// bir yere gidiyor olmasi gerekiyor.
		int gap = -1;
		if (cy + 1 <= GridH && wallH[cx, cy + 1])
		{
			int near = int.MaxValue;
			for (int x = 0; x < GridW; x++)
			{
				if (wallH[x, cy + 1])
					continue;
				int d = GapDist(x, cx);
				if (d >= near)
					continue;
				near = d;
				gap = x;
			}
		}

		for (int k = 0; k < 3; k++)
		{
			int m = k == 2 ? Flip : k;
			if (Legal(cx, cy, ori, m, RivalCol) < -1)
				continue;

			Target(cx, cy, ori, m, out int nx, out int ny, out int no);

			// Oyuncunun havada oldugu hedef hucre henuz occ'ta isaretli degil,
			// elle eliyoruz; yoksa ikisi ayni yarim hucreye iniyor.
			if (tumbling && nx == pendingCx && ny == pendingCy)
				continue;
			if (sliding && nx == slideCx && ny == slideCy)
				continue;

			// Tirmanma odulu kazma cezasini acik ara gecmeli. Esit olduklarinda
			// dolu tahtada her yukari hamle sifirlaniyor ve rakip yerinde donuyor.
			float s = (ny - cy) * 40f + UnityEngine.Random.value * 3f;

			if (gap >= 0)
				s += (GapDist(cx, gap) - GapDist(nx, gap)) * 22f;

			// Hem dikey hem yatay takla, ucgenin o anki yonelimine gore tek yone gidiyor;
			// obur yon icin once Flip gerekiyor. Bu yuzden hamleyi, ardindan NE
			// yapabilecegine gore de puanliyoruz. Flip boylece tam gerektigi anda
			// degerli oluyor, bosuna yapildiginda degil.
			Target(nx, ny, no, 0, out _, out int ay, out _);
			if (ay > ny && !Barred(nx, ny, no, 0))
				s += 16f;

			if (gap >= 0)
			{
				Target(nx, ny, no, 1, out int hx, out _, out _);
				if (GapDist(hx, gap) < GapDist(nx, gap))
					s += 12f;
			}

			int t = occ[nx, ny, no] - 1;
			if (t <= 0)
				t = occ[nx, ny, no ^ 1] - 1;
			if (t > 0)
				s += 60f;

			if (s <= bestScore)
				continue;
			bestScore = s;
			best = m;
		}

		// Rakip de sikisabilir. Oyuncunun kaymasinin karsiligi olarak, tikandiginda
		// onundeki ucgeni kirip geciyor; yoksa kalici olarak yerinde donup kalirdi.
		for (int k = 0; k < 3 && best < 0; k++)
		{
			int m = k == 2 ? Flip : k;
			Target(cx, cy, ori, m, out int nx, out int ny, out int _);
			if (!In(nx, ny) || Barred(cx, cy, ori, m))
				continue;

			bool busy = false;
			for (int o = 0; o < 4 && !busy; o++)
			{
				int t = occ[nx, ny, o] - 1;
				busy = t == Me || t == rivalIdx;
			}
			if (!busy)
				best = m;
		}

		if (best < 0)
			return;

		if (!Landing(cx, cy, ori, best, RivalCol, out rivalPendCx, out rivalPendCy, out rivalPendOri))
			return;

		rivalTakeA = occ[rivalPendCx, rivalPendCy, rivalPendOri] - 1;
		rivalTakeB = -1;
		if (rivalTakeA < 0)
		{
			rivalTakeA = occ[rivalPendCx, rivalPendCy, rivalPendOri ^ 1] - 1;
			rivalTakeB = occ[rivalPendCx, rivalPendCy, rivalPendOri ^ 2] - 1;
		}

		occ[cx, cy, ori] = 0;
		Swing(cx, cy, ori, best, out rivalPivot, out rivalAxis);
		rivalFromPos = tris[rivalIdx].T.position;
		rivalFromRot = tris[rivalIdx].T.rotation;
		rivalAngle = Prism && best == 1 ? 180f - 360f / GridW : 180f;
		rivalUp = Prism ? FacetOut(cx) : Vector3.up;
		rivalT = 0f;
		rivalTumbling = true;
	}

	private void RivalStep()
	{
		if (!rivalTumbling)
		{
			rivalWait -= Time.deltaTime;
			if (rivalWait <= 0f)
				RivalThink();
			return;
		}

		rivalT += Time.deltaTime / Mathf.Max(0.02f, TumbleTime);
		float t = Mathf.Clamp01(rivalT);
		float e = t * t * (3f - 2f * t);

		var rot = Quaternion.AngleAxis(rivalAngle * e, rivalAxis);
		var tr = tris[rivalIdx].T;
		tr.position = rivalPivot + rot * (rivalFromPos - rivalPivot) + rivalUp * (Mathf.Sin(e * Mathf.PI) * HopHeight);
		tr.rotation = rot * rivalFromRot;

		if (t < 1f)
			return;

		rivalTumbling = false;
		tris[rivalIdx].Cx = rivalPendCx;
		tris[rivalIdx].Cy = rivalPendCy;
		tris[rivalIdx].Ori = rivalPendOri;
		tr.position = StateToPos(rivalPendCx, rivalPendCy, rivalPendOri);
		tr.rotation = StateToRot(rivalPendCx, rivalPendOri);

		RivalEat(rivalTakeA);
		RivalEat(rivalTakeB);
		occ[rivalPendCx, rivalPendCy, rivalPendOri] = rivalIdx + 1;

		if (rivalPendCx == goalX && rivalPendCy == goalY)
		{
			lost = true;
			lostText = "RAKIP ONCE CIKTI";
			solvedAt = Time.time;
		}

		rivalWait = RivalDelay;
	}

	private void RivalEat(int i)
	{
		if (i <= 0 || i == rivalIdx)
			return;

		occ[tris[i].Cx, tris[i].Cy, tris[i].Ori] = 0;
		popping.Add(i);
		popT.Add(0f);

		bool own = tris[i].Col == RivalCol;
		Burst(
			tris[i].T.position,
			Prism ? FacetOut(tris[i].Cx) : Vector3.up,
			tris[i].T.GetComponent<MeshRenderer>().sharedMaterial,
			own ? 4 : 9,
			own ? 2.4f : 6.2f);

		// Rakibin mavi yemesi oyuncunun hedefini azaltmiyor ama tahtadan siliyor:
		// engelleme ayri bir sistem olmadan, sadece ayni yuzeyde olmaktan doguyor.
		if (tris[i].Col == RivalCol)
			return;
	}

	private void Undo()
	{
		if (history.Count == 0)
			return;

		var m = history[history.Count - 1];
		history.RemoveAt(history.Count - 1);

		// Once oyuncunun durdugu yeri bosalt, sonra yedigi ucgenleri geri koy:
		// ters sirada oyuncunun silinisi diriltilen ucgeni de siliyordu.
		occ[tris[Me].Cx, tris[Me].Cy, tris[Me].Ori] = 0;
		if (m.Swept != null)
			for (int k = m.Swept.Length - 1; k >= 0; k--)
				Revive(m.Swept[k]);
		else
		{
			Revive(m.A);
			Revive(m.B);
		}

		tris[Me].Cx = m.Cx;
		tris[Me].Cy = m.Cy;
		tris[Me].Ori = m.Ori;
		tris[Me].T.position = StateToPos(m.Cx, m.Cy, m.Ori);
		tris[Me].T.rotation = StateToRot(m.Cx, m.Ori);
		occ[m.Cx, m.Cy, m.Ori] = Me + 1;

		charge += m.Spent;
		steps -= m.Cost;
		budget += m.Cost;
	}

	// --- ayar paneli ---

	private void OnGUI()
	{
		float s = Mathf.Max(Screen.width, Screen.height) / 900f;
		int small = Mathf.RoundToInt(17f * s);

		GUI.skin.label.fontSize = Mathf.RoundToInt(26f * s);
		// Hedef tek: zirve. Yukseklikler yan yana, yaris bir bakista okunuyor.
		string goalText = "Kat: " + (tris[Me].Cy + 1) + " / " + GridH;
		string race = rivalIdx > 0 ? "     Rakip: " + (tris[rivalIdx].Cy + 1) : "";
		GUI.Label(new Rect(16f * s, 12f * s, 1100f * s, 40f * s), goalText + race);

		GUI.skin.label.fontSize = small;
		GUI.Label(new Rect(16f * s, 54f * s, 1000f * s, 30f * s), "Ucgenin UC kenari var, dolayisiyla uc hamlesi: iki bacak komsu kareye, hipotenus ayni karenin obur yarisina");
		GUI.Label(new Rect(16f * s, 80f * s, 1000f * s, 30f * s), "Hedef zirve. Mavi saf yakit: adim ve menzil verir     Soluk ucgenler nereye devrilebilecegini gosterir     A S D E veya oklar     W: hipotenus     Z: geri     R: yeni");
		// Butce ve menzil ucgenin uzerinde duruyor, ekranin tepesinde degil: gozun
		// zaten ucgende oldugu icin azalmasini ve mavi yiyince sicramasini orada
		// goruyorsun. Tepedeki cubuga bakmak icin bakisi bolmek gerekiyordu.
		if (!solved && !lost)
		{
			var sp = cam.WorldToScreenPoint(tris[Me].T.position);
			if (sp.z > 0f)
			{
				float full = Mathf.Max(1f, Mathf.Max(1f, BudgetPerRow) * GridH);

				var big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(18f * s) };
				big.normal.textColor = Color.Lerp(
					Color.Lerp(new Color(1f, 0.32f, 0.26f), Color.white, Mathf.Clamp01(budget / full * 3f)),
					Color.white,
					budgetFlash);
				GUI.Label(new Rect(sp.x - 40f * s, Screen.height - sp.y - 13f * s, 80f * s, 26f * s), Mathf.Max(0, budget).ToString(), big);

				if (charge >= 2)
				{
					var reach = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(11f * s) };
					reach.normal.textColor = new Color(1f, 0.84f, 0.33f);
					GUI.Label(new Rect(sp.x - 40f * s, Screen.height - sp.y + 10f * s, 80f * s, 18f * s), "menzil " + charge, reach);
				}
			}
		}

		if (solved || lost || stuck)
		{
			var mid = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(stuck && !solved && !lost ? 34f * s : 54f * s) };
			string msg = solved ? "ZIRVE    " + steps + " ADIM" : lost ? lostText : "SIKISTIN     Z: geri al     R: yeni tahta";
			GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 90f * s), msg, mid);
		}

		GUI.skin.button.fontSize = small;
		if (GUI.Button(new Rect(Screen.width - 150f * s, 12f * s, 134f * s, 46f * s), showPanel ? "Kapat" : "Ayarlar"))
			showPanel = !showPanel;
		if (GUI.Button(new Rect(Screen.width - 150f * s, 66f * s, 134f * s, 46f * s), "Geri (Z)"))
			Undo();
		if (GUI.Button(new Rect(Screen.width - 150f * s, 120f * s, 134f * s, 46f * s), "Yeni (R)"))
		{
			seed++;
			Build();
		}

		GUI.skin.label.fontSize = small;
		panelLeft = Screen.width;
		if (!showPanel)
			return;

		// Liste ekrana sigmiyordu; kaydirilabilir bir pencereye alindi.
		float w = Mathf.Min(320f * s, Screen.width * 0.55f);
		float top = 178f * s;
		var frame = new Rect(Screen.width - w - 12f * s, top, w, Screen.height - top - 12f * s);
		panelLeft = frame.x;

		float inner = w - 26f * s;
		float cw = inner - 16f * s;

		GUI.Box(frame, GUIContent.none);
		panelScroll = GUI.BeginScrollView(frame, panelScroll, new Rect(0f, 0f, inner, 64f * s * 28f + 420f * s));

		float y = 6f * s;
		float x = 8f * s;

		Slider(ref CamAngle, 25f, 89f, "Genel plan acisi", x, ref y, cw, s, SetupCamera);
		Slider(ref CloseAngle, 25f, 89f, "Yakin plan acisi", x, ref y, cw, s, SetupCamera);
		Slider(ref CloseFov, 6f, 45f, "Yakin plan perspektifi", x, ref y, cw, s, SetupCamera);
		Slider(ref CloseZoom, 0.6f, 3f, "Yakin plan yakinligi", x, ref y, cw, s, SetupCamera);
		Slider(ref CamZoom, 0.6f, 1.6f, "Genel plan yakinligi", x, ref y, cw, s, SetupCamera);
		Slider(ref CloseRows, 4f, 20f, "Yakin planda satir", x, ref y, cw, s, SetupCamera);
		Slider(ref IntroHold, 0f, 3f, "Genel plan suresi", x, ref y, cw, s, null);
		Slider(ref TumbleTime, 0.04f, 0.45f, "Devirme suresi", x, ref y, cw, s, null);
		Slider(ref SlideSpeed, 4f, 30f, "Kayma hizi (kare/sn)", x, ref y, cw, s, null);
		Slider(ref PrismDist, 6f, 70f, "Kule kamera mesafesi", x, ref y, cw, s, SetupCamera);
		Slider(ref RivalDelay, 0.05f, 1.2f, "Rakip hamle araligi (sn)", x, ref y, cw, s, null);
		Slider(ref PrismPitch, -10f, 60f, "Kule kamera egimi", x, ref y, cw, s, null);
		Slider(ref HopHeight, 0f, 0.5f, "Ziplama", x, ref y, cw, s, null);
		Slider(ref TriThick, 0.02f, 0.4f, "Ucgen kalinligi", x, ref y, cw, s, null);
		Slider(ref PopTime, 0.05f, 0.6f, "Patlama suresi", x, ref y, cw, s, null);
		Slider(ref Density, 0f, 0.85f, "Doluluk", x, ref y, cw, s, null);
		Slider(ref BlueRatio, 0.05f, 0.7f, "Mavi orani", x, ref y, cw, s, null);
		Slider(ref HalfRatio, 0f, 1f, "Yarim hucre orani", x, ref y, cw, s, null);
		Slider(ref BudgetPerRow, 2f, 20f, "Butce (satir basina adim)", x, ref y, cw, s, null);
		Slider(ref BlueRefund, 0f, 10f, "Mavinin geri verdigi adim", x, ref y, cw, s, null);
		Slider(ref WallChance, 0f, 1f, "Duvar sikligi", x, ref y, cw, s, null);
		Slider(ref WallMax, 1f, 9f, "Duvar uzunlugu", x, ref y, cw, s, null);
		Slider(ref Shoulder, 0.2f, 0.9f, "Daralma baslangici", x, ref y, cw, s, null);
		SliderInt(ref GridW, 4, 14, "Genislik", x, ref y, cw, s);
		SliderInt(ref GridH, 6, 26, "Yukseklik", x, ref y, cw, s);

		if (GUI.Button(new Rect(x, y, cw, 44f * s), Chain ? "Mavi zinciri: VAR" : "Mavi zinciri: YOK"))
		{
			Chain = !Chain;
			SavePrefs();
		}
		y += 52f * s;
		if (GUI.Button(new Rect(x, y, cw, 44f * s), Hints ? "Yonerge ucgenleri: VAR" : "Yonerge ucgenleri: YOK"))
		{
			Hints = !Hints;
			SavePrefs();
		}
		y += 52f * s;
		if (GUI.Button(new Rect(x, y, cw, 44f * s), Marks ? "Menzil isareti: VAR" : "Menzil isareti: YOK"))
		{
			Marks = !Marks;
			SavePrefs();
		}
		y += 52f * s;
		if (GUI.Button(new Rect(x, y, cw, 44f * s), Rival ? "Rakip: VAR" : "Rakip: YOK"))
		{
			Rival = !Rival;
			SavePrefs();
			Build();
		}
		y += 52f * s;
		if (GUI.Button(new Rect(x, y, cw, 44f * s), Prism ? "Bicim: KULE (prizma)" : "Bicim: DUZ TAHTA"))
		{
			Prism = !Prism;
			SavePrefs();
			Build();
		}
		y += 52f * s;
		if (GUI.Button(new Rect(x, y, cw, 44f * s), "Tahtayi yeniden kur"))
			Build();
		y += 52f * s;
		if (GUI.Button(new Rect(x, y, cw, 44f * s), "Ayarlari sifirla"))
		{
			PlayerPrefs.DeleteAll();
			PlayerPrefs.Save();
			LoadPrefs();
			Build();
		}

		GUI.EndScrollView();
	}

	private void Slider(ref float v, float lo, float hi, string label, float x, ref float y, float w, float s, System.Action onChange)
	{
		GUI.Label(new Rect(x, y, w, 24f * s), label + ": " + v.ToString("0.00"));
		y += 26f * s;
		float nv = GUI.HorizontalSlider(new Rect(x, y + 8f * s, w, 20f * s), v, lo, hi);
		if (!Mathf.Approximately(nv, v))
		{
			v = nv;
			SavePrefs();
			if (onChange != null)
				onChange();
		}
		y += 38f * s;
	}

	private void SliderInt(ref int v, int lo, int hi, string label, float x, ref float y, float w, float s)
	{
		GUI.Label(new Rect(x, y, w, 24f * s), label + ": " + v);
		y += 26f * s;
		int nv = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x, y + 8f * s, w, 20f * s), v, lo, hi));
		if (nv != v)
		{
			v = nv;
			SavePrefs();
		}
		y += 38f * s;
	}

	private void LoadPrefs()
	{
		if (PlayerPrefs.GetInt("tri_ver", -1) != DefaultsVersion)
			return;

		GridW = PlayerPrefs.GetInt("tri_w", GridW);
		GridH = PlayerPrefs.GetInt("tri_h", GridH);
		Density = PlayerPrefs.GetFloat("tri_den", Density);
		BlueRatio = PlayerPrefs.GetFloat("tri_blue", BlueRatio);
		HalfRatio = PlayerPrefs.GetFloat("tri_half", HalfRatio);
		BudgetPerRow = PlayerPrefs.GetFloat("tri_budget", BudgetPerRow);
		BlueRefund = PlayerPrefs.GetFloat("tri_refund", BlueRefund);
		WallChance = PlayerPrefs.GetFloat("tri_wallp", WallChance);
		WallMax = PlayerPrefs.GetFloat("tri_walllen", WallMax);
		Shoulder = PlayerPrefs.GetFloat("tri_shoulder", Shoulder);
		TumbleTime = PlayerPrefs.GetFloat("tri_time", TumbleTime);
		SlideSpeed = PlayerPrefs.GetFloat("tri_slide", SlideSpeed);
		PrismDist = PlayerPrefs.GetFloat("tri_pdist", PrismDist);
		PrismPitch = PlayerPrefs.GetFloat("tri_ppitch", PrismPitch);
		Prism = PlayerPrefs.GetInt("tri_prism", Prism ? 1 : 0) != 0;
		Rival = PlayerPrefs.GetInt("tri_rival", Rival ? 1 : 0) != 0;
		Marks = PlayerPrefs.GetInt("tri_marks", Marks ? 1 : 0) != 0;
		Hints = PlayerPrefs.GetInt("tri_hints", Hints ? 1 : 0) != 0;
		Chain = PlayerPrefs.GetInt("tri_chain", Chain ? 1 : 0) != 0;
		RivalDelay = PlayerPrefs.GetFloat("tri_rivdelay", RivalDelay);
		HopHeight = PlayerPrefs.GetFloat("tri_hop", HopHeight);
		TriThick = PlayerPrefs.GetFloat("tri_thick", TriThick);
		PopTime = PlayerPrefs.GetFloat("tri_pop", PopTime);
		CamAngle = PlayerPrefs.GetFloat("tri_ang", CamAngle);
		CamZoom = PlayerPrefs.GetFloat("tri_zoom", CamZoom);
		CloseAngle = PlayerPrefs.GetFloat("tri_cang", CloseAngle);
		CloseFov = PlayerPrefs.GetFloat("tri_cfov", CloseFov);
		CloseZoom = PlayerPrefs.GetFloat("tri_czoom", CloseZoom);
		CloseRows = PlayerPrefs.GetFloat("tri_rows", CloseRows);
		IntroHold = PlayerPrefs.GetFloat("tri_intro", IntroHold);
		seed = PlayerPrefs.GetInt("tri_seed", seed);
	}

	private void SavePrefs()
	{
		PlayerPrefs.SetInt("tri_ver", DefaultsVersion);
		PlayerPrefs.SetInt("tri_w", GridW);
		PlayerPrefs.SetInt("tri_h", GridH);
		PlayerPrefs.SetFloat("tri_den", Density);
		PlayerPrefs.SetFloat("tri_blue", BlueRatio);
		PlayerPrefs.SetFloat("tri_half", HalfRatio);
		PlayerPrefs.SetFloat("tri_budget", BudgetPerRow);
		PlayerPrefs.SetFloat("tri_refund", BlueRefund);
		PlayerPrefs.SetFloat("tri_wallp", WallChance);
		PlayerPrefs.SetFloat("tri_walllen", WallMax);
		PlayerPrefs.SetFloat("tri_shoulder", Shoulder);
		PlayerPrefs.SetFloat("tri_time", TumbleTime);
		PlayerPrefs.SetFloat("tri_slide", SlideSpeed);
		PlayerPrefs.SetFloat("tri_pdist", PrismDist);
		PlayerPrefs.SetFloat("tri_ppitch", PrismPitch);
		PlayerPrefs.SetInt("tri_prism", Prism ? 1 : 0);
		PlayerPrefs.SetInt("tri_rival", Rival ? 1 : 0);
		PlayerPrefs.SetInt("tri_marks", Marks ? 1 : 0);
		PlayerPrefs.SetInt("tri_hints", Hints ? 1 : 0);
		PlayerPrefs.SetInt("tri_chain", Chain ? 1 : 0);
		PlayerPrefs.SetFloat("tri_rivdelay", RivalDelay);
		PlayerPrefs.SetFloat("tri_hop", HopHeight);
		PlayerPrefs.SetFloat("tri_thick", TriThick);
		PlayerPrefs.SetFloat("tri_pop", PopTime);
		PlayerPrefs.SetFloat("tri_ang", CamAngle);
		PlayerPrefs.SetFloat("tri_zoom", CamZoom);
		PlayerPrefs.SetFloat("tri_cang", CloseAngle);
		PlayerPrefs.SetFloat("tri_cfov", CloseFov);
		PlayerPrefs.SetFloat("tri_czoom", CloseZoom);
		PlayerPrefs.SetFloat("tri_rows", CloseRows);
		PlayerPrefs.SetFloat("tri_intro", IntroHold);
		PlayerPrefs.SetInt("tri_seed", seed);
		PlayerPrefs.Save();
	}
}
