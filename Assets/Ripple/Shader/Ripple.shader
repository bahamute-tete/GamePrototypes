Shader "Water/Ripple"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color("Color",Color) = (1,1,1,1)
        _RippleSpeed("RippleSpeed",float) = 1
        _RippleAmount("RippleAmount",float) = 1
        _RippleFreq("RippleFreq",float) =1
        _cood("_cood",vector) =(0,0,0,0)
        _WaveX1("_WaveX1",float) = 0
        _WaveZ1("_WaveZ1",float) = 0
    }
    SubShader
    {

        
        Pass
        {
      	    HLSLPROGRAM
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
            #include "Ripplehlsl.hlsl"
			ENDHLSL
        }
    }
}
