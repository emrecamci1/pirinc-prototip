using System.Collections.Generic;
using UnityEngine;

// Katmanli temizlik testi.
// Mahjong Solitaire gibi ust uste yiginlanmis sekiller: her katin kendi zemin rengi var,
// uzerinde zemine ait olmayan sicak renkli parcalar duruyor. Ucgenle gezip parcanin
// uzerine devriliyorsun.
//
// Hedef: ustte bir GOREV duruyor ve iki tipten biri oluyor.
//   Tek renk: "su renkten 3 tane" - ustune devrildigin her dogru renk sayilir.
//   Kare:     iki yarim, mesela alt-solda sari + ust-sagda kirmizi. Elinde sari
//             varken kirmizi parcanin KARSI YARISINA, rozette hangi tarafta
//             duruyorsa O TARAFA basarsan kare tamamlanir. Yani sadece renkler
//             degil, dizilis de tutmali.
//
// Parcanin ustune devrilince onu yok etmiyorsun, RENGINI aliyorsun.
//
// GEREKMEYEN RENKLER DUVAR. Ustunden gecip yok edemiyorsun, yolunu onlara gore
// kuruyorsun. Sebebi basit: gorev degisince o renklere ihtiyacin olacak, yolda
// harcanmalari oyunu kendi kendine bitiriyordu.
//
// Kat gorev doldurunca iniliyor (TasksPerLayer), tahtayi supurerek degil.
//
// Bu yeni bir fizik getirmiyor: yerinde burgu zaten ayni karenin diger
// yarisina geciriyor ve bir parcanin karsi yarisina basmak baslangictan beri
// serbestti, parca yerinde kaliyordu. O durus oyunda vardi ama hicbir anlami
// yoktu. Simdi kare orada tamamlaniyor.
//
// Tek renk tasiniyor: yenisini almak eskisini siler. Karar burada.
//
// Kat temizlenince zemin cekiliyor ve alt kata iniyorsun; o katta birakilan
// parcalar seninle beraber dusuyor.
public class StackPrototype : MonoBehaviour
{
	private const int DefaultsVersion = 7;

	// Yonelim = dik acinin hucre icinde hangi kosede oldugu.
	// bit0: 0 = sol, 1 = sag     bit1: 0 = alt, 1 = ust
	private static readonly int[] DirX = { 1, -1, 0, 0 };
	private static readonly int[] DirY = { 0, 0, 1, -1 };
	private static readonly float[] OriAngle = { 0f, 270f, 90f, 180f };

	// Ucgenin kenari uctur, dolayisiyla hamlesi de uctur.
	// 0 : yatay bacagin uzerinden -> dikey komsu hucre
	// 1 : dikey bacagin uzerinden -> yatay komsu hucre
	// 2 : hipotenusun uzerinden   -> ayni karenin obur yarisi
	private const int MoveCount = 3;
	private const int Flip = 2;

	// Zemin soguk ve mat, parca sicak ve doygun. Ayrim renk ailesinden okunuyor,
	// tek tek renk ezberlemeye gerek kalmiyor.
	private static readonly Color[] FloorTone =
	{
		new Color(0.40f, 0.58f, 0.74f),
		new Color(0.38f, 0.65f, 0.60f),
		new Color(0.47f, 0.53f, 0.72f),
		new Color(0.52f, 0.60f, 0.52f),
		new Color(0.44f, 0.50f, 0.62f),
	};

	private static readonly Color BackColor = new Color(0.80f, 0.79f, 0.75f);

	private static readonly Color[] PieceTone =
	{
		new Color(0.92f, 0.41f, 0.34f),
		new Color(0.96f, 0.67f, 0.26f),
		new Color(0.89f, 0.36f, 0.53f),
	};

	[System.NonSerialized] public int Layers = 3;
	[System.NonSerialized] public int TopRadius = 2;
	[System.NonSerialized] public int RadiusStep = 1;
	[System.NonSerialized] public float LayerGap = 0.82f;
	[System.NonSerialized] public float SlabRatio = 1.15f;
	[System.NonSerialized] public float FogAmount = 0.8f;
	[System.NonSerialized] public float HintSize = 0.42f;
	[System.NonSerialized] public float PieceRatio = 0.26f;
	[System.NonSerialized] public int TasksPerLayer = 3;
	[System.NonSerialized] public float TumbleTime = 0.13f;
	[System.NonSerialized] public float HopHeight = 0.06f;
	[System.NonSerialized] public float PopTime = 0.22f;
	[System.NonSerialized] public float DropTime = 0.5f;
	[System.NonSerialized] public float TriThick = 0.11f;
	private float FloorThick;
	[System.NonSerialized] public float Inset = 0.07f;
	[System.NonSerialized] public float FloorInset = 0.11f;
	[System.NonSerialized] public float CamPitch = 52f;
	[System.NonSerialized] public float CamZoom = 1f;
	[System.NonSerialized] public bool Hints = true;
	[System.NonSerialized] public float SwipeMin = 90f;
	[System.NonSerialized] public float TapRadius = 0.09f;

	private struct Piece
	{
		public int Lay, Cx, Cy, Ori;
		public int Tone;
		public Transform T;
	}

	private struct Move
	{
		public int Lay, Cx, Cy, Ori;
		public int A, B;
		public bool Dove;
		public int Carry, WantA, WantB, Pair;
		public int Tasks, Kind, Tone, Need, Got, WOri, LTasks;
	}

	private Camera cam;
	private Transform board;
	private Mesh triMesh;
	private Mesh floorMesh;
	private Mesh hintMesh;
	private Material matMe;
	private Material matHole;
	private Material matHint;
	private Material matHintTake;
	private Material[] matFloor;
	private Material[] matFloorAlt;
	private Material[] matPiece;

	private int gridN;
	private int mid;
	private bool[,,] onShape;
	private bool[,,,] reach;
	private int[,,,] occ;
	private Transform[] layerRoot;
	private Transform[] fogSheet;
	private int[] holeCx, holeCy, holeOri;
	private int[] alive;

	private Piece[] pieces;
	private int pieceCount;

	private Transform me;
	private int myLay, myCx, myCy, myOri;
	private int activeLay;

	private bool tumbling;
	private float tumbleT;
	private Vector3 tumblePivot;
	private Vector3 tumbleAxis;
	private Vector3 tumbleFromPos;
	private Quaternion tumbleFromRot;
	private int pendingCx, pendingCy, pendingOri;
	private int takeA, takeB;

	private bool dropping;
	private float dropT;
	private int dropLay;
	private Vector3 meDropFrom, meDropTo;
	private readonly List<int> dropIdx = new List<int>();
	private readonly List<Vector3> dropFrom = new List<Vector3>();
	private readonly List<Vector3> dropTo = new List<Vector3>();

	private Transform[] hints;
	private readonly List<int> popping = new List<int>();
	private readonly List<float> popT = new List<float>();
	private readonly List<Move> history = new List<Move>();

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

	private int steps;
	private int tasks;
	private int carry = -1;
	private int objKind = -1;
	private int objTone = -1;
	private int objNeed;
	private int objGot;
	private int wantA = -1;
	private int wantB = -1;
	private int wantOri;
	private int layerTasks;
	private float callSlide = 1f;
	private readonly int[] tonePool = new int[PieceTone.Length];
	private readonly int[] toneCand = new int[PieceTone.Length];
	private Texture2D[] halfTex;
	private Texture2D upTex;
	private bool solved;
	private bool lost;
	private string lostText = "";

	private float focusY;
	private float focusVelY;
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

		cam = Camera.main;
		if (cam == null)
			cam = new GameObject("Main Camera").AddComponent<Camera>();
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = BackColor;

		// Rozet damgalari. Dort yarim, yonelim indisiyle ayni sirada: bit0 dik
		// acinin sagda olmasi, bit1 ustte olmasi. Doku satiri 0 ekranda ALTTA
		// durdugu icin doku uzayi tahta uzayiyla ayni yonde, cevirmeye gerek yok.
		halfTex = new Texture2D[4];
		for (int k = 0; k < 4; k++)
		{
			var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
			tex.filterMode = FilterMode.Bilinear;
			var px = new Color32[64 * 64];
			for (int ty = 0; ty < 64; ty++)
			{
				for (int tx = 0; tx < 64; tx++)
				{
					bool on;
					switch (k)
					{
						case 0: on = tx + ty <= 61; break;
						case 1: on = ty <= tx - 2; break;
						case 2: on = ty >= tx + 2; break;
						default: on = tx + ty >= 65; break;
					}
					px[ty * 64 + tx] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
				}
			}
			tex.SetPixels32(px);
			tex.Apply();
			halfTex[k] = tex;
		}

		// Tek renk gorevinin rozeti simetrik bir ucgen: dizilis istemedigi icin
		// silueti de yon belirtmemeli.
		upTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
		upTex.filterMode = FilterMode.Bilinear;
		var upx = new Color32[64 * 64];
		for (int ty = 0; ty < 64; ty++)
		{
			float half = 32f * (1f - ty / 63f);
			for (int tx = 0; tx < 64; tx++)
				upx[ty * 64 + tx] = Mathf.Abs(tx + 0.5f - 32f) <= half ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
		}
		upTex.SetPixels32(upx);
		upTex.Apply();

		// Isikli shader olmadan yigin tamamen duz bir leke gibi goruluyor:
		// ne katlarin yuksekligi ne de ucgenlerin kalinligi okunuyor.
		if (FindObjectOfType<Light>() == null)
		{
			var lg = new GameObject("Sun").AddComponent<Light>();
			lg.type = LightType.Directional;
			lg.intensity = 0.95f;
			lg.transform.rotation = Quaternion.Euler(52f, 36f, 0f);
			// Golge olmadan ust kat alt katin uzerine oturmuyor, havada duruyor.
			lg.shadows = LightShadows.Soft;
		}
		RenderSettings.ambientLight = new Color(0.52f, 0.54f, 0.60f);

		Build();
		SetupCamera();
	}

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
		Vector3 m0 = (a + b + c) / 3f;
		a = Vector3.LerpUnclamped(a, m0, inset);
		b = Vector3.LerpUnclamped(b, m0, inset);
		c = Vector3.LerpUnclamped(c, m0, inset);

		// Kalinlik yuzeyin iki yanina esit dagiliyor: 180 derecelik takladan sonra
		// ucgen yine duzgun oturuyor, zeminin icine gomulmuyor.
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

		var msh = new Mesh();
		msh.SetVertices(verts);
		msh.SetNormals(norms);
		msh.SetTriangles(tris, 0);
		msh.RecalculateBounds();
		return msh;
	}

	// --- geometri ---

	private int Radius(int lay)
	{
		return TopRadius + lay * RadiusStep;
	}

	// Sekiller ic ice geciyor: ust kat her zaman alt katin icinde kaliyor, yoksa
	// delikten inen ucgen altta zemin bulamaz. Hem kenar hem toplam siniri
	// yaricapla birlikte buyudugu icin bu kendiliginden saglaniyor.
	// r = 2 baklava, r = 3 sekizgen, daha buyugu koseleri kesik kare.
	private bool Shape(int lay, int cx, int cy)
	{
		if (cx < 0 || cy < 0 || cx >= gridN || cy >= gridN)
			return false;

		int r = Radius(lay);
		int dx = Mathf.Abs(cx - mid);
		int dy = Mathf.Abs(cy - mid);
		return dx <= r && dy <= r && dx + dy <= r + Mathf.Max(0, (r - 1) / 2);
	}

	private float LayerY(int lay)
	{
		return (Layers - 1 - lay) * LayerGap;
	}

	private Vector3 CornerToWorld(int lay, float gx, float gy)
	{
		return new Vector3(gx - gridN * 0.5f, LayerY(lay), gy - gridN * 0.5f);
	}

	private Vector3 StateToPos(int lay, int cx, int cy, int ori)
	{
		return CornerToWorld(lay, cx + (ori & 1), cy + ((ori >> 1) & 1));
	}

	private Quaternion StateToRot(int ori)
	{
		return Quaternion.Euler(0f, OriAngle[ori], 0f);
	}

	// --- hareket ---

	// Takla atilan kenar yerinde kalir, karsi kose kenarin obur tarafina gecer.
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

		nx = lx > 0 ? rx : rx - 1;
		ny = ly > 0 ? ry : ry - 1;
		no = (lx < 0 ? 1 : 0) | (ly < 0 ? 2 : 0);
	}

	// Taklanin ekseni: uzerinden devrildigi kenar. Isaret ucgen daima tahtanin
	// USTUNDEN donsun diye seciliyor.
	private void Swing(int lay, int cx, int cy, int ori, int m, out Vector3 point, out Vector3 axis)
	{
		int rx = cx + (ori & 1);
		int ry = cy + ((ori >> 1) & 1);
		int lx = (ori & 1) == 0 ? 1 : -1;
		int ly = ((ori >> 1) & 1) == 0 ? 1 : -1;

		if (m == 0)
		{
			point = CornerToWorld(lay, rx, ry);
			axis = (CornerToWorld(lay, rx + lx, ry) - point).normalized * (lx * (ly > 0 ? -1f : 1f));
			return;
		}

		if (m == 1)
		{
			point = CornerToWorld(lay, rx, ry);
			axis = (CornerToWorld(lay, rx, ry + ly) - point).normalized * (ly * (lx > 0 ? 1f : -1f));
			return;
		}

		point = CornerToWorld(lay, rx + lx, ry);
		axis = (CornerToWorld(lay, rx, ry + ly) - point).normalized * (lx * ly > 0 ? -1f : 1f);
	}

	// Parcalar engel degil, hedef: uzerine devriliyorsun ve kalkiyorlar. Bu yuzden
	// tek sinir siluetin kendisi. Hipotenus taklasi hep ayni hucrede kaldigi icin
	// de her zaman en az bir hamle var: oyun asla kilitlenmiyor.
	private bool Landing(int lay, int cx, int cy, int ori, int m, out int nx, out int ny, out int no)
	{
		Target(cx, cy, ori, m, out nx, out ny, out no);
		return Shape(lay, nx, ny);
	}

	// Gorevin istemedigi her parca DUVAR. Ulasilabilirlik hesabi bunu gormuyor
	// (tahtanin sabit sekli ile oyun sirasindaki durum ayri seyler), bu yuzden
	// Landing'in icine konmuyor; sadece hamle ve nisan tarafinda soruluyor.
	private bool Open(int nx, int ny, int no)
	{
		return Takeable(occ[myLay, nx, ny, no] - 1)
			&& Takeable(occ[myLay, nx, ny, no ^ 1] - 1)
			&& Takeable(occ[myLay, nx, ny, no ^ 2] - 1);
	}

	// Kare gorevinde sadece TASINACAK renk basilabilir; esi tahtada kalmali,
	// yoksa oyuncu kendi hedefini yolda yiyor.
	private bool Takeable(int i)
	{
		if (i < 0)
			return true;
		return objKind == 0 ? pieces[i].Tone == objTone : pieces[i].Tone == wantA;
	}

	// --- kurulum ---

	private void Build()
	{
		if (board != null)
			Destroy(board.gameObject);

		var rnd = new System.Random(seed);
		board = new GameObject("Stack").transform;

		Layers = Mathf.Clamp(Layers, 2, FloorTone.Length);
		gridN = Radius(Layers - 1) * 2 + 1;
		mid = gridN / 2;

		// Zemin karosu ince bir levha degil, asagi uzanan bir GOVDE. Mahjong
		// yigininin derinligi tam olarak taslarin yan yuzunden okunuyor; yan yuz
		// yoksa katlar ust uste degil, ic ice halkalar gibi goruluyor.
		triMesh = BuildTriMesh(Inset, TriThick);
		FloorThick = LayerGap * SlabRatio;
		floorMesh = BuildTriMesh(FloorInset, FloorThick);
		// Yonerge zemine CIZILMIS bir isaret, ortada duran bir cisim degil: ince ve
		// iceri cekik. Karo meshini paylassalardi kat govdesi kadar kalinlasir ve
		// oyuncunun ucgeninden ayirt edilemezlerdi.
		hintMesh = BuildTriMesh(1f - HintSize, 0.02f);

		matMe = MakeMat(new Color(0.97f, 0.97f, 0.99f));
		matHint = MakeMat(new Color(0.86f, 0.86f, 0.86f));
		matHintTake = MakeMat(new Color(0.98f, 0.86f, 0.36f));

		matHole = MakeMat(new Color(0.12f, 0.12f, 0.15f));
		matFloor = new Material[Layers];
		matFloorAlt = new Material[Layers];
		for (int l = 0; l < Layers; l++)
		{
			Color c = FloorTone[l % FloorTone.Length];
			matFloor[l] = MakeMat(c);
			matFloorAlt[l] = MakeMat(c * 0.86f);
		}

		matPiece = new Material[PieceTone.Length];
		for (int i = 0; i < PieceTone.Length; i++)
			matPiece[i] = MakeMat(PieceTone[i]);

		onShape = new bool[Layers, gridN, gridN];
		reach = new bool[Layers, gridN, gridN, 4];
		occ = new int[Layers, gridN, gridN, 4];
		layerRoot = new Transform[Layers];
		fogSheet = new Transform[Layers];
		holeCx = new int[Layers];
		holeCy = new int[Layers];
		holeOri = new int[Layers];
		alive = new int[Layers];

		for (int l = 0; l < Layers; l++)
			for (int x = 0; x < gridN; x++)
				for (int y = 0; y < gridN; y++)
					onShape[l, x, y] = Shape(l, x, y);

		// Oyuncu en ust katin alt ucundan basliyor.
		myLay = 0;
		myCx = mid;
		myCy = mid;
		for (int y = 0; y < gridN; y++)
		{
			if (!onShape[0, mid, y])
				continue;
			myCy = y;
			break;
		}
		myOri = DiagOri(myCx, myCy, 0);
		activeLay = 0;

		// Delik yok: kat temizlenince asagi iniliyor. Geriye sadece hangi yarilara
		// gercekten basilabildigini cikarmak kaliyor.
		for (int l = 0; l < Layers; l++)
			MarkReach(l, myCx, myCy, myOri);

		var list = new List<Piece>();
		for (int l = 0; l < Layers; l++)
		{
			layerRoot[l] = new GameObject("Layer" + l).transform;
			layerRoot[l].SetParent(board, false);
			BuildFloor(l);
			MakePieces(l, rnd, list);
			MakeFog(l);
		}

		pieces = list.ToArray();
		pieceCount = pieces.Length;
		for (int i = 0; i < pieceCount; i++)
		{
			occ[pieces[i].Lay, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori] = i + 1;
			alive[pieces[i].Lay]++;
		}

		me = MakeTri("Me", triMesh, matMe, board);
		me.position = StateToPos(myLay, myCx, myCy, myOri);
		me.rotation = StateToRot(myOri);

		hints = new Transform[MoveCount];
		for (int i = 0; i < MoveCount; i++)
		{
			hints[i] = MakeTri("Hint" + i, hintMesh, matHint, board);
			hints[i].gameObject.SetActive(false);
		}

		var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		var cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
		Destroy(cube);

		shards = new Shard[220];
		for (int i = 0; i < shards.Length; i++)
		{
			var t = MakeTri("Shard", cubeMesh, matMe, board);
			t.gameObject.SetActive(false);
			shards[i].T = t;
		}

		steps = 0;
		tasks = 0;
		layerTasks = 0;
		carry = -1;
		objKind = -1;
		objTone = -1;
		wantA = -1;
		wantB = -1;
		solved = false;
		lost = false;
		lostText = "";
		tumbling = false;
		dropping = false;
		shardAt = 0;
		history.Clear();
		popping.Clear();
		popT.Clear();
		focusY = LayerY(0);
		focusVelY = 0f;
		TintMe();
		NextTask();
		UpdateHints();
	}

	// Her katin USTUNE arka plan renginde saydam bir tabaka seriliyor. Asagi
	// indikce tabakalar ust uste bindigi icin derinlik kendiliginden artiyor.
	// Malzemeyi boyamak yetmiyordu: sis zemini soluklastiriyor ama golgeler ve
	// parcalar keskin kaliyordu. Tabaka hepsini birden yutuyor.
	// Tabakanin rengi arka planla ayni oldugu icin tahtanin disina tasan kismi
	// gorunmuyor; sadece altinda bir sey varken is goruyor.
	// Kat kac kat solgun gorunecek. En alttaki tam FogAmount kadar solar,
	// aradakiler egriye gore; hicbiri arka planin icinde tamamen kaybolmaz.
	private float FogSeen(int lay)
	{
		if (Layers < 2 || lay < 1)
			return 0f;
		return Mathf.Clamp(FogAmount, 0f, 0.93f) * Mathf.Pow(lay / (float)(Layers - 1), 0.68f);
	}

	private void MakeFog(int lay)
	{
		var sh = Shader.Find("Sprites/Default");
		if (sh == null)
			sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");

		// Tabakanin kendi opakligi degil, o katin SONUCTA ne kadar gorunecegi
		// belirleniyor; tabaka alfasi ondan geri hesaplaniyor. Tabakalar ust uste
		// bindigi icin her birine dogrudan deger vermek en alt kati hizla yutuyor
		// ve uc katli yiginda sadece iki kat goruluyordu.
		float a = lay < 1 ? 0f : 1f - (1f - FogSeen(lay)) / (1f - FogSeen(lay - 1));
		var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
		Destroy(go.GetComponent<Collider>());
		go.name = "Fog";
		go.transform.SetParent(layerRoot[lay], false);
		go.transform.position = new Vector3(0f, LayerY(lay) + TriThick * 0.9f, 0f);
		go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		go.transform.localScale = new Vector3(gridN + 4f, gridN + 4f, 1f);

		var mr = go.GetComponent<MeshRenderer>();
		mr.sharedMaterial = new Material(sh) { color = new Color(BackColor.r, BackColor.g, BackColor.b, a) };
		mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		mr.receiveShadows = false;

		fogSheet[lay] = go.transform;
		go.SetActive(lay > 0);
	}

	private Transform MakeTri(string name, Mesh msh, Material mat, Transform parent)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		go.AddComponent<MeshFilter>().sharedMesh = msh;
		go.AddComponent<MeshRenderer>().sharedMaterial = mat;
		return go.transform;
	}

	// Ulasilabilirlik tahtadan once hesaplaniyor. Ucgen bir komsuya devrildiginde
	// hep karsi caprazda cikiyor, hipotenus taklasi ise capraz degistirmiyor; yani
	// bazi yarilar ancak belli rotalardan gorulebiliyor. Delik ya da parca oraya
	// dusmesin diye once BFS ile gercekten gidilebilen yarilari cikariyoruz.
	private void MarkReach(int lay, int sx, int sy, int so)
	{
		var dist = new int[gridN, gridN, 4];
		for (int x = 0; x < gridN; x++)
			for (int y = 0; y < gridN; y++)
				for (int o = 0; o < 4; o++)
					dist[x, y, o] = -1;

		var qx = new Queue<int>();
		var qy = new Queue<int>();
		var qo = new Queue<int>();
		dist[sx, sy, so] = 0;
		reach[lay, sx, sy, so] = true;
		qx.Enqueue(sx);
		qy.Enqueue(sy);
		qo.Enqueue(so);

		while (qx.Count > 0)
		{
			int cx = qx.Dequeue();
			int cy = qy.Dequeue();
			int o = qo.Dequeue();
			int d = dist[cx, cy, o];

			for (int m = 0; m < MoveCount; m++)
			{
				if (!Landing(lay, cx, cy, o, m, out int nx, out int ny, out int no))
					continue;
				if (dist[nx, ny, no] >= 0)
					continue;
				dist[nx, ny, no] = d + 1;
				reach[lay, nx, ny, no] = true;
				qx.Enqueue(nx);
				qy.Enqueue(ny);
				qo.Enqueue(no);
			}
		}

		holeCx[lay] = -1;
		holeCy[lay] = -1;
		holeOri[lay] = 0;
	}

	private bool IsHole(int lay, int cx, int cy, int ori)
	{
		return cx == holeCx[lay] && cy == holeCy[lay] && ori == holeOri[lay];
	}

	// Ucgen bir komsuya her devrildiginde karsi capraza cikiyor, hipotenus taklasi
	// da capraz degistirmiyor. Sonuc: her karede sadece TEK bir capraza basilabiliyor
	// ve hangisi oldugu (x + y) paritesine gore satranc tahtasi gibi degisiyor.
	// Zemin de parcalar da bu caprazdan boluniyor; dosemenin kendisi ucgenin
	// nereye basabildigini gosteriyor, ayrica anlatmaya gerek kalmiyor.
	private static int DiagOri(int cx, int cy, int h)
	{
		return ((cx + cy) & 1) == 0 ? (h == 0 ? 0 : 3) : (h == 0 ? 1 : 2);
	}

	private void BuildFloor(int lay)
	{
		for (int x = 0; x < gridN; x++)
		{
			for (int y = 0; y < gridN; y++)
			{
				if (!onShape[lay, x, y])
					continue;

				for (int h = 0; h < 2; h++)
				{
					int ori = DiagOri(x, y, h);
					bool hole = IsHole(lay, x, y, ori);
					var mat = hole ? matHole : (h == 0 ? matFloor[lay] : matFloorAlt[lay]);
					var t = MakeTri("F", floorMesh, mat, layerRoot[lay]);
					t.position = StateToPos(lay, x, y, ori)
						+ Vector3.down * (TriThick * 0.5f + FloorThick * 0.5f + 0.004f + (hole ? 0.28f : 0f));
					t.rotation = StateToRot(ori);
				}
			}
		}
	}

	private void MakePieces(int lay, System.Random rnd, List<Piece> list)
	{
		for (int x = 0; x < gridN; x++)
		{
			for (int y = 0; y < gridN; y++)
			{
				if (!onShape[lay, x, y])
					continue;

				for (int h = 0; h < 2; h++)
				{
					int ori = DiagOri(x, y, h);
					if (!reach[lay, x, y, ori])
						continue;
					if (IsHole(lay, x, y, ori))
						continue;
					if (lay == myLay && x == myCx && y == myCy && ori == myOri)
						continue;
					if (rnd.NextDouble() > PieceRatio)
						continue;

					int tone = rnd.Next(0, PieceTone.Length);
					var t = MakeTri("P", triMesh, matPiece[tone], layerRoot[lay]);
					t.position = StateToPos(lay, x, y, ori);
					t.rotation = StateToRot(ori);
					list.Add(new Piece { Lay = lay, Cx = x, Cy = y, Ori = ori, Tone = tone, T = t });
				}
			}
		}
	}

	// --- girdi ---

	private void Update()
	{
		if (camScreen.x != Screen.width || camScreen.y != Screen.height)
			SetupCamera();

		StepPops();
		StepShards();
		UpdateCamera();
		callSlide = Mathf.Min(1f, callSlide + Time.deltaTime / 0.28f);

		if (Input.GetKeyDown(KeyCode.Tab))
			showPanel = !showPanel;

		if (dropping)
		{
			StepDrop();
			return;
		}

		if (tumbling)
		{
			StepTumble();
			return;
		}

		if (Input.GetKeyDown(KeyCode.R))
		{
			Build();
			return;
		}
		if (Input.GetKeyDown(KeyCode.N))
		{
			seed++;
			SavePrefs();
			Build();
			return;
		}
		if (Input.GetKeyDown(KeyCode.Z))
		{
			Undo();
			return;
		}

		if (solved || lost)
			return;

		int m = ReadMove();
		if (m >= 0)
			TryMove(m);

		UpdateHints();
	}

	private int ReadMove()
	{
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

		return TapMove(Input.mousePosition);
	}


	private int Steer(int dir)
	{
		int wx = myCx + DirX[dir];
		int wy = myCy + DirY[dir];

		// Iki bacaktan hangisi o yone goturuyorsa o. Goturen yoksa hamle yok:
		// ucgenin o anda nereye devrilebildigini yonerge ucgenleri gosteriyor.
		for (int m = 0; m < 2; m++)
		{
			Target(myCx, myCy, myOri, m, out int nx, out int ny, out int _);
			if (nx == wx && ny == wy && Landing(myLay, myCx, myCy, myOri, m, out int lx2, out int ly2, out int lo2) && Open(lx2, ly2, lo2))
				return m;
		}
		return -1;
	}

	// Telefonda parmak nisanin tam ustune denk gelmez, ustelik dokundugu yeri
	// kendisi kapatir. Bu yuzden karenin icinde mi diye bakmiyoruz: bol bir
	// yaricap icindeki EN YAKIN nisan seciliyor. Yaricapin disina dokunmak
	// hicbir sey yapmaz; uzaga basinca ucgenin kendiliginden yurumesi daha
	// once en cok sikayet edilen seydi.
	//
	// Olcu noktasi StateToPos OLAMAZ: o bir durumun dik aci kosesini verir,
	// iki komsu takla da ayni kose etrafinda dondugu icin 0 ve 1 hamlelerinin
	// StateToPos degeri birebir ayni noktadir. Nisanlar ekranda ayri yerlerde
	// gorunur (mesh o koseye tutturulup farkli aciyla dondurulur) ama olcum
	// onlari ust uste sanirdi; esitlikte ilki kazandigi icin 1 numarali hamle
	// dokunusla hic secilemiyordu. Olcu noktasi artik nisanin GORUNEN agirlik
	// merkezi: dik aci kosesinden iki bacak yonunde birer bolu uc.
	private int TapMove(Vector3 screen)
	{
		float r = Mathf.Max(Screen.width, Screen.height) * TapRadius;
		float best = r * r;
		int pick = -1;
		var tap = new Vector2(screen.x, screen.y);

		for (int m = 0; m < MoveCount; m++)
		{
			if (!Landing(myLay, myCx, myCy, myOri, m, out int nx, out int ny, out int no) || !Open(nx, ny, no))
				continue;

			float rx = nx + (no & 1);
			float ry = ny + ((no >> 1) & 1);
			float lx = (no & 1) == 0 ? 1f : -1f;
			float ly = ((no >> 1) & 1) == 0 ? 1f : -1f;

			Vector3 sp = cam.WorldToScreenPoint(CornerToWorld(myLay, rx + lx / 3f, ry + ly / 3f));
			float d = (new Vector2(sp.x, sp.y) - tap).sqrMagnitude;
			if (d >= best)
				continue;
			best = d;
			pick = m;
		}
		return pick;
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

	private bool PickFine(Vector3 screen, out int cell, out int row, out float fx, out float fy)
	{
		cell = 0;
		row = 0;
		fx = 0f;
		fy = 0f;

		var ray = cam.ScreenPointToRay(screen);
		var plane = new Plane(Vector3.up, new Vector3(0f, LayerY(myLay), 0f));
		if (!plane.Raycast(ray, out float dist))
			return false;

		Vector3 flat = ray.GetPoint(dist);
		float gx = flat.x + gridN * 0.5f;
		float gy = flat.z + gridN * 0.5f;
		cell = Mathf.FloorToInt(gx);
		row = Mathf.FloorToInt(gy);
		fx = gx - Mathf.Floor(gx);
		fy = gy - Mathf.Floor(gy);
		return true;
	}

	// --- hamle ---

	private void TryMove(int m)
	{
		if (!Landing(myLay, myCx, myCy, myOri, m, out pendingCx, out pendingCy, out pendingOri))
			return;
		if (!Open(pendingCx, pendingCy, pendingOri))
			return;

		// Inis yerini fiziksel olarak kapatan her parca kalkiyor: ya tek yarim,
		// ya da kareyi ters yonde bolen karsi capraz ikisi.
		takeA = occ[myLay, pendingCx, pendingCy, pendingOri] - 1;
		takeB = -1;
		if (takeA < 0)
		{
			takeA = occ[myLay, pendingCx, pendingCy, pendingOri ^ 1] - 1;
			takeB = occ[myLay, pendingCx, pendingCy, pendingOri ^ 2] - 1;
		}

		history.Add(new Move
		{
			Lay = myLay, Cx = myCx, Cy = myCy, Ori = myOri, A = takeA, B = takeB, Dove = false,
			Carry = carry, WantA = wantA, WantB = wantB, Pair = -1,
			Tasks = tasks, Kind = objKind, Tone = objTone, Need = objNeed, Got = objGot,
			WOri = wantOri, LTasks = layerTasks
		});

		Swing(myLay, myCx, myCy, myOri, m, out tumblePivot, out tumbleAxis);
		tumbleFromPos = me.position;
		tumbleFromRot = me.rotation;
		tumbleT = 0f;
		tumbling = true;

		steps++;
	}

	private void StepTumble()
	{
		tumbleT += Time.deltaTime / Mathf.Max(0.02f, TumbleTime);
		float t = Mathf.Clamp01(tumbleT);
		float e = t * t * (3f - 2f * t);

		// Kenarin uzerinden devrilirken hafif bir yay: ucgen kendi kalinligi
		// yuzunden zemine surtmesin diye.
		float lift = Mathf.Sin(e * Mathf.PI);

		var rot = Quaternion.AngleAxis(180f * e, tumbleAxis);
		me.position = tumblePivot + rot * (tumbleFromPos - tumblePivot) + Vector3.up * (lift * HopHeight);
		me.rotation = rot * tumbleFromRot;

		if (t < 1f)
			return;

		tumbling = false;
		myCx = pendingCx;
		myCy = pendingCy;
		myOri = pendingOri;
		me.position = StateToPos(myLay, myCx, myCy, myOri);
		me.rotation = StateToRot(myOri);

		// Parca yok olmuyor, rengi sana geciyor. Tek renk tasiniyor: yenisini
		// almak eskisini siler.
		if (takeA >= 0)
			carry = pieces[takeA].Tone;
		Swallow(takeA);
		Swallow(takeB);
		TintMe();

		// Tek renk gorevi: ustune bastigin her dogru renk sayiliyor.
		if (objKind == 0 && takeA >= 0 && pieces[takeA].Tone == objTone)
		{
			objGot++;
			if (objGot >= objNeed)
				CompleteTask();
		}

		// Karsi yari ayni karenin diger ucgeni: parca yerinde kalir, ikiniz
		// birlikte tam bir kare olursunuz. Renkler VE dizilis tutmali - rozette
		// sari alt-soldaysa sen de alt-sol yariya basmissin olacaksin.
		int mate = occ[myLay, myCx, myCy, myOri ^ 3] - 1;
		if (objKind == 1 && mate >= 0 && carry == wantA && myOri == wantOri && pieces[mate].Tone == wantB)
		{
			var done = history[history.Count - 1];
			done.Pair = mate;
			history[history.Count - 1] = done;

			Swallow(mate);
			carry = -1;
			TintMe();
			CompleteTask();
		}

		// Kat artik supurerek degil GOREV doldurarak bitiyor. objKind < 0 ise
		// katta kurulabilecek gorev kalmamis demektir, orada tutmanin anlami yok.
		if ((layerTasks >= TasksPerLayer || objKind < 0) && myLay < Layers - 1)
		{
			Dive();
			return;
		}

		// Gorev artik tamamlanamiyorsa yenisi seciliyor. Oyuncunun hicbir
		// hamleyle duzeltemeyecegi bir hedefi ekranda tutmak en kotusu.
		if (objKind >= 0 && !TaskPossible())
			NextTask();

		Finish();
		UpdateHints();
	}

	private void Finish()
	{
		if (myLay == Layers - 1 && (layerTasks >= TasksPerLayer || objKind < 0))
		{
			solved = true;
			return;
		}

		// Gerekmeyen renkler duvar oldugu icin uc kenarin ucu birden kapanabilir.
		// Donmus bir oyun yerine durumu soyluyoruz; Z ile bir onceki hamleye
		// donup baska yoldan gidiliyor.
		for (int m = 0; m < MoveCount; m++)
			if (Landing(myLay, myCx, myCy, myOri, m, out int nx, out int ny, out int no) && Open(nx, ny, no))
				return;

		lost = true;
		lostText = "Sikistin";
	}

	private void CompleteTask()
	{
		tasks++;
		layerTasks++;
		NextTask();
	}

	private void Swallow(int i)
	{
		if (i < 0)
			return;

		occ[pieces[i].Lay, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori] = 0;
		alive[pieces[i].Lay]--;
		popping.Add(i);
		popT.Add(0f);
		Burst(pieces[i].T.position, matPiece[pieces[i].Tone], 9, 5.4f);
	}

	private bool TaskPossible()
	{
		if (objKind == 0)
		{
			int left = 0;
			for (int i = 0; i < pieceCount; i++)
				if (pieces[i].Tone == objTone && pieces[i].Lay == myLay && occ[myLay, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori] == i + 1)
					left++;
			return left >= objNeed - objGot;
		}

		// Kare icin iki sey gerekiyor: tasinacak renge ulasabilmek, ve esinin
		// DOGRU YARIDA durdugu, karsi yarisi bos bir kare bulunmasi.
		bool haveA = carry == wantA;
		bool spot = false;
		for (int i = 0; i < pieceCount; i++)
		{
			if (pieces[i].Lay != myLay || occ[myLay, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori] != i + 1)
				continue;
			if (pieces[i].Tone == wantA)
				haveA = true;
			if (pieces[i].Tone == wantB && pieces[i].Ori == (wantOri ^ 3) && occ[myLay, pieces[i].Cx, pieces[i].Cy, wantOri] == 0)
				spot = true;
		}
		return haveA && spot;
	}

	// Karsi yarisi bos ve basilabilir olan parca: kare ancak boyle bir yerde
	// kurulabilir, gorev de bu yuzden tahtada duran gercek bir yerlesimden
	// uretiliyor. Rastgele bir dizilis istemek cozulemez gorev demekti.
	private bool MateSpot(int i)
	{
		return pieces[i].Lay == myLay
			&& occ[myLay, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori] == i + 1
			&& occ[myLay, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori ^ 3] == 0
			&& reach[myLay, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori ^ 3];
	}

	// Gorev o katta GERCEKTEN duran renklerden kuruluyor; tahtada karsiligi
	// olmayan bir hedef oyuncunun duzeltemeyecegi tek haksizlik olurdu.
	//
	// Iki tip donusumlu degil, rastgele geliyor: hep kare istenirse oyun tek
	// gorevli gibi okunuyor, hep tek renk istenirse birlestirme hic
	// kullanilmiyor.
	private void NextTask()
	{
		int n = 0;
		for (int t = 0; t < PieceTone.Length; t++)
		{
			tonePool[t] = 0;
			for (int i = 0; i < pieceCount; i++)
				if (pieces[i].Tone == t && pieces[i].Lay == myLay && occ[myLay, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori] == i + 1)
					tonePool[t]++;
			if (tonePool[t] > 0)
				toneCand[n++] = t;
		}

		objKind = -1;
		objTone = -1;
		objGot = 0;
		wantA = -1;
		wantB = -1;

		if (n == 0)
			return;

		callSlide = 0f;

		if (n >= 2 && Random.value < 0.5f)
		{
			int spots = 0;
			for (int i = 0; i < pieceCount; i++)
				if (MateSpot(i))
					spots++;

			if (spots > 0)
			{
				int pick = Random.Range(0, spots);
				for (int i = 0; i < pieceCount; i++)
				{
					if (!MateSpot(i) || pick-- > 0)
						continue;

					wantB = pieces[i].Tone;
					wantOri = pieces[i].Ori ^ 3;
					break;
				}

				int alt = 0;
				for (int t = 0; t < PieceTone.Length; t++)
					if (tonePool[t] > 0 && t != wantB)
						alt++;

				if (alt > 0)
				{
					int ia = Random.Range(0, alt);
					for (int t = 0; t < PieceTone.Length; t++)
					{
						if (tonePool[t] == 0 || t == wantB || ia-- > 0)
							continue;

						objKind = 1;
						wantA = t;
						return;
					}
				}

				wantB = -1;
			}
		}

		objKind = 0;
		objTone = toneCand[Random.Range(0, n)];
		objNeed = Mathf.Min(3, tonePool[objTone]);
	}

	// Tasidigin renk ucgenin kendi rengi oluyor: elinde ne oldugunu anlamak
	// icin ustteki cagriya bakmak gerekmesin.
	private void TintMe()
	{
		me.GetComponent<MeshRenderer>().sharedMaterial = carry >= 0 ? matPiece[carry] : matMe;
	}

	// --- inis ---

	// Delige girmek tek basina bitirmiyor: o katta ne biraktiysan seninle beraber
	// asagi iniyor. Butce ortak oldugu icin erken dalmak hamle kazandiriyor ama
	// borcu alta tasiyor; en alt kat butun ihmallerinin toplandigi yer.
	private void Dive()
	{
		int from = myLay;
		int to = myLay + 1;

		dropIdx.Clear();
		dropFrom.Clear();
		dropTo.Clear();

		// Oyuncu once yerlesiyor ki dusen parca onun yarisina konmasin.
		myLay = to;
		activeLay = to;
		layerTasks = 0;

		for (int i = 0; i < pieceCount; i++)
		{
			if (pieces[i].Lay != from || occ[from, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori] != i + 1)
				continue;

			occ[from, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori] = 0;
			alive[from]--;

			if (!FreeSlot(to, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori, out int nx, out int ny, out int no))
			{
				pieces[i].T.gameObject.SetActive(false);
				continue;
			}

			dropIdx.Add(i);
			dropFrom.Add(pieces[i].T.position);
			dropTo.Add(StateToPos(to, nx, ny, no));

			pieces[i].Lay = to;
			pieces[i].Cx = nx;
			pieces[i].Cy = ny;
			pieces[i].Ori = no;
			pieces[i].T.SetParent(layerRoot[to], true);
			pieces[i].T.rotation = StateToRot(no);
			occ[to, nx, ny, no] = i + 1;
			alive[to]++;
		}

		// Indigin katin sisi kalkiyor: ustunde durdugun kat her zaman net.
		fogSheet[to].gameObject.SetActive(false);

		meDropFrom = me.position;
		meDropTo = StateToPos(to, myCx, myCy, myOri);
		dropLay = from;
		dropT = 0f;
		dropping = true;

		for (int i = 0; i < MoveCount; i++)
			hints[i].gameObject.SetActive(false);

		// Yeni katta hangi renkler var belli degil; gorev bastan seciliyor.
		NextTask();
	}

	// Dusen parca once kendi koordinatina bakiyor: biraktigin yerde seni bekliyor.
	// Dolu ya da ulasilamazsa en yakin uygun yariya kayiyor.
	private bool FreeSlot(int lay, int cx, int cy, int ori, out int nx, out int ny, out int no)
	{
		nx = 0;
		ny = 0;
		no = 0;

		for (int r = 0; r < gridN; r++)
		{
			for (int dx = -r; dx <= r; dx++)
			{
				for (int dy = -r; dy <= r; dy++)
				{
					if (r > 0 && Mathf.Abs(dx) != r && Mathf.Abs(dy) != r)
						continue;

					int x = cx + dx;
					int y = cy + dy;
					if (!Shape(lay, x, y))
						continue;

					for (int k = 0; k < 4; k++)
					{
						// Once ayni yonelim denensin, sonra otekiler.
						int o = (ori + k) & 3;
						if (!reach[lay, x, y, o])
							continue;
						if (IsHole(lay, x, y, o))
							continue;
						if (x == myCx && y == myCy && o == myOri)
							continue;
						if (occ[lay, x, y, o] != 0 || occ[lay, x, y, o ^ 1] != 0 || occ[lay, x, y, o ^ 2] != 0)
							continue;

						nx = x;
						ny = y;
						no = o;
						return true;
					}
				}
			}
		}
		return false;
	}

	private void StepDrop()
	{
		dropT += Time.deltaTime / Mathf.Max(0.05f, DropTime);
		float t = Mathf.Clamp01(dropT);
		float e = t * t;

		me.position = Vector3.Lerp(meDropFrom, meDropTo, e);
		for (int i = 0; i < dropIdx.Count; i++)
			pieces[dropIdx[i]].T.position = Vector3.Lerp(dropFrom[i], dropTo[i], e);

		// Temizlenen kat eriyerek gidiyor: ilerleme sayacta degil, gozunun onunde.
		float s = 1f - Mathf.Clamp01(t * 1.25f);
		layerRoot[dropLay].localScale = new Vector3(s, s, s);

		if (t < 1f)
			return;

		dropping = false;
		layerRoot[dropLay].gameObject.SetActive(false);
		me.position = meDropTo;
		Finish();
		UpdateHints();
	}

	// Inis geri alinamiyor: dusen parcalar yeni yerlerine dagiliyor ve eski
	// dizilimi geri kurmak katin tamamini saklamayi gerektirir. Kat icinde
	// serbest, kat atladiktan sonra R ile bastan.
	private void Undo()
	{
		if (history.Count == 0 || tumbling || dropping || solved)
			return;

		var mv = history[history.Count - 1];
		if (mv.Lay != myLay || mv.Dove)
			return;

		history.RemoveAt(history.Count - 1);
		Revive(mv.A);
		Revive(mv.B);
		Revive(mv.Pair);

		carry = mv.Carry;
		wantA = mv.WantA;
		wantB = mv.WantB;
		tasks = mv.Tasks;
		objKind = mv.Kind;
		objTone = mv.Tone;
		objNeed = mv.Need;
		objGot = mv.Got;
		wantOri = mv.WOri;
		layerTasks = mv.LTasks;
		callSlide = 1f;
		TintMe();

		myCx = mv.Cx;
		myCy = mv.Cy;
		myOri = mv.Ori;
		me.position = StateToPos(myLay, myCx, myCy, myOri);
		me.rotation = StateToRot(myOri);
		steps--;
		lost = false;
		UpdateHints();
	}

	private void Revive(int i)
	{
		if (i < 0)
			return;

		int at = popping.IndexOf(i);
		if (at >= 0)
		{
			popping.RemoveAt(at);
			popT.RemoveAt(at);
		}
		pieces[i].T.localScale = Vector3.one;
		pieces[i].T.gameObject.SetActive(true);
		occ[pieces[i].Lay, pieces[i].Cx, pieces[i].Cy, pieces[i].Ori] = i + 1;
		alive[pieces[i].Lay]++;
	}

	// --- gorsel ---

	private void UpdateHints()
	{
		if (hints == null)
			return;

		bool live = Hints && !tumbling && !dropping && !solved && !lost;
		for (int m = 0; m < MoveCount; m++)
		{
			int nx = 0;
			int ny = 0;
			int no = 0;
			bool ok = live && Landing(myLay, myCx, myCy, myOri, m, out nx, out ny, out no) && Open(nx, ny, no);
			hints[m].gameObject.SetActive(ok);
			if (!ok)
				continue;

			bool take = occ[myLay, nx, ny, no] != 0 || occ[myLay, nx, ny, no ^ 1] != 0 || occ[myLay, nx, ny, no ^ 2] != 0;
			hints[m].GetComponent<MeshRenderer>().sharedMaterial = take ? matHintTake : matHint;
			// Isaret zeminin hemen uzerine yapisiyor; havada asili kalirsa yine cisim gibi okunuyor.
			hints[m].position = StateToPos(myLay, nx, ny, no) + Vector3.down * (TriThick * 0.5f - 0.006f);
			hints[m].rotation = StateToRot(no);
		}
	}

	private void Burst(Vector3 at, Material mat, int count, float force)
	{
		for (int i = 0; i < count; i++)
		{
			int k = shardAt;
			shardAt = (shardAt + 1) % shards.Length;

			float size = UnityEngine.Random.Range(0.07f, 0.16f);
			var t = shards[k].T;
			t.gameObject.SetActive(true);
			t.position = at;
			t.rotation = UnityEngine.Random.rotation;
			t.localScale = Vector3.one * size;
			t.GetComponent<MeshRenderer>().sharedMaterial = mat;

			Vector3 dir = (Vector3.up * UnityEngine.Random.Range(0.5f, 1.2f) + UnityEngine.Random.insideUnitSphere).normalized;
			shards[k].Vel = dir * (force * UnityEngine.Random.Range(0.6f, 1.4f));
			shards[k].Spin = UnityEngine.Random.insideUnitSphere * 720f;
			shards[k].Size = size;
			shards[k].Max = UnityEngine.Random.Range(0.45f, 0.85f);
			shards[k].Life = shards[k].Max;
		}
	}

	private void StepShards()
	{
		if (shards == null)
			return;

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
			var tr = pieces[popping[i]].T;
			tr.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(t * Mathf.PI)) * (1f - t);
			if (t < 1f)
				continue;
			tr.gameObject.SetActive(false);
			popping.RemoveAt(i);
			popT.RemoveAt(i);
		}
	}

	// --- kamera ---

	private void SetupCamera()
	{
		camScreen = new Vector2Int(Screen.width, Screen.height);
		cam.fieldOfView = 42f;
		UpdateCamera();
	}

	private void UpdateCamera()
	{
		float p = CamPitch * Mathf.Deg2Rad;
		float vt = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
		float span = gridN * 0.5f;
		float stack = (Layers - 1) * LayerGap;

		// Egik bakista tahtanin derinligi ekranda sin(egim) kadar, yiginin
		// yuksekligi ise cos(egim) kadar yer kapliyor; ikisini de sigdiriyoruz.
		//
		// Dik telefon ekraninda her zaman needH kazanir: tahta ekranin iki
		// kenarina tam deger, bos kalan yer altta ve ustte kalir. Yani CamZoom
		// 1'in altina inmeden daha fazla yakinlasmanin yolu yok, inince de
		// tahtanin kenarlari kirpilir. Sabit pay bu yuzden kucuk tutuluyor;
		// 1.5 iken telefonda tahta gereksiz yere uzak duruyordu.
		float needH = span / (vt * Mathf.Max(0.2f, cam.aspect));
		float needV = (span * Mathf.Sin(p) + stack * 0.5f * Mathf.Cos(p)) / vt;
		float dist = Mathf.Max(needH, needV) * CamZoom + 0.5f;

		focusY = Mathf.SmoothDamp(focusY, LayerY(activeLay), ref focusVelY, 0.25f);
		Vector3 focus = new Vector3(0f, focusY, 0f);
		Vector3 back = new Vector3(0f, Mathf.Sin(p), -Mathf.Cos(p));
		cam.transform.position = focus + back * dist;
		cam.transform.LookAt(focus);
	}

	// --- arayuz ---

	private void OnGUI()
	{
		float s = Mathf.Max(Screen.width, Screen.height) / 900f;
		var big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(21f * s) };
		var huge = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(38f * s), alignment = TextAnchor.MiddleCenter };

		int left = 0;
		for (int l = 0; l < Layers; l++)
			left += alive[l];

		GUI.Label(new Rect(16f * s, 10f * s, 700f * s, 32f * s),
			"Gorev: " + layerTasks + " / " + TasksPerLayer + "     Toplam: " + tasks + "     Hamle: " + steps, big);
		GUI.Label(new Rect(16f * s, 40f * s, 700f * s, 32f * s),
			"Kat " + (myLay + 1) + " / " + Layers + "     Bu katta: " + alive[myLay] + "     Kalan: " + left, big);
		if (myLay < Layers - 1)
			GUI.Label(new Rect(16f * s, 70f * s, 700f * s, 32f * s), "Gorevler bitince zemin cekiliyor, asagi iniyorsun.", big);

		// Gorev yandan kayarak giriyor: degistigi an goz sayiya degil harekete
		// takiliyor. Tek renk gorevi tek ucgen, kare gorevi iki yarim.
		if (objKind >= 0 && !solved && !lost)
		{
			float q = 68f * s;
			float e = 1f - (1f - callSlide) * (1f - callSlide);
			float bx = Mathf.Lerp(Screen.width + 40f * s, (Screen.width - q) * 0.5f, e);
			float by = 106f * s;
			var r = new Rect(bx, by, q, q);

			var oc = GUI.color;
			if (objKind == 1)
			{
				GUI.color = PieceTone[wantA];
				GUI.DrawTexture(r, halfTex[wantOri]);
				GUI.color = PieceTone[wantB];
				GUI.DrawTexture(r, halfTex[wantOri ^ 3]);
			}
			else
			{
				GUI.color = PieceTone[objTone];
				GUI.DrawTexture(r, upTex);
			}
			GUI.color = oc;

			GUI.Label(new Rect(bx + q + 10f * s, by + 20f * s, 320f * s, 40f * s),
				objKind == 1 ? "kareyi yap" : objGot + " / " + objNeed, big);
		}

		if (solved)
			GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 60f * s), "TEMIZ  -  " + tasks + " gorev  /  " + steps + " hamle", huge);
		else if (lost)
			GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 60f * s), lostText + "  -  Z geri al, R bastan", huge);

		float pw = 300f * s;
		panelLeft = Screen.width - pw - 10f * s;
		if (GUI.Button(new Rect(Screen.width - 110f * s, 10f * s, 100f * s, 32f * s), showPanel ? "Kapat" : "Ayar"))
			showPanel = !showPanel;

		if (!showPanel)
			return;

		GUI.Box(new Rect(panelLeft, 50f * s, pw, Screen.height - 66f * s), "");
		panelScroll = GUI.BeginScrollView(
			new Rect(panelLeft + 8f * s, 58f * s, pw - 16f * s, Screen.height - 82f * s),
			panelScroll,
			new Rect(0f, 0f, pw - 44f * s, 1120f * s));

		float x = 0f;
		float y = 0f;
		float w = pw - 48f * s;

		SliderInt(ref Layers, 2, FloorTone.Length, "Kat sayisi", x, ref y, w, s, true);
		SliderInt(ref TopRadius, 1, 5, "Ust kat yaricapi", x, ref y, w, s, true);
		SliderInt(ref RadiusStep, 1, 2, "Yaricap artisi", x, ref y, w, s, true);
		Slider(ref PieceRatio, 0.05f, 0.6f, "Parca yogunlugu", x, ref y, w, s, true);
		SliderInt(ref TasksPerLayer, 1, 8, "Kat basina gorev", x, ref y, w, s, true);
		Slider(ref LayerGap, 0.2f, 1.6f, "Kat araligi", x, ref y, w, s, true);
		Slider(ref SlabRatio, 0.1f, 1.6f, "Kat govde yuksekligi", x, ref y, w, s, true);
		Slider(ref FogAmount, 0f, 0.93f, "En alt kat sisi", x, ref y, w, s, true);
		Slider(ref HintSize, 0.12f, 0.9f, "Yonerge boyutu", x, ref y, w, s, true);
		Slider(ref TapRadius, 0.03f, 0.2f, "Dokunus yaricapi", x, ref y, w, s, false);
		Slider(ref TumbleTime, 0.05f, 0.4f, "Takla suresi", x, ref y, w, s, false);
		Slider(ref DropTime, 0.15f, 1.2f, "Inis suresi", x, ref y, w, s, false);
		Slider(ref HopHeight, 0f, 0.3f, "Takla yayi", x, ref y, w, s, false);
		Slider(ref TriThick, 0.04f, 0.3f, "Ucgen kalinlik", x, ref y, w, s, true);
		Slider(ref Inset, 0f, 0.25f, "Ucgen bosluk", x, ref y, w, s, true);
		Slider(ref FloorInset, 0f, 0.25f, "Zemin bosluk", x, ref y, w, s, true);
		Slider(ref CamPitch, 20f, 85f, "Kamera egimi", x, ref y, w, s, false);
		Slider(ref CamZoom, 0.6f, 2f, "Kamera uzakligi", x, ref y, w, s, false);

		bool nh = GUI.Toggle(new Rect(x, y, w, 24f * s), Hints, " Yonerge ucgenleri");
		if (nh != Hints)
		{
			Hints = nh;
			SavePrefs();
			UpdateHints();
		}
		y += 34f * s;

		GUI.Label(new Rect(x, y, w, 24f * s), "Tohum: " + seed);
		y += 28f * s;
		if (GUI.Button(new Rect(x, y, w, 30f * s), "Yeni tahta (N)"))
		{
			seed++;
			SavePrefs();
			Build();
		}
		y += 36f * s;
		if (GUI.Button(new Rect(x, y, w, 30f * s), "Bastan (R)"))
			Build();
		y += 36f * s;
		if (GUI.Button(new Rect(x, y, w, 30f * s), "Varsayilana don"))
		{
			PlayerPrefs.DeleteKey("stk_ver");
			PlayerPrefs.Save();
			Layers = 3;
			TopRadius = 2;
			RadiusStep = 1;
			LayerGap = 0.82f;
			SlabRatio = 1.15f;
			FogAmount = 0.8f;
			HintSize = 0.42f;
			PieceRatio = 0.26f;
			TasksPerLayer = 3;
			Build();
		}
		y += 42f * s;

		GUI.Label(new Rect(x, y, w, 24f * s), "Nisana dokun (telefonda da ayni)");
		y += 24f * s;
		GUI.Label(new Rect(x, y, w, 24f * s), "Parcaya basinca rengini alirsin");
		y += 24f * s;
		GUI.Label(new Rect(x, y, w, 24f * s), "Gerekmeyen renkler duvar, etrafindan dolas");
		y += 24f * s;
		GUI.Label(new Rect(x, y, w, 24f * s), "Karede rozetteki dizilise bas");
		y += 24f * s;
		GUI.Label(new Rect(x, y, w, 24f * s), "Z: geri al (kat icinde)");

		GUI.EndScrollView();
	}

	private void Slider(ref float v, float lo, float hi, string label, float x, ref float y, float w, float s, bool rebuild)
	{
		GUI.Label(new Rect(x, y, w, 24f * s), label + ": " + v.ToString("0.00"));
		y += 26f * s;
		float nv = GUI.HorizontalSlider(new Rect(x, y + 8f * s, w, 20f * s), v, lo, hi);
		if (!Mathf.Approximately(nv, v))
		{
			v = nv;
			SavePrefs();
			if (rebuild)
				Build();
		}
		y += 38f * s;
	}

	private void SliderInt(ref int v, int lo, int hi, string label, float x, ref float y, float w, float s, bool rebuild)
	{
		GUI.Label(new Rect(x, y, w, 24f * s), label + ": " + v);
		y += 26f * s;
		int nv = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x, y + 8f * s, w, 20f * s), v, lo, hi));
		if (nv != v)
		{
			v = nv;
			SavePrefs();
			if (rebuild)
				Build();
		}
		y += 38f * s;
	}

	private void LoadPrefs()
	{
		if (PlayerPrefs.GetInt("stk_ver", -1) != DefaultsVersion)
			return;

		Layers = PlayerPrefs.GetInt("stk_lay", Layers);
		TopRadius = PlayerPrefs.GetInt("stk_r0", TopRadius);
		RadiusStep = PlayerPrefs.GetInt("stk_rs", RadiusStep);
		PieceRatio = PlayerPrefs.GetFloat("stk_pr", PieceRatio);
		TasksPerLayer = PlayerPrefs.GetInt("stk_tpl", TasksPerLayer);
		LayerGap = PlayerPrefs.GetFloat("stk_gap", LayerGap);
		SlabRatio = PlayerPrefs.GetFloat("stk_slab", SlabRatio);
		FogAmount = PlayerPrefs.GetFloat("stk_fog", FogAmount);
		HintSize = PlayerPrefs.GetFloat("stk_hsz", HintSize);
		TapRadius = PlayerPrefs.GetFloat("stk_tap", TapRadius);
		TumbleTime = PlayerPrefs.GetFloat("stk_tt", TumbleTime);
		DropTime = PlayerPrefs.GetFloat("stk_dt", DropTime);
		HopHeight = PlayerPrefs.GetFloat("stk_hop", HopHeight);
		TriThick = PlayerPrefs.GetFloat("stk_th", TriThick);
		Inset = PlayerPrefs.GetFloat("stk_in", Inset);
		FloorInset = PlayerPrefs.GetFloat("stk_fin", FloorInset);
		CamPitch = PlayerPrefs.GetFloat("stk_cp", CamPitch);
		CamZoom = PlayerPrefs.GetFloat("stk_cz", CamZoom);
		Hints = PlayerPrefs.GetInt("stk_hint", Hints ? 1 : 0) == 1;
		seed = PlayerPrefs.GetInt("stk_seed", seed);
	}

	private void SavePrefs()
	{
		PlayerPrefs.SetInt("stk_ver", DefaultsVersion);
		PlayerPrefs.SetInt("stk_lay", Layers);
		PlayerPrefs.SetInt("stk_r0", TopRadius);
		PlayerPrefs.SetInt("stk_rs", RadiusStep);
		PlayerPrefs.SetFloat("stk_pr", PieceRatio);
		PlayerPrefs.SetInt("stk_tpl", TasksPerLayer);
		PlayerPrefs.SetFloat("stk_gap", LayerGap);
		PlayerPrefs.SetFloat("stk_slab", SlabRatio);
		PlayerPrefs.SetFloat("stk_fog", FogAmount);
		PlayerPrefs.SetFloat("stk_hsz", HintSize);
		PlayerPrefs.SetFloat("stk_tap", TapRadius);
		PlayerPrefs.SetFloat("stk_tt", TumbleTime);
		PlayerPrefs.SetFloat("stk_dt", DropTime);
		PlayerPrefs.SetFloat("stk_hop", HopHeight);
		PlayerPrefs.SetFloat("stk_th", TriThick);
		PlayerPrefs.SetFloat("stk_in", Inset);
		PlayerPrefs.SetFloat("stk_fin", FloorInset);
		PlayerPrefs.SetFloat("stk_cp", CamPitch);
		PlayerPrefs.SetFloat("stk_cz", CamZoom);
		PlayerPrefs.SetInt("stk_hint", Hints ? 1 : 0);
		PlayerPrefs.SetInt("stk_seed", seed);
		PlayerPrefs.Save();
	}
}
