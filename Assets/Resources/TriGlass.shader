// Parcalarin cam hali. TriLit'in saydam esi: govde yari saydam, kenarlar hem
// daha opak hem daha parlak - cama kenarindan bakinca kat edilen kalinlik
// artiyor, gercek camin kenarinin parlak gorunmesi bu yuzden.
//
// Zemin ve bos oyuncu TriLit'te kaliyor; sadece toplanacak parcalar bunu
// kullaniyor. Resources altinda olmasi sart: sahne calisma aninda kuruluyor,
// hicbir materyal shaderi referans etmedigi icin derlemede silinirdi.
Shader "Pirinc/TriGlass"
{
	Properties
	{
		_Color ("Renk", Color) = (1,1,1,0.58)
		_SpecPower ("Parlaklik keskinligi", Range(0.03, 1)) = 0.22
		_SpecAmount ("Parlaklik siddeti", Range(0, 1)) = 0.55
		_Fresnel ("Kenar aydinligi", Range(0, 1)) = 0.35
		_FresnelPower ("Kenar keskinligi", Range(1, 8)) = 2.5
	}

	SubShader
	{
		// Sis tabakalari (Sprites/Default) 3000'de. Parcalar onlardan ONCE
		// cizilmeli, yoksa alt katin sisi ust kattaki parcalarin da uzerine
		// biniyor ve ekran yine perdeleniyor. 2900 bunu garantiliyor.
		Tags { "Queue" = "Transparent-100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
		LOD 200

		// Derinlige YAZIYOR. Arka yuzler elendigi icin kapali bir parcanin
		// gorunen yuzleri hic ust uste binmiyor, yani saydamin alisildik
		// siralama sorunu burada yok. Yazmasaydi sis tabakalari derinlik
		// testine takilmaz ve parcalarin onune gecerdi.
		ZWrite On

		CGPROGRAM
		// noshadow: ileri yolda saydam yuzeyin golge ALMASI dogru calismiyor.
		// Golge BIRAKMA shadowcaster pasindan devam ediyor, yani parca hala
		// zemine oturuyor.
		#pragma surface surf BlinnPhong alpha:fade noshadow
		#pragma target 3.0

		fixed4 _Color;
		half _SpecPower;
		half _SpecAmount;
		half _Fresnel;
		half _FresnelPower;

		struct Input
		{
			float3 viewDir;
		};

		void surf(Input IN, inout SurfaceOutput o)
		{
			half f = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), _FresnelPower);

			o.Albedo = lerp(_Color.rgb, half3(0.93, 0.93, 0.95), f * _Fresnel);
			o.Specular = _SpecPower;
			o.Gloss = _SpecAmount;
			o.Alpha = lerp(_Color.a, 1.0, f);
		}
		ENDCG
	}

	FallBack "Legacy Shaders/Transparent/Diffuse"
}
