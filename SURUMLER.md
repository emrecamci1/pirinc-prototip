# Surumler

Her itilen degisiklik bir surum numarasi alir. Numara ekranin sag ustunde, Ayar
dugmesinin altinda yazar - telefonda hangi surumu oynadigin oradan belli olur.
Ayni numara git etiketi olarak da atilir, yani:

    git checkout v3        (o surume bak)
    git diff v3 v5         (arada ne degisti)

"v3 daha iyiydi" demen o surume birebir donmek icin yeterli.

Kural: numara sadece ITILEN degisikliklerde artar. Denenen ama begenilmeyip geri
alinan bir sey de numara alir, cunku sen onu oynadin; geri alma islemi de ayri bir
numaradir. Boylece hicbir ara hal kaybolmaz.

| Surum | Commit | Ne degisti | Karar |
|-------|--------|------------|-------|
| v4 | 642fcf0 | Rozet rengi tahtadaki parca rengiyle esitlendi (`Swatch`, isik carpani sadece renk hesabi olarak). Yeni isik yok. | Telefonda bakilacak |
| v3 | 30f8e5e | v2 geri alindi: kod birebir v1, TriGlass shader silindi. Ekran etiketi v3, icerik v1. | Onayli |
| v2 | 5e39f25 | Parcalar cam oldu: TriGlass shader (yari saydam govde, fresnel kenar), ust yuz parlamasi icin ucuncu isik, panelde `Cam` ayari. Zemin ve tahta aynen duruyor. | GERI ALINDI (v3) |
| v1 | d802569 | Gorsel olarak onaylanan hal: pahli kenar, spekular shader, iki isik + degrade ortam isigi. Rozetler ham renkle ciziliyor. Surum etiketi eklendi. | Onayli |

## v1 oncesi (numarasiz)

Surum sayaci baslamadan once yapilanlar. Gorsel tur burada gecti, iki deneme geri
alindi; ayrintisi NOTLAR.md "Gorsel" bolumunde.

| Commit | Ne |
|--------|-----|
| e5757d1 | Gorsel: pahli kenar, spekular shader, iki isik ve degrade ortam - BEGENILEN hal |
| 7168a27 | Isik kalibrasyonu (ust yuz carpani 1.0) - geri alindi |
| 7c45712 | Kalibrasyon revert + rozete isik carpani - rozet kismi geri alindi |
| bbe0e90 | Contrast duzeltmesi (golge tam, dolgu 0.20, kenar isigi 0) - geri alindi |
| ae9c23d | bbe0e90 revert |
| 5f68c20 | Rozet carpani revert - icerik olarak e5757d1 |
