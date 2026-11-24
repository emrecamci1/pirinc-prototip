// Parcalarin yuzey shaderi. Legacy Diffuse duz Lambert oldugu icin yigin tek
// parca bir leke gibi okunuyordu: ne kalinlik ne de pah kenarlar belli oluyordu.
// Burada spekular bir vurgu ve ince bir kenar isigi var; hacim esas bunlardan
// okunuyor.
//
// Resources altinda duruyor cunku sahne tamamen calisma aninda kuruluyor:
// hicbir materyal bu shaderi referans etmedigi icin derlemede silinirdi.
Shader "Pirinc/TriLit"
{
	Properties
	{
		_Color ("Renk", Color) = (1,1,1,1)
		_SpecPower ("Parlaklik keskinligi", Range(0.03, 1)) = 0.30
		_SpecAmount ("Parlaklik siddeti", Range(0, 1)) = 0.14
		_RimAmount ("Kenar isigi", Range(0, 1)) = 0.0
		_RimPower ("Kenar isigi keskinligi", Range(1, 8)) = 3.5
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
		#pragma surface surf BlinnPhong fullforwardshadows
		#pragma target 3.0

		fixed4 _Color;
		half _SpecPower;
		half _SpecAmount;
		half _RimAmount;
		half _RimPower;

		struct Input
		{
			float3 viewDir;
		};

		void surf(Input IN, inout SurfaceOutput o)
		{
			o.Albedo = _Color.rgb;
			o.Specular = _SpecPower;
			o.Gloss = _SpecAmount;

			// Kenara dogru hafif bir aydinlanma: pahlarin nerede bittigi ve
			// parcanin zeminden ayrildigi yer boylece tek bakista goruluyor.
			half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
			o.Emission = _Color.rgb * pow(rim, _RimPower) * _RimAmount;
			o.Alpha = 1.0;
		}
		ENDCG
	}

	FallBack "Legacy Shaders/Diffuse"
}
