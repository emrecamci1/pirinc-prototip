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
// PARMAGINI BASILI TUT, UCGEN O YONE AKAR. Yon her karede, hamle secimi de her
// varista yeniden okunuyor: oyuncu rota gondermiyor, direksiyon tutuyor.
// Onceden hedefe dokunulup en kisa yol oynatiliyordu; hareket oyuncunun degil
// animasyonun oluyordu ve aradaki yurume olu zamandi.
//
// Ucgen kenarindan DEVRILMIYOR artik, kayiyor: agirlik merkezi iki hucre
// arasinda duz bir cizgide sabit hizla gidiyor, govde de yol boyunca kendi
// merkezi etrafinda yeni yonune donuyor. Takla, 180 derecelik donus karsiligi
// yarim hucre ilerledigi ve her adim sifirda baslayip sifirda bittigi icin
// akmiyordu; sorun egri degil, geometriydi.
//
// GEREKMEYEN PARCANIN USTUNDEN GECERSIN. Yok olmaz, yolunu da kesmez: ucgen
// onun ustune binip devam eder (bir TriThick yukarida durur). Boylece ayni
// yariya iki ucgen girmiyor, biri digerinin ustunde duruyor.
//
// Once bu parcalar basilinca kalkiyordu ve oyun kendi kendini bitiriyordu;
// sonra duvar yapildi ve bu sefer yol kesiyordu. Ikisi de yanlisti - parca
// sadece GOREVIN ISTEDIGI renkse kalkiyor, geri kalani yerinde kaliyor ve
// ileride kendi gorevi geldiginde kullaniliyor.
//
// Kat ancak tamamen BOSALINCA iniliyor: her kata konan her renk kullanilacak.
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
	private const int DefaultsVersion = 14;

	// Ekranin sag ustunde yazar: telefonda hangi surumu oynadigin belli olsun diye.
	// Her itilen degisiklikte bir artar ve ayni numara git etiketi olarak atilir,
	// yani "v6 daha iyiydi" demek o surume birebir donmeye yetiyor. SURUMLER.md
	// hangi numarada ne degistigini tutuyor.
	private const string Version = "v5";

	// Yonelim = dik acinin hucre icinde hangi kosede oldugu.
	// bit0: 0 = sol, 1 = sag     bit1: 0 = alt, 1 = ust
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

	// Parca tahtada isik altinda duruyor, rozet ise duz bir damga. Ham albedo
	// cizmek rozeti tahtadakinden farkli gosteriyordu ve hangi rozetin hangi
	// parca oldugu anlasilmiyordu.
	//
	// Ust yuze dusen toplam carpan, kanal basina:
	//   gunes   1.05 * sin(52) = 0.827          (beyaz)
	//   dolgu   0.38 * sin(26) = 0.167          x (0.70, 0.79, 1.00)
	//   gok ortami                              = (0.64, 0.68, 0.76)
	// Renk uzayi Gamma oldugu icin carpan dogrudan sRGB degeri uzerinde isliyor.
	// ISIK DEGERLERI DEGISIRSE BURASI DA DEGISMELI.
	private static readonly Color LitMul = new Color(1.584f, 1.639f, 1.754f);

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
	// Birim/saniye. Adimlarin uzunlugu farkli (bacak 0.62, hipotenus 0.44) ama
	// hiz ayni: zincir boyunca tempo sabit kaliyor, hucre sinirinda tokluk yok.
	[System.NonSerialized] public float MoveSpeed = 4.2f;
	[System.NonSerialized] public float HopHeight = 0.03f;
	[System.NonSerialized] public float PopTime = 0.22f;
	[System.NonSerialized] public float DropTime = 0.5f;
	[System.NonSerialized] public float TriThick = 0.11f;
	[System.NonSerialized] public float Bevel = 0.013f;
	[System.NonSerialized] public bool Shiny = true;
	private float FloorThick;
	[System.NonSerialized] public float Inset = 0.07f;
	[System.NonSerialized] public float FloorInset = 0.11f;
	[System.NonSerialized] public float CamPitch = 52f;
	[System.NonSerialized] public float CamZoom = 1f;
	[System.NonSerialized] public bool Hints = true;
	// Parmak ucgene bu kadar yakinsa yon okunmuyor: oyuncu kendi uzerine
	// basarak duruyor.
	private const float DeadZone = 0.5f;

	// Hizin sifirdan tama cikis ve inis suresi. Yumusama ADIM BASINA degil
	// zincirin ucunda: ilk adim yumusak baslar, son adim yumusak biter,
	// aradakilerin hepsi tam hizda. Akiskanlik tam olarak buradan geliyor.
	private const float FlowUp = 0.10f;
	private const float FlowDown = 0.08f;

	// Donusun ust siniri, derece/saniye. Bacak adimi 90, hipotenus adimi 180
	// derece donuyor ama hipotenus YOLU daha kisa: sirf mesafeye gore surulse
	// kisa adim iki kati aciyi yariya yakin surede donerdi ve o adim cirpinma
	// gibi okunurdu. Sinir ikisinin suresini de ~0.15 saniyede bulusturuyor.
	private const float SpinSpeed = 1200f;

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
		public int Tasks, Kind, Tone, Need, Got, WOri;
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

	// Agirlik merkezi yerelde daima burasi: ucler iceri alinirken merkeze dogru
	// lerplendigi icin bosluk ayari merkezi kaydirmiyor.
	private static readonly Vector3 CLocal = new Vector3(1f / 3f, 0f, 1f / 3f);

	private bool moving;
	private float glideT;
	private float stepDur;
	private Vector3 glideFromC, glideToC;
	private Quaternion glideFromRot, glideToRot;
	private float glideFromLift, glideToLift;
	private float flow;
	private Vector3 steer;
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
	private float callSlide = 1f;
	private readonly int[] tonePool = new int[PieceTone.Length];
	private readonly int[] toneCand = new int[PieceTone.Length];
	private Texture2D[] halfTex;
	private Texture2D[] edgeTex;
	private Texture2D upTex;
	private bool solved;
	private bool lost;
	private string lostText = "";

	private float focusY;
	private float focusVelY;
	private Vector2Int camScreen;
	private bool held;
	private Vector2 heldPos;
	private bool showPanel;
	private Vector2 panelScroll;
	private float panelContent;
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
		// Her yarimin bir de kenar damgasi var: ici bos cizilen yarim icin.
		halfTex = new Texture2D[4];
		edgeTex = new Texture2D[4];
		for (int k = 0; k < 4; k++)
		{
			var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
			var rim = new Texture2D(64, 64, TextureFormat.RGBA32, false);
			tex.filterMode = FilterMode.Bilinear;
			rim.filterMode = FilterMode.Bilinear;
			var px = new Color32[64 * 64];
			var rpx = new Color32[64 * 64];
			for (int ty = 0; ty < 64; ty++)
			{
				for (int tx = 0; tx < 64; tx++)
				{
					bool on = InHalf(k, tx, ty);
					px[ty * 64 + tx] = on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
					rpx[ty * 64 + tx] = on && OnRim(k, tx, ty) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
				}
			}
			tex.SetPixels32(px);
			tex.Apply();
			halfTex[k] = tex;
			rim.SetPixels32(rpx);
			rim.Apply();
			edgeTex[k] = rim;
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
			// Siddetler bilerek yuksek: ust yuze dusen toplam carpan ~1.6, yani
			// parca tahtada kendi renginden parlak goruluyor ve doygun renkler
			// beyaza dogru kayiyor. Bu bir kez 1.0a kalibre edildi (o zaman tahta
			// rengi gorev rozetiyle birebir esitleniyor) ama parlak hali daha iyi
			// bulundu ve geri alindi. Rozetle tahtanin tam ayni renk olmamasi
			// kabul edilmis bir sonuc, duzeltilecek bir hata degil.
			var lg = new GameObject("Sun").AddComponent<Light>();
			lg.type = LightType.Directional;
			lg.intensity = 1.05f;
			lg.transform.rotation = Quaternion.Euler(52f, 36f, 0f);
			// Golge olmadan ust kat alt katin uzerine oturmuyor, havada duruyor.
			lg.shadows = LightShadows.Soft;
			lg.shadowStrength = 0.62f;

			// Karsi yondan soguk bir dolgu. Tek isikta golgede kalan yuzler
			// tamamen duzlesiyor ve parcanin yan yuzu govdesinden ayirt
			// edilemiyor; hacim esas iki isigin farkindan okunuyor.
			var fill = new GameObject("Fill").AddComponent<Light>();
			fill.type = LightType.Directional;
			fill.intensity = 0.38f;
			fill.color = new Color(0.70f, 0.79f, 1f);
			fill.transform.rotation = Quaternion.Euler(26f, -142f, 0f);
			fill.shadows = LightShadows.None;
		}

		// Tek renk ortam isigi her yuzu esit aydinlatip hacmi siliyordu. Ustten
		// acik, altta koyu bir degrade pahlari kendiliginden ayiriyor.
		RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
		RenderSettings.ambientSkyColor = new Color(0.64f, 0.68f, 0.76f);
		RenderSettings.ambientEquatorColor = new Color(0.50f, 0.51f, 0.55f);
		RenderSettings.ambientGroundColor = new Color(0.32f, 0.31f, 0.34f);

		Build();
		SetupCamera();
	}

	private Material MakeMat(Color c)
	{
		// TriLit Resources altinda: sahne calisma aninda kuruldugu icin hicbir
		// materyal onu referans etmiyor ve normalde derlemede silinirdi.
		Shader sh = Shiny ? Resources.Load<Shader>("TriLit") : null;
		if (sh == null)
			sh = Shader.Find("Legacy Shaders/Diffuse");
		if (sh == null)
			sh = Shader.Find("Diffuse");
		if (sh == null)
			sh = Shader.Find("Unlit/Color");

		var m = new Material(sh);
		m.color = c;
		return m;
	}

	// Rozet rengi: parcanin isik altindaki hali. Kirpilma da bilerek aynen
	// birakiliyor, tahtada da ayni sekilde kirpiliyor.
	private static Color Swatch(int tone)
	{
		Color c = PieceTone[tone];
		return new Color(
			Mathf.Min(1f, c.r * LitMul.r),
			Mathf.Min(1f, c.g * LitMul.g),
			Mathf.Min(1f, c.b * LitMul.b));
	}

	private static bool InHalf(int k, int tx, int ty)
	{
		switch (k)
		{
			case 0: return tx + ty <= 61;
			case 1: return ty <= tx - 2;
			case 2: return ty >= tx + 2;
			default: return tx + ty >= 65;
		}
	}

	private static bool OnRim(int k, int tx, int ty)
	{
		const int b = 8;
		switch (k)
		{
			case 0: return tx <= b || ty <= b || tx + ty >= 61 - b;
			case 1: return ty <= b || tx >= 63 - b || ty >= tx - 2 - b;
			case 2: return tx <= b || ty >= 63 - b || ty <= tx + 2 + b;
			default: return tx >= 63 - b || ty >= 63 - b || tx + ty <= 65 + b;
		}
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

	// Pahli ucgen prizma. Keskin kenar isigi hic yakalamiyor: kenar bir cizgi
	// olarak degil, iki yuzun ani rengi degismesi olarak okunuyordu ve yigin
	// duz bir leke gibi goruluyordu. Ince bir pah o kenari isiga doner, yuzeyin
	// nerede bittigi gorulur.
	private static Mesh BuildTriMesh(float inset, float thick, float bevel)
	{
		// Dik aci yerelde (0,0), bacaklar +X ve +Z boyunca.
		Vector3 a = new Vector3(0f, 0f, 0f);
		Vector3 b = new Vector3(1f, 0f, 0f);
		Vector3 c = new Vector3(0f, 0f, 1f);
		Vector3 m0 = (a + b + c) / 3f;
		a = Vector3.LerpUnclamped(a, m0, inset);
		b = Vector3.LerpUnclamped(b, m0, inset);
		c = Vector3.LerpUnclamped(c, m0, inset);

		// Ic kenarin merkeze dogru kaymasi ICTEGET uzerinden: bir ucgeni her
		// kenarindan esit mesafe iceri almanin tek dogru yolu bu. Agirlik
		// merkezine lerp etmek dar acida daha fazla, genis acida daha az
		// kaydirirdi ve pah genisligi kenardan kenara degisirdi.
		float la = Vector3.Distance(b, c);
		float lb = Vector3.Distance(c, a);
		float lc = Vector3.Distance(a, b);
		float per = la + lb + lc;
		Vector3 inc = (la * a + lb * b + lc * c) / per;
		float rad = Vector3.Cross(b - a, c - a).magnitude / per;

		// Pah ne kalinligi ne de yuzeyi yiyebilir: ince yonergelerde (0.02)
		// kalinlik, kucuk parcalarda ictegetin kendisi sinirliyor.
		float bv = Mathf.Min(bevel, thick * 0.4f, rad * 0.5f);
		bv = Mathf.Max(bv, 0f);

		float k = rad > 0f ? (rad - bv) / rad : 0f;
		Vector3 ai = Vector3.LerpUnclamped(inc, a, k);
		Vector3 bi = Vector3.LerpUnclamped(inc, b, k);
		Vector3 ci = Vector3.LerpUnclamped(inc, c, k);

		// Kalinlik yuzeyin iki yanina esit dagiliyor: 180 derecelik takladan sonra
		// ucgen yine duzgun oturuyor, zeminin icine gomulmuyor.
		float h = thick * 0.5f;
		Vector3 top = Vector3.up * h;
		Vector3 bot = Vector3.down * h;
		Vector3 topIn = Vector3.up * (h - bv);
		Vector3 botIn = Vector3.down * (h - bv);

		var verts = new List<Vector3>();
		var norms = new List<Vector3>();
		var tris = new List<int>();

		AddFace(verts, norms, tris, ai + top, ci + top, bi + top, Vector3.up);
		AddFace(verts, norms, tris, ai + bot, bi + bot, ci + bot, Vector3.down);

		Vector3[] loop = { a, b, c };
		Vector3[] inner = { ai, bi, ci };
		for (int i = 0; i < 3; i++)
		{
			int j = (i + 1) % 3;
			Vector3 p = loop[i];
			Vector3 q = loop[j];
			Vector3 pi = inner[i];
			Vector3 qi = inner[j];
			Vector3 nrm = Vector3.Cross(Vector3.up, q - p).normalized;

			// Yan yuz, pahlarin arasinda kalan bant.
			AddFace(verts, norms, tris, p + botIn, q + topIn, q + botIn, nrm);
			AddFace(verts, norms, tris, p + botIn, p + topIn, q + topIn, nrm);

			if (bv <= 0f)
				continue;

			// Pah yuzu kendi duz normalini aliyor: yumusatilsaydi kenar yine
			// kaybolurdu, oysa istenen tam olarak o ince parlak serit.
			Vector3 nt = (nrm + Vector3.up).normalized;
			AddFace(verts, norms, tris, p + topIn, pi + top, qi + top, nt);
			AddFace(verts, norms, tris, p + topIn, qi + top, q + topIn, nt);

			Vector3 nb = (nrm + Vector3.down).normalized;
			AddFace(verts, norms, tris, p + botIn, qi + bot, pi + bot, nb);
			AddFace(verts, norms, tris, p + botIn, q + botIn, qi + bot, nb);
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

	// Parcalar engel degil, hedef: uzerine devriliyorsun ve kalkiyorlar. Bu yuzden
	// tek sinir siluetin kendisi. Hipotenus taklasi hep ayni hucrede kaldigi icin
	// de her zaman en az bir hamle var: oyun asla kilitlenmiyor.
	private bool Landing(int lay, int cx, int cy, int ori, int m, out int nx, out int ny, out int no)
	{
		Target(cx, cy, ori, m, out nx, out ny, out no);
		return Shape(lay, nx, ny);
	}

	// O yaride kaldirilacak parca var mi. Kare gorevinde sadece TASINACAK renk
	// kalkiyor; esi yerinde kalmali, yoksa oyuncu kendi hedefini yolda yiyor.
	// Gorevin istemedigi parca kalkmiyor ama yol da kesmiyor, ustune biniliyor.
	private int Pick(int nx, int ny, int no)
	{
		int i = occ[myLay, nx, ny, no] - 1;
		if (i < 0)
			return -1;

		bool want = objKind == 0 ? pieces[i].Tone == objTone : pieces[i].Tone == wantA;
		return want ? i : -1;
	}

	// Ustune binilen parca duruyor demek; iki ucgen ayni yukseklikte ust uste
	// durursa z-fighting basliyor.
	private Vector3 MePos(int lay, int cx, int cy, int ori)
	{
		Vector3 p = StateToPos(lay, cx, cy, ori);
		if (occ[lay, cx, cy, ori] != 0)
			p.y += TriThick;
		return p;
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
		triMesh = BuildTriMesh(Inset, TriThick, Bevel);
		FloorThick = LayerGap * SlabRatio;
		floorMesh = BuildTriMesh(FloorInset, FloorThick, Bevel);
		// Yonerge zemine CIZILMIS bir isaret, ortada duran bir cisim degil: ince ve
		// iceri cekik. Karo meshini paylassalardi kat govdesi kadar kalinlasir ve
		// oyuncunun ucgeninden ayirt edilemezlerdi.
		hintMesh = BuildTriMesh(1f - HintSize, 0.02f, Bevel);

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
		me.position = MePos(myLay, myCx, myCy, myOri);
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
		carry = -1;
		objKind = -1;
		objTone = -1;
		wantA = -1;
		wantB = -1;
		solved = false;
		lost = false;
		lostText = "";
		moving = false;
		flow = 0f;
		steer = Vector3.zero;
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

		// Birakma anini beklemek yok: parmak BASILI oldugu surece yon veriyor.
		// Panel acikken sagdaki serit paneldir; sag ust kose de Ayar dugmesiyle
		// surum yazisinindir. Ikisinde de yon okunmuyor, yoksa paneli acmak
		// ucgeni bir adim seyirtiyor.
		float gs = Mathf.Max(Screen.width, Screen.height) / 900f;
		held = Input.GetMouseButton(0)
			&& (!showPanel || Input.mousePosition.x < panelLeft)
			&& !(Input.mousePosition.x > Screen.width - 115f * gs
				&& Input.mousePosition.y > Screen.height - 80f * gs);
		heldPos = Input.mousePosition;

		if (dropping)
		{
			StepDrop();
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
		{
			flow = 0f;
			return;
		}

		steer = ReadSteer();
		bool want = steer.sqrMagnitude > 0.0001f;
		flow = Mathf.MoveTowards(flow, want ? 1f : 0f,
			Time.deltaTime / (want ? FlowUp : FlowDown));

		if (moving)
			StepGlide();
		else if (want)
			StartStep();

		UpdateHints();
	}

	// Yon, parmagin tahtada durdugu nokta eksi ucgenin su anki yeri. Ekranda bir
	// kol yok, parmagin kendisi kol: ucgen parmaga dogru akiyor, parmak kayinca
	// yon ayni karede degisiyor.
	private Vector3 ReadSteer()
	{
		Vector3 key = Vector3.zero;
		if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
			key.z += 1f;
		if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
			key.z -= 1f;
		if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
			key.x += 1f;
		if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
			key.x -= 1f;
		if (key.sqrMagnitude > 0f)
			return key.normalized;

		if (!held)
			return Vector3.zero;

		var ray = cam.ScreenPointToRay(heldPos);
		var plane = new Plane(Vector3.up, new Vector3(0f, LayerY(myLay), 0f));
		if (!plane.Raycast(ray, out float dist))
			return Vector3.zero;

		// Olculen yer HUCRE degil, ucgenin o andaki gercek yeri: adimin ortasinda
		// parmak kayarsa yon biraktigi hucreden degil bulundugu yerden okunur.
		Vector3 here = moving
			? Vector3.Lerp(glideFromC, glideToC, Mathf.Clamp01(glideT))
			: Centroid(myLay, myCx, myCy, myOri);

		Vector3 d = ray.GetPoint(dist) - here;
		d.y = 0f;
		return d.magnitude < DeadZone ? Vector3.zero : d.normalized;
	}

	private Vector3 Centroid(int lay, int cx, int cy, int ori)
	{
		return StateToPos(lay, cx, cy, ori) + StateToRot(ori) * CLocal;
	}

	// --- hamle ---

	// Uc kenardan hangisi verilen yone en cok goturuyorsa o seciliyor. Ucgenin
	// uc cikisi 90 ve 135 derecelik araliklarla dagildigi icin hangi yone
	// basilirsa basilsin en kotu sapma 67.5 derece: direksiyon asla bosa
	// dusmuyor, her yon icin daima gercekten o tarafa giden bir cikis var.
	private void StartStep()
	{
		Vector3 from = Centroid(myLay, myCx, myCy, myOri);
		Vector3 to = from;
		float best = 0f;
		bool found = false;

		for (int m = 0; m < MoveCount; m++)
		{
			if (!Landing(myLay, myCx, myCy, myOri, m, out int nx, out int ny, out int no))
				continue;

			Vector3 c = Centroid(myLay, nx, ny, no);
			float d = Vector3.Dot((c - from).normalized, steer);
			if (d <= best)
				continue;

			best = d;
			to = c;
			found = true;
			pendingCx = nx;
			pendingCy = ny;
			pendingOri = no;
		}

		if (!found)
			return;

		takeA = Pick(pendingCx, pendingCy, pendingOri);
		takeB = -1;
		if (takeA < 0 && occ[myLay, pendingCx, pendingCy, pendingOri] == 0)
		{
			takeA = Pick(pendingCx, pendingCy, pendingOri ^ 1);
			takeB = Pick(pendingCx, pendingCy, pendingOri ^ 2);
		}

		// Kalkmayacak bir parcanin ustune biniyorsak inis bir kat yukarida.
		glideFromLift = occ[myLay, myCx, myCy, myOri] != 0 ? TriThick : 0f;
		glideToLift = occ[myLay, pendingCx, pendingCy, pendingOri] != 0 && takeA < 0 ? TriThick : 0f;

		history.Add(new Move
		{
			Lay = myLay, Cx = myCx, Cy = myCy, Ori = myOri, A = takeA, B = takeB, Dove = false,
			Carry = carry, WantA = wantA, WantB = wantB, Pair = -1,
			Tasks = tasks, Kind = objKind, Tone = objTone, Need = objNeed, Got = objGot,
			WOri = wantOri
		});

		// Yol agirlik merkezleri arasinda olculuyor, kose noktalari arasinda
		// degil: govde kendi merkezi etrafinda donsun, merkez duz gitsin diye.
		glideFromC = from;
		glideToC = to;
		glideFromRot = StateToRot(myOri);
		glideToRot = StateToRot(pendingOri);
		stepDur = Mathf.Max(
			Vector3.Distance(from, to) / Mathf.Max(0.1f, MoveSpeed),
			Quaternion.Angle(glideFromRot, glideToRot) / SpinSpeed);
		glideT = 0f;
		moving = true;

		steps++;
	}

	private void StepGlide()
	{
		// Parmak kalksa bile baslanan adim tamamlaniyor: ucgen iki hucrenin
		// arasinda kalmiyor. Yavasliyor ama iniyor.
		glideT += Time.deltaTime * Mathf.Max(flow, 0.35f) / Mathf.Max(0.01f, stepDur);

		float t = Mathf.Clamp01(glideT);

		// KONUM dogrusal: adim basina hizlanma-yavaslama yok, yumusama zincirin
		// iki ucunda flow uzerinden oluyor. DONUS ise adim icinde yumusuyor,
		// yoksa her hucre sinirinda acisal hiz takirdiyor.
		float e = t * t * (3f - 2f * t);
		var rot = Quaternion.Slerp(glideFromRot, glideToRot, e);
		me.rotation = rot;
		me.position = Vector3.Lerp(glideFromC, glideToC, t) - rot * CLocal
			+ Vector3.up * (Mathf.Sin(t * Mathf.PI) * HopHeight
				+ Mathf.Lerp(glideFromLift, glideToLift, t));

		if (t < 1f)
			return;

		float over = (glideT - 1f) * stepDur;
		moving = false;
		Arrive();

		// Yon hala duruyorsa siradaki adim AYNI KAREDE basliyor ve asan mesafe
		// devrediliyor: hucre sinirinda tek kare bile duraklama olmuyor.
		if (dropping || solved || lost || steer.sqrMagnitude <= 0.0001f)
			return;

		StartStep();
		if (moving)
			glideT = Mathf.Clamp01(over / Mathf.Max(0.01f, stepDur));
	}

	private void Arrive()
	{
		myCx = pendingCx;
		myCy = pendingCy;
		myOri = pendingOri;
		me.rotation = StateToRot(myOri);

		// Parca yok olmuyor, rengi sana geciyor. Tek renk tasiniyor: yenisini
		// almak eskisini siler.
		if (takeA >= 0)
			carry = pieces[takeA].Tone;
		Swallow(takeA);
		Swallow(takeB);
		TintMe();

		// Yukseklik parca kalktiktan SONRA okunuyor: kalkan parcanin ustune
		// binmis gibi havada kalmamak icin.
		me.position = MePos(myLay, myCx, myCy, myOri);

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

		// Kat ancak bosalinca bitiyor. objKind < 0 ise katta kurulabilecek gorev
		// kalmamis demektir (tek parca, tek renk), orada tutmanin anlami yok.
		if ((alive[myLay] == 0 || objKind < 0) && myLay < Layers - 1)
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
		if (myLay == Layers - 1 && (alive[myLay] == 0 || objKind < 0))
			solved = true;
	}

	private void CompleteTask()
	{
		tasks++;
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
		meDropTo = MePos(to, myCx, myCy, myOri);
		dropLay = from;
		dropT = 0f;
		dropping = true;

		for (int i = 0; i < MoveCount; i++)
			hints[i].gameObject.SetActive(false);

		// Inis bir duraktir: yeni katta hiz sifirdan kuruluyor.
		flow = 0f;

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
		if (history.Count == 0 || moving || dropping || solved)
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
		callSlide = 1f;
		TintMe();

		myCx = mv.Cx;
		myCy = mv.Cy;
		myOri = mv.Ori;
		me.position = MePos(myLay, myCx, myCy, myOri);
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

		// Yonerge dururken gosteriliyor: akarken zaten parmagi takip ediyorsun,
		// uc kucuk isaret o sirada sadece titreyen bir gurultu olurdu.
		bool live = Hints && !moving && !dropping && !solved && !lost;
		for (int m = 0; m < MoveCount; m++)
		{
			int nx = 0;
			int ny = 0;
			int no = 0;
			bool ok = live && Landing(myLay, myCx, myCy, myOri, m, out nx, out ny, out no);
			hints[m].gameObject.SetActive(ok);
			if (!ok)
				continue;

			// Sari nisan "buraya basarsan parca alirsin" demek; sadece ustunden
			// gecilecek bir parca icin yanmiyor, yoksa isaret yaniltiyor.
			bool take = Pick(nx, ny, no) >= 0;
			hints[m].GetComponent<MeshRenderer>().sharedMaterial = take ? matHintTake : matHint;
			// Isaret zeminin hemen uzerine yapisiyor; havada asili kalirsa yine cisim gibi okunuyor.
			// Parcanin ustune binilecekse nisan da onun uzerine cikiyor, yoksa
			// parcanin altinda kalip gorunmuyor.
			hints[m].position = MePos(myLay, nx, ny, no) + Vector3.down * (TriThick * 0.5f - 0.006f);
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
		// IMGUI varsayilan yazisi ekran olceginden etkilenmiyor: kutular s ile
		// buyuyor ama yazi 12 pikselde kaliyor, telefonda ayar paneli okunmuyordu.
		int body = Mathf.RoundToInt(18f * s);
		GUI.skin.label.fontSize = body;
		GUI.skin.button.fontSize = body;
		GUI.skin.toggle.fontSize = body;
		GUI.skin.box.fontSize = body;

		// Kaydirma cubugu parmakla tutulacak kadar genis olmali: IMGUI kaydirma
		// alani dokunusla SURUKLENMIYOR, tek tutamak ince cubugun kendisi.
		GUI.skin.verticalScrollbar.fixedWidth = 30f * s;
		GUI.skin.verticalScrollbarThumb.fixedWidth = 30f * s;

		var big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(21f * s) };
		var huge = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(38f * s), alignment = TextAnchor.MiddleCenter };

		int left = 0;
		for (int l = 0; l < Layers; l++)
			left += alive[l];

		GUI.Label(new Rect(16f * s, 10f * s, 700f * s, 32f * s), "Gorev: " + tasks + "     Hamle: " + steps, big);
		GUI.Label(new Rect(16f * s, 40f * s, 700f * s, 32f * s),
			"Kat " + (myLay + 1) + " / " + Layers + "     Bu katta: " + alive[myLay] + "     Kalan: " + left, big);
		if (myLay < Layers - 1)
			GUI.Label(new Rect(16f * s, 70f * s, 700f * s, 32f * s), "Kat bosalinca zemin cekiliyor, asagi iniyorsun.", big);

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
				// Dolu yarim tahtada DURAN parca, ici bos olan SENIN getirecegin
				// renk. Iki renkli duz bir kare hangisini hangisine goturecegini
				// soylemiyordu; en cok takilan yer burasiydi.
				GUI.color = Swatch(wantB);
				GUI.DrawTexture(r, halfTex[wantOri ^ 3]);

				// Renk zaten elindeyse bos yarim doluyor: soru artik hangisini
				// getirecegin degil, nereye basacagin.
				var ghost = Swatch(wantA);
				if (carry != wantA)
					ghost.a = 0.28f;
				GUI.color = ghost;
				GUI.DrawTexture(r, halfTex[wantOri]);
				GUI.color = Swatch(wantA);
				GUI.DrawTexture(r, edgeTex[wantOri]);
			}
			else
			{
				GUI.color = Swatch(objTone);
				GUI.DrawTexture(r, upTex);
			}
			GUI.color = oc;

			GUI.Label(new Rect(bx + q + 10f * s, by + 20f * s, 320f * s, 40f * s),
				objKind == 1 ? carry == wantA ? "yerine bas" : "bos yarimi getir" : objGot + " / " + objNeed, big);
		}

		if (solved)
			GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 60f * s), "TEMIZ  -  " + tasks + " gorev  /  " + steps + " hamle", huge);
		else if (lost)
			GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 60f * s), lostText + "  -  Z geri al, R bastan", huge);

		float pw = 300f * s;
		panelLeft = Screen.width - pw - 10f * s;
		if (GUI.Button(new Rect(Screen.width - 110f * s, 10f * s, 100f * s, 32f * s), showPanel ? "Kapat" : "Ayar"))
			showPanel = !showPanel;

		var vs = new GUIStyle(GUI.skin.label)
		{
			fontSize = Mathf.RoundToInt(15f * s),
			alignment = TextAnchor.MiddleRight
		};
		vs.normal.textColor = new Color(0.35f, 0.34f, 0.32f);
		GUI.Label(new Rect(Screen.width - 110f * s, 44f * s, 100f * s, 22f * s), Version, vs);

		if (!showPanel)
			return;

		GUI.Box(new Rect(panelLeft, 50f * s, pw, Screen.height - 66f * s), "");
		panelScroll = GUI.BeginScrollView(
			new Rect(panelLeft + 8f * s, 58f * s, pw - 16f * s, Screen.height - 82f * s),
			panelScroll,
			// Icerik yuksekligi sabit degil, gecen karede nereye kadar uzadiysa o:
			// sabit sayi tutmadiginda alttaki dugmelere hic ulasilamiyordu.
			new Rect(0f, 0f, pw - 44f * s, Mathf.Max(panelContent, 1f)));

		float x = 0f;
		float y = 0f;
		float w = pw - 48f * s;

		// Dugmeler en ustte: en cok kullanilanlar icin paneli dibe kadar
		// kaydirmak gerekmemeli.
		if (GUI.Button(new Rect(x, y, w, 34f * s), "Bastan (R)"))
			Build();
		y += 40f * s;
		if (GUI.Button(new Rect(x, y, w, 34f * s), "Yeni tahta (N)"))
		{
			seed++;
			SavePrefs();
			Build();
		}
		y += 40f * s;
		if (GUI.Button(new Rect(x, y, w, 34f * s), "Varsayilana don"))
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
			Bevel = 0.013f;
			Shiny = true;
			Build();
		}
		y += 44f * s;

		bool nh = GUI.Toggle(new Rect(x, y, w, 26f * s), Hints, " Yonerge ucgenleri");
		if (nh != Hints)
		{
			Hints = nh;
			SavePrefs();
			UpdateHints();
		}
		y += 34f * s;

		bool ns = GUI.Toggle(new Rect(x, y, w, 26f * s), Shiny, " Parlak yuzey");
		if (ns != Shiny)
		{
			Shiny = ns;
			SavePrefs();
			Build();
		}
		y += 40f * s;

		SliderInt(ref Layers, 2, FloorTone.Length, "Kat sayisi", x, ref y, w, s, true);
		SliderInt(ref TopRadius, 1, 5, "Ust kat yaricapi", x, ref y, w, s, true);
		SliderInt(ref RadiusStep, 1, 2, "Yaricap artisi", x, ref y, w, s, true);
		Slider(ref PieceRatio, 0.05f, 0.6f, "Parca yogunlugu", x, ref y, w, s, true);
		Slider(ref LayerGap, 0.2f, 1.6f, "Kat araligi", x, ref y, w, s, true);
		Slider(ref SlabRatio, 0.1f, 1.6f, "Kat govde yuksekligi", x, ref y, w, s, true);
		Slider(ref FogAmount, 0f, 0.93f, "En alt kat sisi", x, ref y, w, s, true);
		Slider(ref HintSize, 0.12f, 0.9f, "Yonerge boyutu", x, ref y, w, s, true);
		Slider(ref MoveSpeed, 1.5f, 9f, "Hiz", x, ref y, w, s, false);
		Slider(ref DropTime, 0.15f, 1.2f, "Inis suresi", x, ref y, w, s, false);
		Slider(ref HopHeight, 0f, 0.3f, "Ziplama", x, ref y, w, s, false);
		Slider(ref TriThick, 0.04f, 0.3f, "Ucgen kalinlik", x, ref y, w, s, true);
		Slider(ref Bevel, 0f, 0.05f, "Pah genisligi", x, ref y, w, s, true);
		Slider(ref Inset, 0f, 0.25f, "Ucgen bosluk", x, ref y, w, s, true);
		Slider(ref FloorInset, 0f, 0.25f, "Zemin bosluk", x, ref y, w, s, true);
		Slider(ref CamPitch, 20f, 85f, "Kamera egimi", x, ref y, w, s, false);
		Slider(ref CamZoom, 0.6f, 2f, "Kamera uzakligi", x, ref y, w, s, false);

		GUI.Label(new Rect(x, y, w, 26f * s), "Tohum: " + seed);
		y += 32f * s;
		GUI.Label(new Rect(x, y, w, 26f * s), "Basili tut, o yone akarsin");
		y += 26f * s;
		GUI.Label(new Rect(x, y, w, 26f * s), "Parcaya basinca rengini alirsin");
		y += 26f * s;
		GUI.Label(new Rect(x, y, w, 26f * s), "Gerekmeyenin ustunden gecersin");
		y += 26f * s;
		GUI.Label(new Rect(x, y, w, 26f * s), "Karede ici bos yarim senin rengin");
		y += 26f * s;
		GUI.Label(new Rect(x, y, w, 26f * s), "Z: geri al (kat icinde)");
		y += 26f * s;

		panelContent = y + 16f * s;

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
		LayerGap = PlayerPrefs.GetFloat("stk_gap", LayerGap);
		SlabRatio = PlayerPrefs.GetFloat("stk_slab", SlabRatio);
		FogAmount = PlayerPrefs.GetFloat("stk_fog", FogAmount);
		HintSize = PlayerPrefs.GetFloat("stk_hsz", HintSize);
		MoveSpeed = PlayerPrefs.GetFloat("stk_spd", MoveSpeed);
		DropTime = PlayerPrefs.GetFloat("stk_dt", DropTime);
		HopHeight = PlayerPrefs.GetFloat("stk_hop", HopHeight);
		TriThick = PlayerPrefs.GetFloat("stk_th", TriThick);
		Bevel = PlayerPrefs.GetFloat("stk_bev", Bevel);
		Shiny = PlayerPrefs.GetInt("stk_shiny", Shiny ? 1 : 0) == 1;
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
		PlayerPrefs.SetFloat("stk_gap", LayerGap);
		PlayerPrefs.SetFloat("stk_slab", SlabRatio);
		PlayerPrefs.SetFloat("stk_fog", FogAmount);
		PlayerPrefs.SetFloat("stk_hsz", HintSize);
		PlayerPrefs.SetFloat("stk_spd", MoveSpeed);
		PlayerPrefs.SetFloat("stk_dt", DropTime);
		PlayerPrefs.SetFloat("stk_hop", HopHeight);
		PlayerPrefs.SetFloat("stk_th", TriThick);
		PlayerPrefs.SetFloat("stk_bev", Bevel);
		PlayerPrefs.SetInt("stk_shiny", Shiny ? 1 : 0);
		PlayerPrefs.SetFloat("stk_in", Inset);
		PlayerPrefs.SetFloat("stk_fin", FloorInset);
		PlayerPrefs.SetFloat("stk_cp", CamPitch);
		PlayerPrefs.SetFloat("stk_cz", CamZoom);
		PlayerPrefs.SetInt("stk_hint", Hints ? 1 : 0);
		PlayerPrefs.SetInt("stk_seed", seed);
		PlayerPrefs.Save();
	}
}
