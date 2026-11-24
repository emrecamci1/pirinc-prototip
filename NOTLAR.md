# Pirinc prototipi - durum notlari

Kisisel yan proje. Unity 2022.3.57f1, built-in render pipeline, dikey (portrait) mobil hedef.
Emre sanat ve Play Mode testini yapiyor; kodu Claude yaziyor.

## Kurallar

- Calisma dizini is bilgisayarinda `spooky-match` olabilir. **Bu projenin kodu oraya ASLA yazilmaz.**
- Kaynak kodda Turkce karakter yok (ASCII). Tek istisna: editor `[MenuItem("Pirinç/...")]` yollari.
- Sahneler calisma aninda kuruluyor: sahnede sadece kamera + script tasiyici var, geri kalani `Awake()` icinde.
- Her itilen degisiklik bir surum numarasi alir (`StackPrototype.Version`, ekranin sag ustunde yazar),
  ayni numara git etiketi olarak atilir ve `SURUMLER.md` icine yazilir. Geri donus bu numaradan yapilir.

## Sahneler

| Menu | Sahne | Script | Durum |
|---|---|---|---|
| Pirinç/1 - YIĞIN | `Assets/Scenes/Stack.unity` | `StackPrototype.cs` | **aktif olan** |
| Pirinç/2 - Üçgen kule (eski) | `Assets/Scenes/Triangle.unity` | `TrianglePrototype.cs` | park edildi |

Sahne yoksa veya bozuksa menuden tekrar kur; `Assets/Editor/SetupStackScene.cs` sifirdan uretir.

## YIGIN (aktif prototip)

Kat kat duran bir board. Ucgenle kenar uzerinden takla atarak geziyoruz. Kat tamamen
BOSALINCA asagiki kata iniyoruz: her kata konan her renk kullanilacak.

**Hedef (2026-11-23):** parcanin ustune devrilince onu yok etmiyorsun, RENGINI
aliyorsun - ucgen o renge donuyor. Tek renk tasiniyor, yenisini almak eskisini siler.

Ustte bir GOREV duruyor, yandan kayarak giriyor, iki tipten biri oluyor:

- **Tek renk:** "su renkten 3 tane". Ustune devrildigin her dogru renk sayiliyor,
  rozet tek ucgen olarak cizilir, yaninda `2 / 3` yazar.
- **Kare:** iki yarim, mesela alt-solda sari + ust-sagda kirmizi. Rozette DOLU yarim
  tahtada duran parca, ICI BOS olan senin getirecegin renk - iki renkli duz bir kare
  hangisini hangisine goturecegini soylemiyordu. Renk eline gectigi an bos yarim
  doluyor, yazi da "bos yarimi getir"den "yerine bas"a doner. Elinde sari varken
  kirmizi parcanin KARSI YARISINA basarsan kare olusur - ama DIZILIS de tutmali:
  rozette sari alt-soldaysa sen de alt-sol yariya basmis olacaksin (`wantOri`).
  Rozet dort yarim dokusundan (`halfTex[ori]`) ikisiyle ciziliyor, yonelim indisiyle
  ayni sirada. Doku satiri 0 ekranda altta durdugu icin doku uzayi tahta uzayiyla
  ayni yonde - cevirmeye gerek yok.
  Gorev tahtada duran GERCEK bir yerlesimden uretiliyor (`MateSpot`): karsi yarisi bos
  ve ulasilabilir bir parca secilip dizilis ondan okunuyor. Rastgele dizilis istemek
  cozulemeyen gorev demekti.

Tip her gorevde yeniden ve rastgele seciliyor (yarisi kare, yarisi tek renk; katta
tek renk kalmissa zorunlu olarak tek renk). Donusumlu olsaydi hep kare istenirse oyun
tek gorevli gibi okunuyordu, hep tek renk istenirse birlestirme hic kullanilmiyordu.

Bu neden oturuyor: 2 numarali hamle (yerinde burgu) zaten ayni karenin diger yarisina
geciriyor, ve bir parcanin karsi yarisina basmak bastan beri serbestti - parca yerinde
kaliyordu, ikiniz tam bir kare oluyordunuz, ama hicbir anlami yoktu. Yeni fizik yok,
var olan bos duruma anlam verildi.

Gorev her zaman o katta GERCEKTEN duran renklerden kuruluyor ve tamamlanamaz hale
gelirse (`TaskPossible`) aninda yenileniyor. Oyuncunun hicbir hamleyle duzeltemeyecegi
bir hedef ekranda durmamali.

**Gerekmeyen parcanin USTUNDEN gecersin (`Pick` / `MePos`).** Yok olmaz, yol da
kesmez: ucgen onun ustune binip devam eder, bir `TriThick` yukarida durur.

Bu noktada iki yanlis denendi, ikisi de ayni gun geri alindi:

1. *Her parca basilinca kalkiyordu.* Oyun kendi kendini bitiriyordu, ileride lazim
   olacak renkler yolda harcaniyordu.
2. *Gerekmeyen renkler duvar yapildi.* Bu sefer yol kesiyordu; Emre'nin istegi acikti,
   engel olmasin.

Dogrusu: parca sadece GOREVIN ISTEDIGI renkse kalkiyor, geri kalaninin ustune
biniliyor. Ayni yariya iki ucgen girmiyor, biri digerinin ustunde duruyor.

Dikkat edilecek iki yer: inis yuksekligi parca KALKTIKTAN sonra okunmali (`MePos`
cagrisi `Swallow`'dan sonra), ve takla hesabi binisi gormemeli - yukseklik ayri bir
rampa (`tumbleFromLift` / `tumbleToLift`), yoksa kaldirilmis baslangic noktasi donup
zeminin altina iniyor. Nisan da parcanin ustune cikiyor, yoksa altinda kalip
gorunmuyor; sari nisan artik "burada parca alirsin" demek, sadece "burada parca var"
degil. Parca yogunlugu 0.18'den 0.26'ya cikti, parcalar artik tukenmedigi icin.

**Hamle butcesi kaldirildi.** Iki saat olmaz: baski gorevlerden gelecek. Sureli gorevler
(mesela "40 saniyede mavi") henuz yok, once birlestirme hissi olculecek.

**Hareket:** 3 kenar = 3 hamle. 0 = yatay kenardan dikey komsuya, 1 = dikey kenardan
yatay komsuya, 2 = hipotenus uzerinde yerinde donme.

**Parite:** her karede 2 ucgen yeri var ama bunlarin sadece yarisina ulasilabiliyor.
`(cx+cy)` cift ise {0,3}, tek ise {1,2}. Dama tahtasi gibi. Bu yuzden zemin dosemesi de
ayni kosegenden bolunmus - oyuncu nereye basabilecegini doseme deseninden okuyor.
Ulasilamayan tas koyulmasi imkansiz (eski "maviyi alamiyorum" hatasi bu sekilde coktu).

**Sis:** her kat icin ayri saydam quad. Ust uste bindikleri icin her levhanin opacity'si
dogrudan verilemez; gorunen solma `V(L)` verilip levha alfasi `a = 1 - (1-V(L))/(1-V(L-1))`
olarak turetiliyor. 3 katta FogAmount 0.8 -> %0 / %50 / %80. Basilan kat her zaman net.

**Kumanda:** gitmek istedigin yere dokun, ucgen kendi yolunu buluyor (`TapTarget` ->
`PlanPath`). Kenar kenar yurutmek sikiciydi; rota kurmak zaten oyuncunun isi, taklalari
tek tek tiklatmak degil. En kisa yol durumlar (kare, yarim) ve uc hamle uzerinde
genislik aramasiyla bulunuyor; tahta en fazla `gridN * gridN * 4` durum oldugu icin her
dokunusta bastan hesaplamak bedava.

Dokunulan KARE yetiyor: karenin iki basilabilir yarisindan dokunusun dustugu taraf
seciliyor (`Inside` + `DiagOri`), yani parmagin tam yarimin uzerine denk gelmesi
gerekmiyor. Nisana nisan alma devri bitti, `TapRadius` kaldirildi.

Dokunus takla ve inis sirasinda da yakalaniyor - birakma olayi tek kare surdugu icin
animasyon beklenirse dokunus tamamen kayboluyor. Girdi yol adimindan ONCE okunuyor,
boylece yururken yon degistirilebiliyor. Inis sirasindaki dokunus atiliyor: ekran
koordinati yeni katin duzlemine goturulurse bakilmayan bir kareye donusuyor.

Gidilen yer koyu bir nisanla isaretli kaliyor (`goalMark`). Gorev tamamlaninca,
inince ve geri alinca yol temizleniyor (`ClearPath`).

Masaustunde kaydirma (swipe) hala tek hamle surmek icin duruyor. Harfler ve klavye
daha once kaldirildi.

**Ayarlar:** oyun icinde IMGUI slider paneli. Yazi boyutunu OnGUI basinda
`GUI.skin.label/button/toggle/box.fontSize` olarak veriyoruz: IMGUI varsayilan yazisi
ekran olceginden etkilenmiyor, kutular `s` ile buyurken yazi 12 pikselde kaliyor ve
telefonda panel okunmuyordu.

Panelde dikkat edilecek uc sey: kaydirma alani dokunusla SURUKLENMIYOR (IMGUI boyle),
bu yuzden kaydirma cubugu `30 * s` genisliginde - parmakla tutulabilsin diye. Icerik
yuksekligi sabit sayi degil, gecen karede `y` nereye kadar uzadiysa o (`panelContent`);
sabit sayi tutmadiginda alttaki dugmelere hic ulasilamiyordu. Dugmeler de en uste
alindi, en cok kullanilanlar icin dibe kadar kaydirmak gerekmesin. PlayerPrefs'e yaziyor, `DefaultsVersion`
sabiti degisince kayitli degerler gecersiz oluyor.

## Siradaki

1. Birlestirme hissi telefonda olculecek: kare tamamlandigi an okunuyor mu, tasidigin
   rengi ucgenden anlamak yetiyor mu (ucgen parca rengine boyaniyor - parca saniliyor mu).
2. Sureli gorevler: bazilari saatli olsun ("40 saniyede mavi"), hepsi degil. Gorev tipleri
   (tek renk / kare) hazir, geriye sure ve kaybetme kaliyor. `lost` / `lostText` duruyor
   ama su an hicbir yerden set edilmiyor.
3. Hareket v5 ile tamamen degisti. Ilk olculecek sey: akiskanlik geldi mi, ve
   parmakla surmek bulmacayi kolaylastirip anlamsizlastirdi mi.
4. Masada duran kural adayi: ustunden ayrildigin kare arkanda kapaniyor.

## Iki makine

Depo: `github.com/emrecamci1/pirinc-prototip` (private).
Is bilgisayari Windows (git komut satiri), evde MacBook (GitHub Desktop).
Calismaya baslamadan **Pull**, birakirken **Push**. Pull'dan once Play Mode'dan cik.

Claude oturumu makineyle gelmiyor - bu dosya iki makine arasindaki tek kopru.
Her oturum sonunda guncel tut.

## Telefona atmak

**Android** (Windows'tan, calisiyor): `Pirinç/Android Ayarlarini Yap` -> File >
Build Settings > Build And Run. Test cihazi Galaxy S23 (SM-S911B).

**iOS** (sadece Mac'ten): `Pirinç/iOS Ayarlarini Yap` -> Build (Build And Run degil)
-> uretilen `Unity-iPhone.xcodeproj` Xcode'da acilir -> Signing & Capabilities >
Team > Personal Team -> cihaz secilip Run. iPhone'da once Gizlilik ve Guvenlik >
Gelistirici Modu acik olmali. Ucretsiz Apple ID ile imza 7 gun gecerli.

## Tuzaklar (ikisi de Play Mode'da patladi, derleyici gormedi)

**Yerlesik font adi.** `Resources.GetBuiltinResource<Font>("Arial.ttf")` Unity
2022.3'te ArgumentException atiyor; dogrusu `LegacyRuntime.ttf`. Hata `Build()`
ortasinda patlayinca sahne yarim kaliyor ve ekran bos geliyor - derleme temiz
oldugu icin ancak Play'e basinca anlasiliyor. Editor.log'dan okunuyor.

**StateToPos dokunus olcumu icin kullanilamaz.** Bir durumun konumu olarak dik
aci KOSESINI veriyor. Iki komsu takla ayni kose etrafinda dondugu icin 0 ve 1
hamlelerinin StateToPos degeri birebir ayni nokta. Nisanlar ekranda ayri gorunur
(mesh o koseye tutturulup farkli aciyla dondurulur) ama mesafe olcumu onlari ust
uste sanir; esitlikte ilki kazandigi icin 1 numarali hamle dokunusla hic
secilemiyordu. Dokunus artik nisanin GORUNEN agirlik merkezine bakiyor:
dik aci kosesinden iki bacak yonunde birer bolu uc.

## Kumanda

**Parmagini basili tut, ucgen o yone akar.** Parmagin kendisi koldur: ucgen
parmaga dogru gider, parmak kayinca yon ayni karede degisir, parmagin altina
varinca durur (`DeadZone` 0.5). Birakinca yavaslayarak son hucresine iner.

Hedefe dokunup yol oynatma KALKTI - sebebi asagida, "Hareket" bolumunde.

Editorde WASD / ok tuslari da yon veriyor (telefonsuz test icin). Duran tuslar:
R yeniden kur, N yeni board, Z geri al, Tab ayar paneli.

Sag ust kosede (Ayar dugmesi + surum yazisi) yon OKUNMUYOR: yoksa paneli acmak
ucgeni bir adim seyirtiyordu.

## Hareket (v5)

Takla biciminde ilerleme KALDIRILDI. Emre'nin verdigi hukum: "takla atarak
ilerlemesi zevkli akiskan hissettirmedi". Sorun ayar degil geometriydi, uc
tane:

1. **Oyuncu ucgeni hareket ettirmiyordu, hedef secip izliyordu.** Tek dokunusla
   BFS en kisa yolu buluyor ve adimlari kendisi oynatiyordu. Hissiyatin
   kacmasinin ASIL sebebi bu: adim adim verilen karar yok, aradaki yurume olu
   zaman.
2. **Taklanin donusu mesafesine gore cok fazla.** Dik ucgenin agirlik merkezi
   dik koseden (1/3, 1/3): bacaktan takla 180 derece donup 0.62 hucre, hipotenus
   taklasi 180 derece donup 0.44 hucre gidiyor. Yuvarlanan bir kup 90 derecede
   1.00 hucre gidiyor - yani ucgen ayni mesafe icin kupun uc-dort kati donuyor.
   Cok cabalayip az ilerleyen bir sey izleniyordu.
3. **Her adim sifirda baslayip sifirda bitiyordu.** `t*t*(3-2t)` hem ease-in hem
   ease-out. Bes hucre yurumek bes ayri duraksama demekti.

Yerine gelen: agirlik merkezi iki hucre arasinda DUZ bir cizgide sabit hizla
gidiyor, govde yol boyunca kendi merkezi etrafinda yeni yonune donuyor. Donus
artik ilerlemenin kendisi degil, yan urunu.

- **Agirlik merkezi** yerelde daima `(1/3, 0, 1/3)` (`CLocal`). `Inset` ucleri
  merkeze dogru lerpledigi icin bosluk ayari merkezi KAYDIRMIYOR, o yuzden sabit.
  Konum kose noktasindan degil merkezden kuruluyor: `pos = merkez - rot * CLocal`.
  Kose uzerinden lerplenseydi merkez egri bir yol izlerdi.
- **Yumusama adim basina DEGIL, zincirin iki ucunda.** `flow` 0'dan 1'e
  `FlowUp` 0.10 sn'de cikiyor, birakinca `FlowDown` 0.08 sn'de iniyor; aradaki
  butun adimlar tam hizda. Akiskanlik tam olarak buradan geliyor.
- **Baslanan adim her halukarda tamamlaniyor** (`Mathf.Max(flow, 0.35f)`):
  parmak kalkinca ucgen iki hucrenin arasinda donup kalmiyor, yavaslayarak iniyor.
- **Hucre sinirinda duraklama yok**: varista asan sure (`over`) siradaki adima
  devrediliyor ve yeni adim AYNI KAREDE basliyor.
- **Donus suresi sinirli** (`SpinSpeed` 1200 derece/sn). Sirf mesafeye gore
  surulseydi hipotenus adimi iki kati aciyi yariya yakin surede donerdi
  (1730 derece/sn) ve cirpinma gibi okunurdu. Sinir, bacak adimiyla hipotenus
  adiminin suresini ~0.15 sn'de bulusturuyor.
- **Yon secimi**: uc cikistan hangisinin merkez farki yone en cok bakiyorsa o.
  Ucgenin uc cikisi 90 ve 135 derecelik araliklarla dagildigi icin en kotu sapma
  67.5 derece - yani hangi yone basilirsa basilsin daima gercekten o tarafa giden
  bir cikis var, direksiyon bosa dusmuyor. Tahta kenarinda cikis kalmazsa duruyor.
- **Yonerge ucgenleri sadece DURURKEN** gosteriliyor; akarken zaten parmak takip
  ediliyor, uc kucuk isaret o sirada titreyen gurultu olurdu.

Silinenler: `StepTumble`, `Swing`, `PlanPath`, `TapTarget`, `ClearPath`, `Steer`,
`PickFine`, `Inside`, yol dizileri, hedef nisani (`goalMark`), `TumbleTime`,
`SwipeMin`, `DirX` / `DirY`. Hepsi `v4` etiketinde duruyor.

Ayardaki `Hiz` (birim/sn, varsayilan 4.2) ve `Ziplama` (adim basina kucuk yay,
0.03) bu sistemin iki tutamagi.

## Gorsel (2026-11-24)

Sorun mesh silueti degildi, golgelemeydi. Uc sey birden degisti:

1. **Pahli prizma** (`BuildTriMesh(inset, thick, bevel)`). Keskin kenar isik yakalamiyor,
   kenar bir cizgi olarak degil iki yuzun ani renk degisimi olarak okunuyordu. Ince bir
   pah o kenari isiga donduruyor. Ic kenar ICTEGET uzerinden kaydiriliyor - agirlik
   merkezine lerp etmek dar acida daha cok kaydirir ve pah genisligi kenardan kenara
   degisirdi. Pah yuzu kendi duz normalini aliyor; yumusatilsaydi kenar yine kaybolurdu,
   oysa istenen tam olarak o ince parlak serit. `Bevel` ayardan degisiyor, 0 = eski hal.
2. **`Assets/Resources/TriLit.shader`** - spekular + ince kenar isigi. Resources altinda
   olmasi sart: sahne calisma aninda kuruldugu icin hicbir materyal shaderi referans
   etmiyor ve derlemede silinirdi. `Shiny` ayari kapatilinca Legacy Diffuse'e doner,
   yani A/B karsilastirmasi telefonda yapilabiliyor.
3. **Iki isik + degrade ortam isigi.** Tek isikta golgede kalan yuzler duzlesiyor ve yan
   yuz govdeden ayirt edilemiyordu; karsi yondan soguk bir dolgu kondu. Ortam isigi tek
   renk yerine Trilight: ustten acik, altta koyu.

   Isik siddetleri bir kez KALIBRE edildi: ust yuze dusen toplam carpan (gunes * sin52 +
   dolgu * sin26 + gok ortami) 1.0a cekilince tahtadaki renk gorev rozetiyle birebir
   esitleniyor - renk uzayi Gamma oldugu icin carpan dogrudan sRGB degeri uzerinde
   isliyor. Ama parlak hali (carpan ~1.6) daha iyi bulundu ve kalibrasyon GERI ALINDI
   (7168a27 revert). Yani rozetle tahtanin tam ayni renk olmamasi bilinen ve kabul
   edilmis bir sonuc; tekrar duzeltmeye kalkisma.

   **Isiga/contrast'a artik dokunma.** Iki ayri deneme de GERI ALINDI: once carpani 1.0a
   ceken kalibrasyon (7168a27), sonra "her sey sisli" sikayetinden sonra yapilan contrast
   duzeltmesi (bbe0e90: golge tam siddet, dolgu 0.20, kenar isigi 0, ek Parlaklik/Kenar
   isigi ayarlari). Ikincisi de begenilmedi ve ayni gun revert edildi - yani mevcut
   degerler (golge 0.62, dolgu 0.38, kenar isigi 0.30, spekular 0.22) ISTENEN hal.
   Yeniden bir netlik sikayeti gelirse once NEYIN okunmadigini sor (rozet mi, parca
   kenari mi, hangi kat) ve o tek seyi duzelt; sahne isigini topluca degistirme.

   Rozet rengi: `Swatch()` isik carpanini rozete de uyguluyor (v4). Bir kez geri
   alinmisti, sonra tekrar istendi - "rozetin rengi board ustundeki ucgen renkleri
   gibi olsun ama isik koymadan, sadece renklerle". Yani yeni bir isik yok, sadece
   renk hesabi. Kanallarin 1'de kirpilmasi KASITLI: tahtada da ayni sekilde
   kirpiliyor, rozetin solgun gorunmesi parcanin solgun gorunmesinden.


Referans modeli: `Documents/Blender/Eskiz/blender_ucgen_oyun.py` tek ucgeni oyunun
birebir olculerinde kuruyor (bacak 0.93, kalinlik 0.11, pah 0.013, pah sadece cevre
kenarlarinda, dikey koseler keskin). Olcu degisirse script parametre aliyor.
4. **Cam parcalar - GERI ALINDI (v2, v3 ile kaldirildi).** Asagisi neden calistigini
   anlatiyor, tekrar denenecek olursa diye duruyor; su an kodda YOK. Parcalar artik yari saydam: `Assets/Resources/TriGlass.shader`.
   Govde `Cam` ayarina gore saydam (0 = eski opak hal, 1 = alfa 0.40), kenarlar
   fresnel ile hem opaklasiyor hem parliyor - cama kenarindan bakinca kat edilen
   kalinlik artiyor, kenarin parlak gorunmesi bundan. Blender referansindaki
   (`ucgen_oyun.blend`) buzlu cam bakisi buraya tasindi.

   Iki tuzak, ikisi de sisle ilgili:
   - **Siralama**: sis tabakalari Sprites/Default ile 3000'de. Parca da 3000'de
     olsaydi mesafeye gore bazi parcalar sisten SONRA cizilir, bazilari once -
     alt katin sisi ust kattaki parcalarin uzerine binerdi. Shader bu yuzden
     `Transparent-100` (2900).
   - **ZWrite On**: kapali bir parcanin gorunen yuzleri (arka yuzler elendigi
     icin) hic ust uste binmiyor, yani saydamin alisildik siralama sorunu yok.
     Kapatsaydik sis tabakalari derinlik testine takilmaz, parcalarin onune
     gecerdi. Yani ZWrite On sadece bir optimizasyon degil, sisin calismasinin
     sarti.

   Ust yuzdeki parlama icin ucuncu bir yonlu isik var ("Glint", 0.30, Euler
   52/180). Yeri rastgele degil: kamera egimi de 52, yani kameranin ust yuzden
   yansiyan bakisi tam oraya gidiyor ve yari vektor yukari bakiyor. Tahtanin
   ONUNDEN gelen bir isik ust yuzu aydinlatir ama parlatmaz.

   Renk tasiyan oyuncu da `matPiece[carry]` kullandigi icin camlasiyor. Tahtada
   oyuncuyu kaybetmek sorun olursa oyuncuya ayri bir opak malzeme seti gerekir.


Blender hala masada ama parca KAYNAGI olarak degil: ucgen `Inset` / `TriThick` / `Bevel`
ayarlarina bagli uretildigi icin FBX getirmek o bagi koparir (pivot dik aci yerelde (0,0),
bacaklar +X ve +Z). Blender'i hedef belirlemek icin kullanmak mantikli, uretim icin degil.
