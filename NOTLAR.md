# Pirinc prototipi - durum notlari

Kisisel yan proje. Unity 2022.3.57f1, built-in render pipeline, dikey (portrait) mobil hedef.
Emre sanat ve Play Mode testini yapiyor; kodu Claude yaziyor.

## Kurallar

- Calisma dizini is bilgisayarinda `spooky-match` olabilir. **Bu projenin kodu oraya ASLA yazilmaz.**
- Kaynak kodda Turkce karakter yok (ASCII). Tek istisna: editor `[MenuItem("Pirinç/...")]` yollari.
- Sahneler calisma aninda kuruluyor: sahnede sadece kamera + script tasiyici var, geri kalani `Awake()` icinde.

## Sahneler

| Menu | Sahne | Script | Durum |
|---|---|---|---|
| Pirinç/1 - YIĞIN | `Assets/Scenes/Stack.unity` | `StackPrototype.cs` | **aktif olan** |
| Pirinç/2 - Üçgen kule (eski) | `Assets/Scenes/Triangle.unity` | `TrianglePrototype.cs` | park edildi |

Sahne yoksa veya bozuksa menuden tekrar kur; `Assets/Editor/SetupStackScene.cs` sifirdan uretir.

## YIGIN (aktif prototip)

Kat kat duran bir board. Ucgenle kenar uzerinden takla atarak geziyoruz. Bir kat
tamamen temizlenince asagiki kata iniyoruz, o katta birakilanlar bizimle dusuyor.

**Hedef (2026-11-23):** parcanin ustune devrilince onu yok etmiyorsun, RENGINI
aliyorsun - ucgen o renge donuyor. Tek renk tasiniyor, yenisini almak eskisini siler.
Ustte istenen bir KARE duruyor (iki yarim, mesela sari + kirmizi) ve yandan kayarak
giriyor. Elinde sari varken kirmizi parcanin KARSI YARISINA basarsan orada tam bir
sari-kirmizi kare olusur, istenen tamamlanir ve yeni cift gelir.

Bu neden oturuyor: 2 numarali hamle (yerinde burgu) zaten ayni karenin diger yarisina
geciriyor, ve bir parcanin karsi yarisina basmak bastan beri serbestti - parca yerinde
kaliyordu, ikiniz tam bir kare oluyordunuz, ama hicbir anlami yoktu. Yeni fizik yok,
var olan bos duruma anlam verildi.

Istenen cift her zaman o katta GERCEKTEN duran renklerden seciliyor; renklerden biri
tukenirse cift aninda yenileniyor. Oyuncunun hicbir hamleyle duzeltemeyecegi bir hedef
ekranda durmamali.

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

**Kumanda:** nisana dokun (telefonda da ayni). En yakin nisan seciliyor, `TapRadius`
yaricapi icinde; disina dokunmak hicbir sey yapmiyor. Harfler ve klavye kaldirildi.
Olcu noktasi nisanin GORUNEN agirlik merkezi - `StateToPos` dik aci kosesini verdigi
icin 0 ve 1 hamleleri orada ust uste biniyordu ve 1 hic secilemiyordu.

**Ayarlar:** oyun icinde IMGUI slider paneli. PlayerPrefs'e yaziyor, `DefaultsVersion`
sabiti degisince kayitli degerler gecersiz oluyor.

## Siradaki

1. Birlestirme hissi telefonda olculecek: kare tamamlandigi an okunuyor mu, tasidigin
   rengi ucgenden anlamak yetiyor mu (ucgen parca rengine boyaniyor - parca saniliyor mu).
2. Gorev listesi: kat basina birden fazla gorev, farkli tipler. Sadece renk toplama degil,
   birlestirme de gorev olabilsin. Bazilari sureli ("40 saniyede mavi"), hepsi degil.
3. Kaybetme kosulu geri gelecek (sureli gorev kacirilinca). `lost` / `lostText` alanlari
   duruyor ama su an hicbir yerden set edilmiyor.
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

Tek sey: nisana dokun. `TapRadius` yaricapi icindeki en yakin nisan secilir,
disina dokunmak hicbir sey yapmaz. Klavye kisayollari kaldirildi (harfler
gorunmeyince hangi tusun nereye goturdugu bilinemiyordu). Duran tuslar:
R yeniden kur, N yeni board, Z geri al, Tab ayar paneli.
