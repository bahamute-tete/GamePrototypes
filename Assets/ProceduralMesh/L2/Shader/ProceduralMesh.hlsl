
//UNITY_SHADER_NO_UPGRADE



void Ripple_float (float3 PositionIn, float3 Origin,float Period, float Speed, float Amplitude,
	out float3 PositionOut,out float3 NormalOut,out float3 TangentOut)
{
	float d = length(PositionIn - Origin);
	float f= 2.0*PI*Period *(d-Speed*_Time.y);
	
	PositionOut =PositionIn + float3(0.0,Amplitude * sin(f),0.0);

	//对曲面函数求偏导x,偏导z,求法线
	//f =2 pi p (d -st) ; y=acos(f) ; dy/dx= dy/dd*dd/dx=acos(f)*2pi*t * dd/dx
	//dd/dx =x/sqrt(x^2+y^2+z^2)  又因为 d= sqrt(x^2+y^2+z^2)
	//dd/dx =x/d
	//dy/dx =cos(f)*2pi*t *x/d=(cos(f)*2pi*t/d)x

	
	float2 derivatives = (2.0 * PI * Amplitude * Period * cos(f) / max(d,0.001)) *(PositionIn - Origin).xz;

	//构造2个向量在yz 平面和 xy 平面
	TangentOut =  float3(1.,derivatives.x,0.);
	NormalOut = saturate( cross(float3(0.,derivatives.y,1.),TangentOut));

}


